// AppAudioService.cs — Volumen por aplicación mediante la Audio Session API (WASAPI).
// Enumera las sesiones del dispositivo de salida predeterminado, las agrupa por proceso
// y permite ajustar volumen/silencio de cada aplicación. Análogo a AudioService, pero
// para sesiones en lugar de dispositivos.
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AudioLeap.Core.Audio;

public sealed class AppAudioService : IDisposable
{
    private static readonly Guid IID_IAudioSessionManager2 = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
    private const int CLSCTX_ALL = 23;

    private readonly IMMDeviceEnumerator _enumerator;
    private Guid _eventContext = Guid.NewGuid();

    // Controles de volumen vivos entre refrescos: por cada grupo (app), una o más
    // sesiones. Se aplican los cambios sin re-enumerar; se liberan en cada refresco.
    private readonly Dictionary<string, List<ISimpleAudioVolume>> _volumesById = new();

    public AppAudioService()
    {
        _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
    }

    /// <summary>Aplicaciones con audio en el dispositivo predeterminado, agrupadas por proceso
    /// y en el orden en que las reporta Windows. Los sonidos del sistema van como una entrada más.</summary>
    public IReadOnlyList<AppAudioSession> GetSessions()
    {
        ReleaseVolumes();
        var order = new List<string>();
        var byKey = new Dictionary<string, AppAudioSession>();

        var device = GetDefaultRenderDevice();
        if (device is null) return Array.Empty<AppAudioSession>();
        try
        {
            var iid = IID_IAudioSessionManager2;
            device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out object mgrObj);
            var manager = (IAudioSessionManager2)mgrObj;
            try
            {
                if (manager.GetSessionEnumerator(out var sessions) != 0) return Array.Empty<AppAudioSession>();
                try
                {
                    sessions.GetCount(out int count);
                    for (int i = 0; i < count; i++)
                    {
                        if (sessions.GetSession(i, out var control) != 0) continue;
                        bool kept = false;
                        try { kept = ReadSession(control, order, byKey); }
                        catch { /* sesión desaparecida a mitad de lectura */ }
                        // Si conservamos su volumen, el RCW se comparte: NO liberarlo aquí
                        // (lo haría inutilizable); se libera en ReleaseVolumes. Solo se
                        // libera el de las sesiones descartadas (expiradas).
                        finally { if (!kept) Marshal.ReleaseComObject(control); }
                    }
                }
                finally { Marshal.ReleaseComObject(sessions); }
            }
            finally { Marshal.ReleaseComObject(manager); }
        }
        catch { /* sin dispositivo o sin sesiones */ }
        finally { Marshal.ReleaseComObject(device); }

        return order.Select(k => byKey[k]).ToList();
    }

    /// <summary>Fija el volumen (0–100) de una aplicación (todas sus sesiones).</summary>
    public void SetSessionVolume(string id, int percent)
    {
        if (!_volumesById.TryGetValue(id, out var volumes)) return;
        float level = Math.Clamp(percent, 0, 100) / 100f;
        foreach (var vol in volumes)
        {
            try
            {
                vol.SetMasterVolume(level, ref _eventContext);
                if (percent > 0) vol.SetMute(false, ref _eventContext);
            }
            catch { /* sesión cerrada */ }
        }
    }

    /// <summary>Alterna el silencio de una aplicación. Devuelve el nuevo estado (o null si ya no existe).</summary>
    public bool? ToggleSessionMute(string id)
    {
        if (!_volumesById.TryGetValue(id, out var volumes) || volumes.Count == 0) return null;
        try
        {
            volumes[0].GetMute(out bool muted);
            bool target = !muted;
            foreach (var vol in volumes)
                try { vol.SetMute(target, ref _eventContext); } catch { }
            return target;
        }
        catch { return null; }
    }

    /// <summary>Lee una sesión. Devuelve true si se conserva su control de volumen
    /// (en tal caso el llamador NO debe liberar el RCW, pues se comparte y se guarda).</summary>
    private bool ReadSession(IAudioSessionControl control,
        List<string> order, Dictionary<string, AppAudioSession> byKey)
    {
        var control2 = (IAudioSessionControl2)control;

        control2.GetState(out var state);
        if (state == AudioSessionState.Expired) return false; // sesión terminada

        bool isSystem = control2.IsSystemSoundsSession() == 0; // S_OK = sí
        uint pid = 0;
        if (!isSystem) control2.GetProcessId(out pid);
        string key = isSystem ? "system" : "pid:" + pid;

        // El propio control de sesión implementa ISimpleAudioVolume (QueryInterface):
        // es el MISMO objeto COM (mismo RCW), por eso al conservarlo no se libera el control.
        var volume = (ISimpleAudioVolume)control;
        volume.GetMasterVolume(out float level);
        volume.GetMute(out bool muted);

        if (!_volumesById.TryGetValue(key, out var volumes))
        {
            volumes = new List<ISimpleAudioVolume>();
            _volumesById[key] = volumes;
            order.Add(key);
        }
        volumes.Add(volume); // se conserva la referencia COM para aplicar cambios luego

        // La primera sesión del grupo define lo que se muestra.
        if (!byKey.ContainsKey(key))
        {
            var (name, path) = isSystem ? ("", (string?)null) : ResolveProcess(pid);
            byKey[key] = new AppAudioSession(
                key, pid, name, path, isSystem,
                (int)Math.Round(level * 100), muted);
        }
        return true;
    }

    /// <summary>Nombre amigable y ruta del ejecutable de un proceso. Tolera procesos
    /// elevados o ya cerrados (devuelve un nombre de reserva y ruta nula).</summary>
    private static (string Name, string? Path) ResolveProcess(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            string? path = null;
            string name = process.ProcessName;
            try
            {
                path = process.MainModule?.FileName;
                var description = process.MainModule?.FileVersionInfo.FileDescription;
                if (!string.IsNullOrWhiteSpace(description)) name = description!.Trim();
            }
            catch { /* MainModule no accesible (proceso elevado/32-64): usar ProcessName */ }
            return (name, path);
        }
        catch { return ($"PID {pid}", null); }
    }

    private IMMDevice? GetDefaultRenderDevice()
    {
        try
        {
            int hr = _enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device);
            return hr == 0 ? device : null;
        }
        catch { return null; }
    }

    private void ReleaseVolumes()
    {
        foreach (var volumes in _volumesById.Values)
            foreach (var vol in volumes)
                try { Marshal.ReleaseComObject(vol); } catch { }
        _volumesById.Clear();
    }

    public void Dispose()
    {
        ReleaseVolumes();
        Marshal.ReleaseComObject(_enumerator);
    }
}
