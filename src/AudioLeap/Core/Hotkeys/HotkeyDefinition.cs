using System.Windows.Input;

namespace AudioLeap.Core.Hotkeys;

public enum HotkeyAction { VolumeUp, VolumeDown, ToggleMute, NextDevice, PreviousDevice, ShowMixer, ShowAppMixer }

/// <summary>
/// Combinación de teclas serializable como texto ("Win+Alt+Up").
/// Formato: modificadores (Win, Ctrl, Alt, Shift) + nombre de tecla del enum WPF <see cref="Key"/>.
/// </summary>
public readonly record struct HotkeyDefinition(ModifierKeys Modifiers, Key Key)
{
    public static bool TryParse(string? text, out HotkeyDefinition hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        ModifierKeys mods = ModifierKeys.None;
        Key key = Key.None;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "win" or "windows": mods |= ModifierKeys.Windows; break;
                case "ctrl" or "control": mods |= ModifierKeys.Control; break;
                case "alt": mods |= ModifierKeys.Alt; break;
                case "shift": mods |= ModifierKeys.Shift; break;
                default:
                    if (!Enum.TryParse(raw, ignoreCase: true, out key)) return false;
                    break;
            }
        }
        if (key == Key.None) return false;
        hotkey = new HotkeyDefinition(mods, key);
        return true;
    }

    public override string ToString()
    {
        var parts = new List<string>(4);
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }
}
