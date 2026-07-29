namespace AudioLeap.Core.Audio;

/// <summary>Instantánea inmutable de un dispositivo de salida activo.</summary>
public sealed record AudioDevice(string Id, string Name, AudioFormFactor FormFactor, bool IsDefault)
{
    /// <summary>Nombre a mostrar en OSD/mezclador/tray: el personalizado si existe, si no el real.
    /// El campo <see cref="Name"/> conserva siempre el nombre real de Windows (visible en configuración).</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>Clave de icono personalizado (ver DeviceIcons); nulo = icono automático según <see cref="FormFactor"/>.</summary>
    public string? IconKey { get; init; }
}

/// <summary>Estado del dispositivo predeterminado para pintar el OSD.</summary>
public sealed record AudioState(AudioDevice Device, int VolumePercent, bool IsMuted);

/// <summary>Dispositivo + su volumen, para el mezclador.</summary>
public sealed record DeviceVolume(AudioDevice Device, int VolumePercent, bool IsMuted);
