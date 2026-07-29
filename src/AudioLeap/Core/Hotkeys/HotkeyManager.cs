// HotkeyManager.cs — Atajos globales con RegisterHotKey sobre una ventana message-only.
// Funcionan aunque el foco esté en juegos/aplicaciones a pantalla completa.
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace AudioLeap.Core.Hotkeys;

public sealed class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _source;
    private readonly Dictionary<int, HotkeyAction> _registered = new();

    /// <summary>Se dispara en el hilo de UI cuando se pulsa un atajo registrado.</summary>
    public event Action<HotkeyAction>? HotkeyPressed;

    public HotkeyManager()
    {
        // Ventana message-only: invisible, sin coste, solo recibe WM_HOTKEY.
        _source = new HwndSource(new HwndSourceParameters("AudioLeapHotkeys")
        {
            WindowStyle = 0,
            ParentWindow = HWND_MESSAGE,
            Width = 0,
            Height = 0,
        });
        _source.AddHook(WndProc);
    }

    /// <summary>
    /// (Re)registra todos los atajos. Devuelve las acciones cuyo registro falló
    /// (combinación ocupada por otra aplicación).
    /// </summary>
    public IReadOnlyList<HotkeyAction> RegisterAll(IReadOnlyDictionary<HotkeyAction, HotkeyDefinition> hotkeys)
    {
        UnregisterAll();
        var failed = new List<HotkeyAction>();
        int id = 1;
        foreach (var (action, def) in hotkeys)
        {
            uint mods = ToNativeModifiers(def.Modifiers);
            // Volumen debe repetir al mantener pulsado; el resto no.
            if (action is HotkeyAction.ToggleMute or HotkeyAction.NextDevice
                or HotkeyAction.PreviousDevice or HotkeyAction.ShowMixer)
                mods |= MOD_NOREPEAT;
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(def.Key);

            if (RegisterHotKey(_source.Handle, id, mods, vk))
                _registered[id] = action;
            else
                failed.Add(action);
            id++;
        }
        return failed;
    }

    public void UnregisterAll()
    {
        foreach (int id in _registered.Keys)
            UnregisterHotKey(_source.Handle, id);
        _registered.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _registered.TryGetValue(wParam.ToInt32(), out var action))
        {
            HotkeyPressed?.Invoke(action);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static uint ToNativeModifiers(ModifierKeys mods)
    {
        uint result = 0;
        if (mods.HasFlag(ModifierKeys.Alt)) result |= MOD_ALT;
        if (mods.HasFlag(ModifierKeys.Control)) result |= MOD_CONTROL;
        if (mods.HasFlag(ModifierKeys.Shift)) result |= MOD_SHIFT;
        if (mods.HasFlag(ModifierKeys.Windows)) result |= MOD_WIN;
        return result;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.Dispose();
    }
}
