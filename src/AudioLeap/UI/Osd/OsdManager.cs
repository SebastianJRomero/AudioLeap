// OsdManager.cs — Orquesta el OSD: una única ventana reutilizada, temporizador
// de auto-ocultado y posicionamiento según configuración. La ventana solo existe
// mientras se usa; el temporizador se detiene al ocultarse (CPU 0 en reposo).
using System.Windows;
using System.Windows.Threading;
using AudioLeap.Core.Audio;
using AudioLeap.Core.Settings;

namespace AudioLeap.UI.Osd;

public sealed class OsdManager : IDisposable
{
    private readonly DispatcherTimer _hideTimer;
    private OsdWindow? _window;
    private AppSettings _settings;

    public OsdManager(AppSettings settings)
    {
        _settings = settings;
        _hideTimer = new DispatcherTimer();
        _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); _window?.AnimateOutAndHide(); };
    }

    public void UpdateSettings(AppSettings settings) => _settings = settings;

    public void ShowVolume(AudioState state) => Show(state, deviceChanged: false);
    public void ShowDevice(AudioState state) => Show(state, deviceChanged: true);

    private void Show(AudioState state, bool deviceChanged)
    {
        _window ??= new OsdWindow();

        bool wasVisible = _window.IsVisible;
        _window.Render(state, deviceChanged, _settings);

        if (!wasVisible)
        {
            // Mostrar fuera de pantalla, medir (SizeToContent) y recolocar:
            // evita cualquier parpadeo en (0,0) en el primer frame.
            _window.Left = -10000;
            _window.Show();
            _window.UpdateLayout();
            Position(_window);
            _window.AnimateIn(fromBottom: IsBottom(_settings.OsdPosition));
        }
        else
        {
            Position(_window); // por si cambió la escala o la posición configurada
        }

        _hideTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(_settings.OsdDurationMs, 500, 10000));
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void Position(OsdWindow window)
    {
        var area = SystemParameters.WorkArea; // respeta la barra de tareas
        double w = window.ActualWidth, h = window.ActualHeight;
        const double margin = 8; // el resto del margen visual viene de la sombra interna (24px)

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
