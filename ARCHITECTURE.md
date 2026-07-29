# AudioLeap — Arquitectura

Utilidad ligera para Windows que cambia el dispositivo de salida de audio y controla el volumen mediante atajos globales, con un OSD estilo Windows 11.

## Stack elegido: C# / .NET 8 + WPF

Justificación frente a las 5 prioridades:

1. **Máximo rendimiento** — .NET 8 con JIT por niveles y PGO arranca en <300 ms y queda inactivo sin consumo de CPU (todo es dirigido por eventos: hotkeys y callbacks COM; no hay polling). RAM típica: 30–45 MB, dentro del objetivo de 50 MB. Es el mismo stack de EarTrumpet.
2. **Integración con Windows** — Acceso directo a Core Audio (COM interop sin dependencias externas), `RegisterHotKey`, registro, DWM y system tray.
3. **Animaciones fluidas** — WPF renderiza composición por hardware (DirectX) con animaciones dependientes del reloj de composición, no del hilo de UI → 60 FPS estables.
4. **Ejecutable pequeño** — Framework-dependent: ~1–2 MB. (Self-contained opcional ~60 MB si se quiere portabilidad total.)
5. **Fácil mantenimiento** — C#/XAML, tooling maduro, sin dependencias NuGet: todo el interop es propio y auditable.

Alternativas descartadas: **C++/Win32** (mínimo consumo pero animaciones y mantenimiento mucho más costosos), **WinUI 3** (Mica nativo pero +80 MB RAM, runtime adicional y overlays menos fiables sobre pantalla completa), **Electron/Tauri** (consumo o inmadurez del ecosistema Windows-audio).

## Diagrama de módulos

```
                 ┌───────────────────────────────┐
                 │        App (composition root)  │
                 └──┬────┬─────┬─────┬─────┬─────┘
                    │    │     │     │     │
        ┌───────────▼┐ ┌─▼───────┐ ┌─▼──────────┐ ┌▼──────────┐ ┌▼───────────┐
        │AudioService│ │ Hotkey  │ │ OsdManager │ │  Tray     │ │ Settings   │
        │(Core Audio)│ │ Manager │ │ + OsdWindow│ │ Manager   │ │ Manager    │
        └───────────┬┘ └─────────┘ └─┬──────────┘ └───────────┘ └┬───────────┘
                    │                │                            │
        ┌───────────▼┐             ┌─▼──────────┐   ┌─────────────▼┐
        │CoreAudio   │             │ThemeManager│   │StartupManager│
        │Interop(COM)│             │WindowInterop│  │ (registro)   │
        └────────────┘             └────────────┘   └──────────────┘
```

## Módulos

| Módulo | Carpeta | Responsabilidad |
|---|---|---|
| **AudioService** | `Core/Audio` | Enumerar dispositivos activos, cambiar dispositivo predeterminado (IPolicyConfig), volumen/mute (IAudioEndpointVolume), notificaciones de cambios (IMMNotificationClient). No conoce UI. |
| **CoreAudioInterop** | `Core/Audio` | Declaraciones COM puras (IMMDeviceEnumerator, IPolicyConfig, etc.). Sin lógica. |
| **HotkeyManager** | `Core/Hotkeys` | Atajos globales vía `RegisterHotKey` sobre una ventana message-only. Expone eventos por acción. Re-registrable en caliente al cambiar configuración. |
| **SettingsManager** | `Core/Settings` | Carga/guarda `%APPDATA%\AudioLeap\settings.json`. Evento `SettingsChanged` al que se suscriben los demás módulos. |
| **StartupManager** | `Core/Settings` | Alta/baja en `HKCU\...\Run` para inicio con Windows. |
| **ThemeManager** | `Core/Theme` | Detecta tema claro/oscuro y color de acento; publica brushes como recursos de aplicación; reacciona a cambios del sistema. |
| **OsdManager / OsdWindow** | `UI/Osd` | Ventana flotante topmost sin foco (WS_EX_NOACTIVATE), animaciones de entrada/salida y de barra, auto-ocultado con temporizador, posición/escala/duración configurables. |
| **TrayManager** | `UI/Tray` | Icono en el área de notificación, menú con lista de dispositivos, acceso a configuración y salida. |
| **SettingsWindow** | `UI/Settings` | Ventana de configuración (atajos, OSD, tema, inicio con Windows). |
| **WindowInterop** | `Interop` | P/Invoke de ventanas: topmost real, no-activación, esquinas redondeadas DWM, blur acrílico opcional. |

## Decisiones clave

- **Desacoplamiento por eventos**: `App.xaml.cs` es el único punto que conecta módulos (HotkeyManager → AudioService → OsdManager). Ningún módulo referencia a otro directamente, lo que permite añadir perfiles de audio, cambio automático por aplicación, control de brillo, etc., sin tocar lo existente.
- **Sin polling**: cero timers de fondo. CPU ~0 % en reposo. Los cambios externos de dispositivos llegan por `IMMNotificationClient`.
- **OSD sobre pantalla completa**: `HWND_TOPMOST + WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW`, reafirmado en cada aparición. Funciona sobre juegos en *borderless/windowed fullscreen* (la mayoría hoy). En *exclusive fullscreen* Windows no permite overlays de ningún tipo; los atajos siguen funcionando y el cambio se aplica igualmente.
- **`IPolicyConfig`**: API no documentada pero estable desde Vista, la misma que usan SoundSwitch/EarTrumpet. Es la única forma de cambiar el dispositivo predeterminado sin abrir el panel del sistema.
- **NotifyIcon de WinForms**: evita dependencias NuGet; solo se usa esa clase, sin message pump de WinForms adicional.

## Extensibilidad futura

Cada funcionalidad futura se implementa como módulo nuevo suscrito a los existentes: perfiles de audio (nuevo `ProfileService` sobre `AudioService`), cambio automático al conectar auriculares (ya llega `OnDeviceAdded`), cambio por aplicación (nuevo watcher de foreground window), control de brillo/multimedia (nuevas acciones en `HotkeyAction` + servicio propio), Bluetooth (extensión de `AudioService`).

## Compilar

```
dotnet build -c Release          # requiere .NET 8 SDK
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```
