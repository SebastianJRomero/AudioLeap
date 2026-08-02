# AudioLeap

Utilidad ligera para Windows 10/11 que cambia el dispositivo de salida de audio y controla el volumen mediante atajos de teclado globales, con un OSD flotante estilo Windows 11.

## Características

Cambio instantáneo de dispositivo de salida (siguiente/anterior o selección directa desde el menú del tray), control de volumen y silencio del dispositivo activo, OSD con icono del dispositivo, nombre, barra de volumen animada, porcentaje y estado de silencio, tema claro/oscuro/automático con color de acento del sistema, atajos totalmente configurables, inicio con Windows y ejecución en segundo plano desde el área de notificación.

- **Idioma configurable** — interfaz en español o inglés, con selector en la configuración.
- **Dispositivos personalizables** — nombre e icono propios por dispositivo (parlantes, audífonos, TV, Bluetooth, etc.). El nombre real de Windows se conserva y solo se muestra en la configuración; en el OSD, el mezclador y el tray aparece el nombre y el icono personalizados.
- **Mezclador de aplicaciones** — panel con el volumen de cada aplicación que está reproduciendo audio (con su icono real), volumen y silencio por app. Se abre con su propio atajo.

## Atajos por defecto

| Acción | Atajo |
|---|---|
| Subir volumen | `Ctrl + F12` |
| Bajar volumen | `Ctrl + F11` |
| Silenciar | `Ctrl + F10` |
| Mezclador de dispositivos | `Ctrl + F9` |
| Mezclador de aplicaciones | `Ctrl + F8` |
| Siguiente dispositivo | *(sin asignar)* |
| Dispositivo anterior | *(sin asignar)* |

Un atajo vacío queda deshabilitado. En el campo de atajo, `Supr`/`Retroceso` lo borra. Los atajos globales se suspenden mientras la ventana de configuración está abierta.

Todos se cambian desde **Configuración** (doble clic en el icono del tray).

## Requisitos

- Windows 10 1903+ o Windows 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (solo para la build framework-dependent)

## Compilar

```powershell
dotnet build -c Release
dotnet run --project src/AudioLeap        # ejecutar en desarrollo

# Publicación (exe único, requiere .NET 8 Desktop Runtime instalado, ~2 MB):
dotnet publish src/AudioLeap -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

# Publicación portable (sin runtime instalado, ~60 MB):
dotnet publish src/AudioLeap -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

El ejecutable queda en `src/AudioLeap/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/`.

## Notas de compatibilidad

- El OSD aparece sobre juegos y aplicaciones en modo *borderless/windowed fullscreen* (el modo habitual hoy en DirectX/OpenGL/Vulkan). En *exclusive fullscreen* Windows no permite mostrar ninguna ventana encima; los atajos siguen funcionando y el cambio se aplica igualmente.
- El cambio de dispositivo predeterminado usa `IPolicyConfig`, la misma API interna que emplean SoundSwitch y EarTrumpet.
- La configuración se guarda en `%APPDATA%\AudioLeap\settings.json`.

## Arquitectura

Ver [ARCHITECTURE.md](ARCHITECTURE.md). Estructura:

```
src/AudioLeap/
├── App.xaml(.cs)            # Composition root: crea y conecta los módulos
├── Core/
│   ├── Audio/               # AudioService + interop COM de Core Audio
│   ├── Hotkeys/             # HotkeyManager (RegisterHotKey) + definiciones
│   ├── Localization/        # Textos de la interfaz (español / inglés)
│   ├── Settings/            # SettingsManager (JSON) + StartupManager (registro)
│   └── Theme/               # ThemeManager (claro/oscuro + acento)
├── Interop/                 # P/Invoke de ventana (topmost, no-activate, acrílico)
└── UI/
    ├── AppMixer/            # Mezclador de aplicaciones (volumen por app)
    ├── Common/              # Glifos, iconos y estilos compartidos
    ├── Mixer/               # Mezclador de dispositivos (volumen por dispositivo)
    ├── Osd/                 # OsdWindow + OsdManager (animaciones, posicionamiento)
    ├── Settings/            # Ventana de configuración
    └── Tray/                # Icono y menú del área de notificación
```

## Licencia

[MIT](LICENSE).
