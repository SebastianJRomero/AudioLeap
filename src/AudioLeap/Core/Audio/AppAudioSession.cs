namespace AudioLeap.Core.Audio;

/// <summary>Instantánea de una aplicación con audio en el dispositivo predeterminado.
/// Las sesiones del mismo proceso se agrupan en una sola entrada.</summary>
public sealed record AppAudioSession(
    string Id,                // clave estable de la agrupación ("system" o "pid:1234")
    uint ProcessId,           // 0 para los sonidos del sistema
    string DisplayName,
    string? ExecutablePath,   // ruta del ejecutable para extraer el icono (nula = sin acceso/sistema)
    bool IsSystemSounds,
    int VolumePercent,
    bool IsMuted);
