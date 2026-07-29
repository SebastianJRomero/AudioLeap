// CoreAudioInterop.cs — Declaraciones COM de Core Audio (MMDevice API / EndpointVolume / PolicyConfig).
// Solo definiciones; la lógica vive en AudioService.
using System.Runtime.InteropServices;

namespace AudioLeap.Core.Audio;

internal enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
internal enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

internal static class DeviceState
{
    public const int Active = 0x1;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropertyKey
{
    public Guid fmtid;
    public int pid;
    public PropertyKey(Guid f, int p) { fmtid = f; pid = p; }
}

internal static class PropertyKeys
{
    // Nombre amigable del dispositivo ("Altavoces (Realtek...)")
    public static PropertyKey DeviceFriendlyName =
        new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
    // Factor de forma del endpoint (altavoces, auriculares, HDMI...)
    public static PropertyKey AudioEndpointFormFactor =
        new(new Guid("1da5d803-d492-4edd-8c23-e0c0ffee7f0e"), 0);
}

/// <summary>Factor de forma reportado por Windows para elegir icono.</summary>
public enum AudioFormFactor
{
    RemoteNetworkDevice = 0, Speakers = 1, LineLevel = 2, Headphones = 3,
    Microphone = 4, Headset = 5, Handset = 6, UnknownDigitalPassthrough = 7,
    SPDIF = 8, DigitalAudioDisplayDevice = 9, UnknownFormFactor = 10
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropVariant
{
    public ushort vt;
    private ushort r1, r2, r3;
    public IntPtr ptr;
    private IntPtr r4;

    public string? GetString() => vt == 31 /*VT_LPWSTR*/ ? Marshal.PtrToStringUni(ptr) : null;
    public uint GetUInt() => vt == 19 /*VT_UI4*/ ? unchecked((uint)ptr.ToInt64()) : 0u;

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant pvar);
    public void Clear() => PropVariantClear(ref this);
}

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorComObject { }

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IMMDeviceCollection devices);
    int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    int RegisterEndpointNotificationCallback(IMMNotificationClient client);
    int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
}

[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    int GetCount(out int count);
    int Item(int index, out IMMDevice device);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
        [MarshalAs(UnmanagedType.IUnknown)] out object result);
    int OpenPropertyStore(int stgmAccess, out IPropertyStore properties);
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    int GetState(out int state);
}

[ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    int GetCount(out int count);
    int GetAt(int index, out PropertyKey key);
    int GetValue(ref PropertyKey key, out PropVariant value);
    int SetValue(ref PropertyKey key, ref PropVariant value);
    int Commit();
}

[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    int RegisterControlChangeNotify(IntPtr notify);
    int UnregisterControlChangeNotify(IntPtr notify);
    int GetChannelCount(out int count);
    int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
    int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
    int GetMasterVolumeLevel(out float levelDb);
    int GetMasterVolumeLevelScalar(out float level);
    int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);
    int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
    int GetChannelVolumeLevel(uint channel, out float levelDb);
    int GetChannelVolumeLevelScalar(uint channel, out float level);
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
    int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    int GetVolumeStepInfo(out uint step, out uint stepCount);
    int VolumeStepUp(ref Guid eventContext);
    int VolumeStepDown(ref Guid eventContext);
    int QueryHardwareSupport(out uint mask);
    int GetVolumeRange(out float minDb, out float maxDb, out float incDb);
}

/// <summary>
/// API no documentada (estable desde Vista) para fijar el dispositivo predeterminado.
/// La misma que usan SoundSwitch y EarTrumpet. El orden del vtable es crítico.
/// </summary>
[ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
internal class PolicyConfigComObject { }

[ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    int GetMixFormat(IntPtr deviceId, IntPtr format);
    int GetDeviceFormat(IntPtr deviceId, int def, IntPtr format);
    int ResetDeviceFormat(IntPtr deviceId);
    int SetDeviceFormat(IntPtr deviceId, IntPtr endpointFormat, IntPtr mixFormat);
    int GetProcessingPeriod(IntPtr deviceId, int def, IntPtr defaultPeriod, IntPtr minPeriod);
    int SetProcessingPeriod(IntPtr deviceId, IntPtr period);
    int GetShareMode(IntPtr deviceId, IntPtr mode);
    int SetShareMode(IntPtr deviceId, IntPtr mode);
    int GetPropertyValue(IntPtr deviceId, int stored, IntPtr key, IntPtr value);
    int SetPropertyValue(IntPtr deviceId, int stored, IntPtr key, IntPtr value);
    int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
    int SetEndpointVisibility(IntPtr deviceId, int visible);
}

[ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMNotificationClient
{
    void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int newState);
    void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
    void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
    void OnDefaultDeviceChanged(EDataFlow flow, ERole role, [MarshalAs(UnmanagedType.LPWStr)] string? deviceId);
    void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, PropertyKey key);
}
