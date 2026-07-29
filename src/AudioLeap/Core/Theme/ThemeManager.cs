// ThemeManager.cs — Tema claro/oscuro + color de acento del sistema.
// Publica brushes como recursos de aplicación; el XAML los consume con DynamicResource.
using System.Windows;
using System.Windows.Media;
using AudioLeap.Core.Settings;
using Microsoft.Win32;

namespace AudioLeap.Core.Theme;

public sealed class ThemeManager : IDisposable
{
    private AppTheme _configured = AppTheme.Auto;

    public ThemeManager()
    {
        // Reaccionar a cambios de tema/acento del sistema (evento, no polling).
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void Apply(AppTheme theme)
    {
        _configured = theme;
        bool dark = theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => IsSystemDark(),
        };

        var res = Application.Current.Resources;
        if (dark)
        {
            res["OsdBackgroundBrush"] = Frozen(Color.FromArgb(0xF2, 0x20, 0x20, 0x20));
            res["OsdForegroundBrush"] = Frozen(Colors.White);
            res["OsdSecondaryBrush"] = Frozen(Color.FromArgb(0xFF, 0xB0, 0xB0, 0xB0));
            res["OsdTrackBrush"] = Frozen(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
            res["WindowBackgroundBrush"] = Frozen(Color.FromRgb(0x20, 0x20, 0x20));
            res["WindowForegroundBrush"] = Frozen(Colors.White);
            res["CardBrush"] = Frozen(Color.FromRgb(0x2B, 0x2B, 0x2B));
        }
        else
        {
            res["OsdBackgroundBrush"] = Frozen(Color.FromArgb(0xF2, 0xF3, 0xF3, 0xF3));
            res["OsdForegroundBrush"] = Frozen(Color.FromRgb(0x1A, 0x1A, 0x1A));
            res["OsdSecondaryBrush"] = Frozen(Color.FromArgb(0xFF, 0x60, 0x60, 0x60));
            res["OsdTrackBrush"] = Frozen(Color.FromArgb(0x50, 0x00, 0x00, 0x00));
            res["WindowBackgroundBrush"] = Frozen(Color.FromRgb(0xF3, 0xF3, 0xF3));
            res["WindowForegroundBrush"] = Frozen(Color.FromRgb(0x1A, 0x1A, 0x1A));
            res["CardBrush"] = Frozen(Colors.White);
        }
        res["AccentBrush"] = Frozen(GetAccentColor());
    }

    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return true; }
    }

    private static Color GetAccentColor()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int abgr)
            {
                var bytes = BitConverter.GetBytes(abgr); // AABBGGRR
                return Color.FromRgb(bytes[0], bytes[1], bytes[2]);
            }
        }
        catch { }
        return Color.FromRgb(0x00, 0x78, 0xD4); // azul Windows por defecto
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle or UserPreferenceCategory.Color)
            Application.Current?.Dispatcher.BeginInvoke(() => Apply(_configured));
    }

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze(); // brushes congelados = render más rápido, sin hilos de animación extra
        return b;
    }

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
}
