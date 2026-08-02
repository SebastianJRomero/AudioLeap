// AppMixerManager.cs — Orquesta el mezclador de aplicaciones: alternar con el atajo,
// auto-ocultado por inactividad y posicionamiento igual que el OSD/mezclador. El
// temporizador solo corre mientras el panel está visible (CPU 0 en reposo).
using System.Windows;
using System.Windows.Threading;
using AudioLeap.Core.Audio;
using AudioLeap.Core.Settings;

namespace AudioLeap.UI.AppMixer;

public sealed class AppMixerManager : IDisposable
{
    private readonly AppAudioService _appAudio;
    private readonly DispatcherTimer _hideTimer;
    private AppMixerWindow? _window;
    private AppSettings _settings;

    public AppMixerManager(AppAudioService appAudio, AppSettings settings)
    {
        _appAudio = appAudio;
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
            _window = new AppMixerWindow();
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
            _appAudio.GetSessions(),
            onVolumeChanged: (id, volume) =>
            {
                _appAudio.SetSessionVolume(id, volume);
                RestartTimer();
            },
            onToggleMute: id =>
            {
                _appAudio.ToggleSessionMute(id);
                Refresh();       // refleja el nuevo estado de silencio (icono)
                Reposition();
                RestartTimer();
            });
    }

    private void RestartTimer()
    {
        // Panel interactivo: ventana más generosa que el OSD pasivo.
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

    private void Position(AppMixerWindow window)
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
