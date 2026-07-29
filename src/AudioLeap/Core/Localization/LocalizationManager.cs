// LocalizationManager.cs — Idioma de la interfaz (español/inglés).
// Publica los textos como recursos de aplicación con prefijo "L_"; el XAML los
// consume con DynamicResource (se actualizan en caliente al cambiar de idioma)
// y el code-behind con Loc.T("Clave"). Mismo patrón que ThemeManager con los brushes.
using System.Windows;
using AudioLeap.Core.Settings;

namespace AudioLeap.Core.Localization;

public static class Loc
{
    private static readonly Dictionary<string, string> Spanish = new()
    {
        // Ventana de configuración
        ["SettingsTitle"] = "AudioLeap — Configuración",
        ["SectionHotkeys"] = "Atajos de teclado",
        ["HotkeysHint"] = "Haz clic en un campo y pulsa la combinación deseada. Supr o Retroceso borra el atajo (vacío = deshabilitado). Los atajos globales quedan suspendidos mientras esta ventana esté abierta.",
        ["VolumeUp"] = "Subir volumen",
        ["VolumeDown"] = "Bajar volumen",
        ["Mute"] = "Silenciar",
        ["NextDevice"] = "Siguiente dispositivo",
        ["PrevDevice"] = "Dispositivo anterior",
        ["ShowMixer"] = "Mostrar mezclador",
        ["SectionOsd"] = "OSD",
        ["Position"] = "Posición",
        ["Duration"] = "Duración",
        ["Size"] = "Tamaño",
        ["Animations"] = "Animaciones",
        ["SectionDevices"] = "Dispositivos",
        ["DevicesHint"] = "Marca los que aparecen en el mezclador y en la rotación siguiente/anterior. Debajo del nombre real puedes ponerles un nombre y un icono personalizados.",
        ["CustomNamePlaceholder"] = "Nombre personalizado",
        ["IconAuto"] = "Icono automático",
        ["IconSpeakers"] = "Parlantes",
        ["IconHeadphones"] = "Audífonos",
        ["IconHeadset"] = "Diadema / casco",
        ["IconTv"] = "TV / monitor",
        ["IconBluetooth"] = "Bluetooth",
        ["IconGame"] = "Consola",
        ["IconPhone"] = "Teléfono",
        ["IconMusic"] = "Música",
        ["IconGeneric"] = "Genérico",
        ["SectionGeneral"] = "General",
        ["VolumeStep"] = "Paso de volumen",
        ["Theme"] = "Tema",
        ["Language"] = "Idioma",
        ["StartWithWindows"] = "Iniciar con Windows",
        ["CommsRole"] = "Aplicar también a comunicaciones (llamadas)",
        ["Cancel"] = "Cancelar",
        ["Save"] = "Guardar",
        ["NoActiveDevices"] = "No hay dispositivos activos.",
        ["InvalidHotkey"] = "El atajo de \"{0}\" no es válido.",

        // Opciones de combos
        ["PosTopLeft"] = "Arriba a la izquierda",
        ["PosTopCenter"] = "Arriba al centro",
        ["PosTopRight"] = "Arriba a la derecha",
        ["PosBottomLeft"] = "Abajo a la izquierda",
        ["PosBottomCenter"] = "Abajo al centro",
        ["PosBottomRight"] = "Abajo a la derecha",
        ["ThemeAuto"] = "Automático (según el sistema)",
        ["ThemeLight"] = "Claro",
        ["ThemeDark"] = "Oscuro",

        // Tray y mezclador
        ["OutputDevices"] = "Dispositivos de salida",
        ["TraySettings"] = "Configuración…",
        ["TrayExit"] = "Salir",
        ["HotkeysBusy"] = "Algunos atajos están ocupados por otra aplicación y no se pudieron registrar.",
    };

    private static readonly Dictionary<string, string> English = new()
    {
        // Settings window
        ["SettingsTitle"] = "AudioLeap — Settings",
        ["SectionHotkeys"] = "Keyboard shortcuts",
        ["HotkeysHint"] = "Click a field and press the desired combination. Delete or Backspace clears the shortcut (empty = disabled). Global shortcuts are suspended while this window is open.",
        ["VolumeUp"] = "Volume up",
        ["VolumeDown"] = "Volume down",
        ["Mute"] = "Mute",
        ["NextDevice"] = "Next device",
        ["PrevDevice"] = "Previous device",
        ["ShowMixer"] = "Show mixer",
        ["SectionOsd"] = "OSD",
        ["Position"] = "Position",
        ["Duration"] = "Duration",
        ["Size"] = "Size",
        ["Animations"] = "Animations",
        ["SectionDevices"] = "Devices",
        ["DevicesHint"] = "Check the ones shown in the mixer and in the next/previous rotation. Below each real name you can give it a custom name and icon.",
        ["CustomNamePlaceholder"] = "Custom name",
        ["IconAuto"] = "Automatic icon",
        ["IconSpeakers"] = "Speakers",
        ["IconHeadphones"] = "Headphones",
        ["IconHeadset"] = "Headset",
        ["IconTv"] = "TV / monitor",
        ["IconBluetooth"] = "Bluetooth",
        ["IconGame"] = "Console",
        ["IconPhone"] = "Phone",
        ["IconMusic"] = "Music",
        ["IconGeneric"] = "Generic",
        ["SectionGeneral"] = "General",
        ["VolumeStep"] = "Volume step",
        ["Theme"] = "Theme",
        ["Language"] = "Language",
        ["StartWithWindows"] = "Start with Windows",
        ["CommsRole"] = "Also apply to communications (calls)",
        ["Cancel"] = "Cancel",
        ["Save"] = "Save",
        ["NoActiveDevices"] = "No active devices.",
        ["InvalidHotkey"] = "The \"{0}\" shortcut is not valid.",

        // Combo options
        ["PosTopLeft"] = "Top left",
        ["PosTopCenter"] = "Top center",
        ["PosTopRight"] = "Top right",
        ["PosBottomLeft"] = "Bottom left",
        ["PosBottomCenter"] = "Bottom center",
        ["PosBottomRight"] = "Bottom right",
        ["ThemeAuto"] = "Automatic (follow system)",
        ["ThemeLight"] = "Light",
        ["ThemeDark"] = "Dark",

        // Tray and mixer
        ["OutputDevices"] = "Output devices",
        ["TraySettings"] = "Settings…",
        ["TrayExit"] = "Exit",
        ["HotkeysBusy"] = "Some shortcuts are in use by another application and could not be registered.",
    };

    private static IReadOnlyDictionary<string, string> _current = Spanish;

    /// <summary>Activa el idioma: actualiza el diccionario actual y republica los
    /// recursos "L_*" para que todo XAML con DynamicResource se refresque al instante.</summary>
    public static void Apply(AppLanguage language)
    {
        _current = language == AppLanguage.English ? English : Spanish;
        var res = Application.Current.Resources;
        foreach (var (key, value) in _current)
            res["L_" + key] = value;
    }

    /// <summary>Texto localizado para code-behind (menús del tray, mensajes, combos).</summary>
    public static string T(string key) => _current.TryGetValue(key, out var v) ? v : key;
}
