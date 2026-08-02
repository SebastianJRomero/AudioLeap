namespace AudioLeap.Core.Settings;

public enum OsdPosition { TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight }
public enum AppTheme { Auto, Light, Dark }
public enum AppLanguage { Spanish, English }

/// <summary>Modelo de configuración persistido como JSON. Valores por defecto sensatos.</summary>
public sealed class AppSettings
{
    // Atajos (texto parseable por HotkeyDefinition; vacío = deshabilitado)
    public string HotkeyVolumeUp { get; set; } = "Ctrl+F12";
    public string HotkeyVolumeDown { get; set; } = "Ctrl+F11";
    public string HotkeyMute { get; set; } = "Ctrl+F10";
    public string HotkeyNextDevice { get; set; } = "";
    public string HotkeyPreviousDevice { get; set; } = "";
    public string HotkeyShowMixer { get; set; } = "Ctrl+F9";
    public string HotkeyShowAppMixer { get; set; } = "Ctrl+F8";

    // OSD
    public OsdPosition OsdPosition { get; set; } = OsdPosition.BottomCenter;
    public int OsdDurationMs { get; set; } = 1500;   // 1–2 s
    public double OsdScale { get; set; } = 1.0;      // 0.8–1.5
    public bool AnimationsEnabled { get; set; } = true;

    // Comportamiento
    public int VolumeStepPercent { get; set; } = 2;
    public bool RunAtStartup { get; set; } = false;
    public AppTheme Theme { get; set; } = AppTheme.Auto;
    public AppLanguage Language { get; set; } = AppLanguage.Spanish;
    public bool AlsoSetCommunicationsRole { get; set; } = false;

    /// <summary>Dispositivos ocultos en el mezclador y en la rotación siguiente/anterior
    /// (el usuario marca sus favoritos; los demás se excluyen).</summary>
    public List<string> ExcludedDeviceIds { get; set; } = new();

    /// <summary>Nombre e icono personalizados por dispositivo (clave = id del dispositivo).
    /// El nombre/icono real de Windows se conserva y solo se muestra en la configuración.</summary>
    public Dictionary<string, DeviceCustomization> DeviceCustomizations { get; set; } = new();
}

/// <summary>Personalización visual de un dispositivo. Campos nulos/vacíos = usar el valor real.</summary>
public sealed class DeviceCustomization
{
    /// <summary>Nombre a mostrar en el OSD, el mezclador y el tray. Vacío = nombre real de Windows.</summary>
    public string? Name { get; set; }

    /// <summary>Clave de icono predeterminado (ver DeviceIcons). Nulo/"auto" = icono automático según el tipo.</summary>
    public string? IconKey { get; set; }
}
