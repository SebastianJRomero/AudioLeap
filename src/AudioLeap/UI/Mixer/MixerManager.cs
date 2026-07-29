// MixerManager.cs — Orquesta el mezclador: alternar con el atajo, auto-ocultado
// por inactividad (se reinicia con la actividad del ratón) y posicionamiento
// igual que el OSD. El temporizador solo corre mientras el widget está visible.
using System.Windows;
using System.Windows.Threading;
using AudioLeap.Core.Audio;
using AudioLeap.Core.Settings;

namespace AudioLeap.UI.Mixer;

public sealed class MixerManager : IDisposable
{
    private readonly AudioService _audio;
    private readonly DispatcherTimer _hideTimer;
    private MixerWindow? _window;
    private AppSettings _settings;

    public MixerManager(AudioService audio, AppSettings settings)
    {
        _audio = audio;
        _settings = settings;
        _hideTimer = new DispatcherTimer();
        _hideTimer.Tick += (_, _) => Hide();
    }

    public void UpdateSettings(AppSettings settings) => _settings = settings;

    /// <summary>El atajo alterna: visible → ocultar; oculto → mostrar.</summary>
    public void Toggle()
    {
        if (_window is { IsVisible: true }) Hide();
        else Show();
    }

    public void Hide()
    {
        _hideTimer.Stop();
        _window?.AnimateOutAndHide();
    }

    private void Show()
    {
        if (_window is null)
        {
            _window = new MixerWindow();
            // Cualquier actividad del ratón sobre el widget pospone el auto-ocultado
            _window.PreviewMouseMove += (_, _) => RestartTimer();
            _window.PreviewMouseDown += (_, _) => RestartTimer();
        }

        _window.Configure(_settings.OsdScale, _settings.AnimationsEnabled);
        Refresh();

        _window.Left = -10000; // mostrar fuera de pantalla, medir y recolocar
        _window.Show();
        _window.UpdateLayout();
        Position(_window);
        _window.AnimateIn(fromBottom: IsBottom(_settings.OsdPosition));
        RestartTimer();
    }

    private void Refresh()
    {
        _window!.Populate(
            _audio.GetDevicesWithVolume(),
            onSelect: id =>
            {
                _audio.SetDefaultDevice(id);
                Refresh(); // refleja el nuevo predeterminado
                Reposition();
                RestartTimer();
            },
            onVolumeChanged: (id, volume) =>
            {
                _audio.SetDeviceVolume(id, volume);
                RestartTimer();
            });
    }

    private void RestartTimer()
    {
        // Más generoso que el OSD pasivo: es un widget interactivo
        _hideTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(3000, _settings.OsdDurationMs * 2));
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void Reposition()
    {
        if (_window is { IsVisible: true })
        {
            _window.UpdateLayout();
            Position(_window);
        }
    }

    private void Position(MixerWindow window)
    {
        var area = SystemParameters.WorkArea;
        double w = window.ActualWidth, h = window.ActualHeight;
        const double margin = 8;

        window.Left = _settings.OsdPosition switch
        {
            OsdPosition.TopLeft or OsdPosition.BottomLeft => area.Left + margin,
            OsdPosition.TopRight or OsdPosition.BottomRight => area.Right - w - margin,
            _ => area.Left + (area.Width - w) / 2,
        };
        window.Top = IsBottom(_settings.OsdPosition)
            ? area.Bottom - h - margin
            : area.Top + margin;
    }

    private static bool IsBottom(OsdPosition p) =>
        p is OsdPosition.BottomLeft or OsdPosition.BottomCenter or OsdPosition.BottomRight;

    public void Dispose()
    {
        _hideTimer.Stop();
        _window?.Close();
    }
}
