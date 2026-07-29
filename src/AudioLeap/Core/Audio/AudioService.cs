// AudioService.cs — Único módulo que habla con Core Audio.
// Sin polling: los cambios externos llegan por IMMNotificationClient.
using System.Runtime.InteropServices;
using AudioLeap.Core.Settings;

namespace AudioLeap.Core.Audio;

public sealed class AudioService : IDisposable
{
    private static readonly Guid IID_IAudioEndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    private const int CLSCTX_ALL = 23;

    private readonly IMMDeviceEnumerator _enumerator;
    private readonly IPolicyConfig _policyConfig;
    private readonly NotificationClient _notificationClient;
    private Guid _eventContext = Guid.NewGuid();

    /// <summary>Cambios de dispositivos u orden por causas externas (conectar/desconectar, otro programa, etc.).</summary>
    public event Action? DevicesChanged;
    /// <summary>El predeterminado cambió desde fuera de la app.</summary>
    public event Action? ExternalDefaultChanged;

    /// <summary>Aplicar también el rol de comunicaciones al cambiar dispositivo.</summary>
    public bool AlsoSetCommunicationsRole { get; set; }

    /// <summary>Ids excluidos por el usuario: no aparecen en el mezclador ni en la
    /// rotación siguiente/anterior. Si el filtro dejara la lista vacía, se ignora.</summary>
    public HashSet<string> ExcludedDeviceIds { get; set; } = new();

    /// <summary>Nombre/icono personalizados por dispositivo. Se aplican al leer cada
    /// dispositivo: rellenan DisplayName e IconKey sin alterar el Name real de Windows.</summary>
    public IReadOnlyDictionary<string, DeviceCustomization> DeviceCustomizations { get; set; }
        = new Dictionary<string, DeviceCustomization>();

    public AudioService()
    {
        _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        _policyConfig = (IPolicyConfig)new PolicyConfigComObject();
        _notificationClient = new NotificationClient(this);
        _enumerator.RegisterEndpointNotificationCallback(_notificationClient);
    }

    // ── Dispositivos ──────────────────────────────────────────────────────

    /// <summary>Dispositivos de salida ACTIVOS, en el orden estable que reporta Windows.</summary>
    public IReadOnlyList<AudioDevice> GetActiveDevices()
    {
        var result = new List<AudioDevice>();
        string? defaultId = GetDefaultDeviceId();

        _enumerator.EnumAudioEndpoints(EDataFlow.eRender, DeviceState.Active, out var collection);
        collection.GetCount(out int count);
        for (int i = 0; i < count; i++)
        {
            collection.Item(i, out var device);
            try { result.Add(ReadDevice(device, defaultId)); }
            catch { /* dispositivo desaparecido a mitad de enumeración */ }
            finally { Marshal.ReleaseComObject(device); }
        }
        Marshal.ReleaseComObject(collection);
        return result;
    }

    public AudioDevice? GetDefaultDevice()
    {
        var device = GetDefaultMMDevice();
        if (device is null) return null;
        try { return ReadDevice(device, null) with { IsDefault = true }; }
        finally { Marshal.ReleaseComObject(device); }
    }

    /// <summary>Fija el dispositivo predeterminado del sistema. Efecto inmediato.</summary>
    public void SetDefaultDevice(string deviceId)
    {
        // Evitar re-ruteos redundantes del motor de audio: cada SetDefaultEndpoint
        // obliga a todas las apps a migrar su sesión de audio (es lo que causa
        // el microcongelamiento en reproductores como Wallpaper Engine).
        if (GetDefaultDeviceId() == deviceId) return;

        _policyConfig.SetDefaultEndpoint(deviceId, ERole.eConsole);
        _policyConfig.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
        if (AlsoSetCommunicationsRole)
            _policyConfig.SetDefaultEndpoint(deviceId, ERole.eCommunications);
    }

    /// <summary>Dispositivos activos sin los excluidos por el usuario (favoritos).</summary>
    public IReadOnlyList<AudioDevice> GetFavoriteDevices()
    {
        var all = GetActiveDevices();
        var filtered = all.Where(d => !ExcludedDeviceIds.Contains(d.Id)).ToList();
        return filtered.Count > 0 ? filtered : all; // nunca dejar al usuario sin opciones
    }

    /// <summary>Salta al siguiente (+1) o anterior (-1) dispositivo favorito. Devuelve el nuevo estado.</summary>
    public AudioState? CycleDevice(int direction)
    {
        var devices = GetFavoriteDevices().ToList();
        if (devices.Count == 0) return null;
        int current = devices.FindIndex(d => d.IsDefault);
        int next = current < 0 ? 0 : (current + direction + devices.Count) % devices.Count;
        SetDefaultDevice(devices[next].Id);
        return GetState();
    }

    // ── Volumen (siempre sobre el dispositivo predeterminado actual) ──────

    /// <summary>Suma <paramref name="deltaPercent"/> (puede ser negativo). Devuelve el nuevo estado.</summary>
    public AudioState? ChangeVolume(int deltaPercent)
    {
        return WithEndpointVolume((vol) =>
        {
            vol.GetMasterVolumeLevelScalar(out float level);
            float target = Math.Clamp(level + deltaPercent / 100f, 0f, 1f);
            vol.SetMasterVolumeLevelScalar(target, ref _eventContext);
            if (target > 0) vol.SetMute(false, ref _eventContext); // subir volumen desactiva mute (como Windows)
        });
    }

    public AudioState? ToggleMute()
    {
        return WithEndpointVolume((vol) =>
        {
            vol.GetMute(out bool muted);
            vol.SetMute(!muted, ref _eventContext);
        });
    }

    /// <summary>Estado actual (dispositivo + volumen + mute) para pintar el OSD.</summary>
    public AudioState? GetState() => WithEndpointVolume(null);

    // ── Volumen por dispositivo (para el mezclador) ───────────────────────

    /// <summary>Dispositivos favoritos con su volumen y estado de silencio (para el mezclador).</summary>
    public IReadOnlyList<DeviceVolume> GetDevicesWithVolume()
    {
        var result = new List<DeviceVolume>();
        foreach (var device in GetFavoriteDevices())
        {
            int vol = 0; bool muted = false;
            WithEndpointVolumeFor(device.Id, v =>
            {
                v.GetMasterVolumeLevelScalar(out float level);
                v.GetMute(out muted);
                vol = (int)Math.Round(level * 100);
            });
            result.Add(new DeviceVolume(device, vol, muted));
        }
        return result;
    }

    /// <summary>Fija el volumen (0–100) de un dispositivo concreto, sea o no el predeterminado.</summary>
    public void SetDeviceVolume(string deviceId, int percent)
    {
        WithEndpointVolumeFor(deviceId, v =>
        {
            v.SetMasterVolumeLevelScalar(Math.Clamp(percent, 0, 100) / 100f, ref _eventContext);
            if (percent > 0) v.SetMute(false, ref _eventContext);
        });
    }

    private void WithEndpointVolumeFor(string deviceId, Action<IAudioEndpointVolume> action)
    {
        try
        {
            if (_enumerator.GetDevice(deviceId, out var device) != 0) return;
            try
            {
                var iid = IID_IAudioEndpointVolume;
                device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out object obj);
                var vol = (IAudioEndpointVolume)obj;
                try { action(vol); }
                finally { Marshal.ReleaseComObject(vol); }
            }
            finally { Marshal.ReleaseComObject(device); }
        }
        catch { /* dispositivo desconectado a mitad de operación */ }
    }

    // ── Internos ──────────────────────────────────────────────────────────

    private AudioState? WithEndpointVolume(Action<IAudioEndpointVolume>? action)
    {
        var device = GetDefaultMMDevice();
        if (device is null) return null;
        try
        {
            var iid = IID_IAudioEndpointVolume;
            device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out object obj);
            var vol = (IAudioEndpointVolume)obj;
            try
            {
                action?.Invoke(vol);
                vol.GetMasterVolumeLevelScalar(out float level);
                vol.GetMute(out bool muted);
                var info = ReadDevice(device, null) with { IsDefault = true };
                return new AudioState(info, (int)Math.Round(level * 100), muted);
            }
            finally { Marshal.ReleaseComObject(vol); }
        }
        catch { return null; }
        finally { Marshal.ReleaseComObject(device); }
    }

    private IMMDevice? GetDefaultMMDevice()
    {
        try
        {
            int hr = _enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device);
            return hr == 0 ? device : null;
        }
        catch { return null; } // sin dispositivos
    }

    private string? GetDefaultDeviceId()
    {
        var device = GetDefaultMMDevice();
        if (device is null) return null;
        try { device.GetId(out string id); return id; }
        finally { Marshal.ReleaseComObject(device); }
    }

    private AudioDevice ReadDevice(IMMDevice device, string? defaultId)
    {
        device.GetId(out string id);
        device.OpenPropertyStore(0 /*STGM_READ*/, out var store);
        try
        {
            string name = ReadStringProp(store, PropertyKeys.DeviceFriendlyName) ?? "Dispositivo de audio";
            var form = (AudioFormFactor)ReadUIntProp(store, PropertyKeys.AudioEndpointFormFactor);
            bool isDefault = defaultId is not null && id == defaultId;

            // Aplicar personalización del usuario (nombre/icono) sin perder los datos reales.
            string display = name;
            string? iconKey = null;
            if (DeviceCustomizations.TryGetValue(id, out var custom))
            {
                if (!string.IsNullOrWhiteSpace(custom.Name)) display = custom.Name!.Trim();
                iconKey = custom.IconKey;
            }
            return new AudioDevice(id, name, form, isDefault) { DisplayName = display, IconKey = iconKey };
        }
        finally { Marshal.ReleaseComObject(store); }
    }

    private static string? ReadStringProp(IPropertyStore store, PropertyKey key)
    {
        store.GetValue(ref key, out var pv);
        try { return pv.GetString(); } finally { pv.Clear(); }
    }

    private static uint ReadUIntProp(IPropertyStore store, PropertyKey key)
    {
        store.GetValue(ref key, out var pv);
        try { return pv.GetUInt(); } finally { pv.Clear(); }
    }

    public void Dispose()
    {
        try { _enumerator.UnregisterEndpointNotificationCallback(_notificationClient); } catch { }
        Marshal.ReleaseComObject(_enumerator);
        Marshal.ReleaseComObject(_policyConfig);
    }

    /// <summary>Callbacks de Windows (llegan en hilo MTA; los consumidores deben ir al hilo de UI).</summary>
    private sealed class NotificationClient(AudioService owner) : IMMNotificationClient
    {
        public void OnDeviceStateChanged(string deviceId, int newState) => owner.DevicesChanged?.Invoke();
        public void OnDeviceAdded(string deviceId) => owner.DevicesChanged?.Invoke();
        public void OnDeviceRemoved(string deviceId) => owner.DevicesChanged?.Invoke();
        public void OnDefaultDeviceChanged(EDataFlow flow, ERole role, string? deviceId)
        {
            if (flow == EDataFlow.eRender && role == ERole.eMultimedia)
                owner.ExternalDefaultChanged?.Invoke();
        }
        public void OnPropertyValueChanged(string deviceId, PropertyKey key) { }
    }
}
