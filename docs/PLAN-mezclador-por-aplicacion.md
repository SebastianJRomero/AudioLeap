# Plan — Mezclador por aplicación

Siguiente fase: además del mezclador de **dispositivos** actual, un mezclador que controle el
**volumen de cada aplicación** abierta (como el mezclador de volumen de Windows), abierto con un
**atajo propio**. No sustituye nada existente; es un módulo nuevo que sigue el mismo patrón
desacoplado por eventos del resto del proyecto.

## Objetivo

- Nuevo atajo (p. ej. `Ctrl+F8`) que abre/cierra un panel flotante con una fila por aplicación con audio.
- Cada fila: icono real de la app, nombre, deslizador de volumen + porcentaje, y silenciar al pulsar el icono.
- Misma estética y comportamiento que el mezclador de dispositivos (posición, auto-ocultado, animaciones, tema).
- Sin *polling*: los cambios se reflejan por eventos de sesión de Windows (igual que el resto de la app).

## API de Windows a usar

El volumen por aplicación vive en la **Audio Session API** (WASAPI), no en `IPolicyConfig`:

| Interfaz COM | Uso |
|---|---|
| `IAudioSessionManager2` | Se activa desde el `IMMDevice` (dispositivo de salida). Punto de entrada. |
| `IAudioSessionEnumerator` | Enumera las sesiones de audio activas del dispositivo. |
| `IAudioSessionControl` / `IAudioSessionControl2` | Estado, nombre, `GetProcessId()`, `GetIconPath()`, agrupación, detectar la sesión de sonidos del sistema. |
| `ISimpleAudioVolume` | `Get/SetMasterVolume` y `Get/SetMute` por sesión. |
| `IAudioSessionNotification` | Aviso de **sesión nueva** (app que empieza a sonar). |
| `IAudioSessionEvents` | Cambios en una sesión (volumen, silencio, desconexión/expiración). |
| `IAudioMeterInformation` *(opcional)* | Medidor de pico en vivo por app, para animar la fila. |

Notas:
- La sesión de **sonidos del sistema** tiene `ProcessId == 0` → etiquetar como "Sonidos del sistema".
- `GetDisplayName()` suele venir vacío; caer al nombre/descripción del ejecutable del proceso.
- Fase 1: enumerar solo el **dispositivo predeterminado**. Multi-dispositivo, más adelante.

## Arquitectura (encaja con lo existente)

Se replica la separación que ya usan `AudioService` (núcleo, sin UI) y `MixerManager`/`MixerWindow` (UI):

```
App.xaml.cs (composition root)
   │  registra HotkeyAction.ShowAppMixer
   ├── AppAudioService (Core/Audio)      ← nuevo: habla con la Audio Session API
   └── AppMixerManager + AppMixerWindow (UI/AppMixer)  ← nuevo: panel flotante
```

### Módulos nuevos

- **`Core/Audio/AppAudioService.cs`** — espejo de `AudioService` para sesiones:
  - `GetSessions()` → `IReadOnlyList<AppAudioSession>`.
  - `SetSessionVolume(id, percent)`, `ToggleSessionMute(id)`.
  - Eventos `SessionsChanged` (nueva/expirada) y `SessionVolumeChanged` (cambio externo), emitidos desde
    callbacks COM y marshalados a la UI con `Dispatcher` (igual que `ExternalDefaultChanged`).
- **`Core/Audio/AppAudioSession.cs`** — record inmutable:
  `record AppAudioSession(string Id, int ProcessId, string DisplayName, string? IconPath, int VolumePercent, bool IsMuted)`.
- **`UI/AppMixer/AppMixerWindow.xaml(.cs)`** y **`UI/AppMixer/AppMixerManager.cs`** — panel y orquestación
  (posición, auto-ocultado por inactividad, animaciones), reutilizando los estilos del mezclador de dispositivos.

### Interop (ampliar `Core/Audio/CoreAudioInterop.cs`)

Añadir las declaraciones COM de las interfaces de la tabla y el `IID` de `IAudioSessionManager2`.
Se activa con `device.Activate(IID_IAudioSessionManager2, ...)` desde el `IMMDevice` del predeterminado.

## Iconos de las aplicaciones

`ProcessId` → `Process.GetProcessById` → `MainModule.FileName` → `Icon.ExtractAssociatedIcon`
→ `ImageSource` (`Imaging.CreateBitmapSourceFromHIcon`). **Cachear por ruta de ejecutable.**
Si falla (proceso elevado / acceso denegado), usar un glifo genérico de `Glyphs`/`DeviceIcons`.

## Atajo y configuración

- `AppSettings`: nuevo `string HotkeyShowAppMixer` (por defecto `Ctrl+F8`).
- `HotkeyAction.ShowAppMixer`; en `App.OnHotkeyPressed` → `_appMixer.Toggle()`.
- `RegisterHotkeys` mapea el nuevo atajo.
- `SettingsWindow`: nuevo campo de atajo en la sección "Atajos de teclado".
- **Localización**: añadir claves `AppMixer` (título/etiqueta) y `SystemSounds` en `LocalizationManager` (ES/EN),
  siguiendo el patrón `L_*` ya establecido.

## Reutilización de estilos (recomendado)

El mezclador de dispositivos define en `MixerWindow.xaml` los estilos `ModernSlider`, `DeviceRowStyle`, etc.
Para no duplicarlos, extraerlos a un `ResourceDictionary` compartido (p. ej. `UI/Common/FlyoutStyles.xaml`)
y fusionarlo en `App.xaml`; ambos mezcladores lo consumen. También puede extraerse un `FlyoutManagerBase`
con la lógica común de posicionamiento y auto-ocultado (hoy repetida entre `OsdManager` y `MixerManager`).

## Fases (commits sugeridos)

- [x] 1. Interop de la Audio Session API en `CoreAudioInterop.cs`.
- [x] 2. `AppAudioService` + `AppAudioSession`: enumerar y get/set de volumen y silencio (sin eventos aún).
- [x] 3. `AppMixerWindow` + `AppMixerManager`: instantánea al abrir, reutilizando estilos del mezclador.
- [x] 4. Atajo `ShowAppMixer` (`Ctrl+F8`) + configuración + localización.
- [x] 5. Extracción y caché de iconos de aplicación.
- [ ] 6. Eventos de sesión en vivo (crear/expirar/volumen/silencio) → filas que se actualizan sin *polling*.
- [x] 7. Agrupar sesiones del mismo proceso (p. ej. varias pestañas) y fila de "Sonidos del sistema".
- [~] 8. Pulido: animaciones y estado vacío hechos; falta un icono de app en el tray/OSD si se desea.

La primera entrega (fases 1–5 y 7) reconstruye la lista **al abrir el panel**, igual que el mezclador de
dispositivos. La fase 6 (actualización en vivo por eventos de sesión) queda como siguiente paso.

## Casos límite

- **Sin aplicaciones sonando** → mostrar estado vacío ("No hay aplicaciones reproduciendo audio").
- **Procesos elevados** → puede fallar la extracción de icono; usar glifo de reserva (el volumen sí suele ser ajustable).
- **Alta rotación** de sesiones (apps que abren/cierran audio rápido) → coalescer refrescos.
- **Sesión en un dispositivo no predeterminado** → fuera de alcance en fase 1 (documentar como mejora futura).
- **Hilo de los callbacks** COM (MTA) → siempre volver al hilo de UI con `Dispatcher.BeginInvoke`.

## Fuera de alcance (fase 1)

Nombre/icono personalizados por aplicación (reutilizable del sistema de dispositivos más adelante),
control multi-dispositivo simultáneo, y persistencia propia de volúmenes (Windows ya los recuerda por app).
