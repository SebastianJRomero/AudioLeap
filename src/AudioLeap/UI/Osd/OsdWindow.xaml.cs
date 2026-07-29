// OsdWindow.xaml.cs — Ventana OSD. Sin foco, sin Alt-Tab, clics la atraviesan.
// Todas las animaciones usan el reloj de composición de WPF (render por hardware, 60 FPS).
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AudioLeap.Core.Audio;
using AudioLeap.Core.Settings;
using AudioLeap.Interop;
using AudioLeap.UI.Common;

namespace AudioLeap.UI.Osd;

public partial class OsdWindow : Window
{
    private static readonly Duration BarDuration = new(TimeSpan.FromMilliseconds(120));
    private static readonly Duration ShowDuration = new(TimeSpan.FromMilliseconds(200));
    private static readonly Duration HideDuration = new(TimeSpan.FromMilliseconds(160));
    private static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };

    // Glifo de silencio (Segoe Fluent Icons / Segoe MDL2 Assets). El icono del
    // dispositivo lo resuelve DeviceIcons según la personalización o el tipo.
    private static readonly string GlyphMute = char.ConvertFromUtf32(0xE74F);

    /// <summary>Valor animable de la barra (0–100). El callback pinta barra y porcentaje juntos → sin parpadeos.</summary>
    public static readonly DependencyProperty DisplayVolumeProperty = DependencyProperty.Register(
        nameof(DisplayVolume), typeof(double), typeof(OsdWindow),
        new PropertyMetadata(0d, (d, _) => ((OsdWindow)d).RenderVolume()));

    public double DisplayVolume
    {
        get => (double)GetValue(DisplayVolumeProperty);
        set => SetValue(DisplayVolumeProperty, value);
    }

    private bool _animationsEnabled = true;
    private string? _lastDeviceId;

    public OsdWindow()
    {
        InitializeComponent();
        // Nota: no se usa blur acrílico (SetWindowCompositionAttribute). En varios
        // sistemas dibuja un rectángulo blanco/opaco detrás de ventanas por capas.
        // El fondo semitransparente del Border ya da la estética Fluent sin artefactos.
        SourceInitialized += (_, _) => WindowInterop.MakeOsd(this);
        BarTrack.SizeChanged += (_, _) => RenderVolume();
    }

    /// <summary>Actualiza el contenido y lanza las animaciones pertinentes.</summary>
    public void Render(AudioState state, bool deviceChanged, AppSettings settings)
    {
        _animationsEnabled = settings.AnimationsEnabled;
        ScaleTransform.ScaleX = ScaleTransform.ScaleY = settings.OsdScale;

        DeviceNameText.Text = state.Device.DisplayName.Trim();
        DeviceIcon.Text = state.IsMuted
            ? GlyphMute
            : DeviceIcons.GlyphFor(state.Device.IconKey, state.Device.FormFactor);
        MutedBadge.Visibility = state.IsMuted ? Visibility.Visible : Visibility.Collapsed;
        BarFill.Background = state.IsMuted
            ? (Brush)FindResource("OsdSecondaryBrush")
            : (Brush)FindResource("AccentBrush");

        // Transición sutil de icono+nombre al cambiar de dispositivo
        if (deviceChanged && _lastDeviceId is not null && _lastDeviceId != state.Device.Id && _animationsEnabled)
        {
            var fade = new DoubleAnimation(0.35, 1, ShowDuration) { EasingFunction = EaseOut };
            DeviceIcon.BeginAnimation(OpacityProperty, fade);
            DeviceNameText.BeginAnimation(OpacityProperty, fade);
        }
        _lastDeviceId = state.Device.Id;

        // Barra y porcentaje
        if (_animationsEnabled && IsVisible)
        {
            var anim = new DoubleAnimation(state.VolumePercent, BarDuration) { EasingFunction = EaseOut };
            BeginAnimation(DisplayVolumeProperty, anim);
        }
        else
        {
            BeginAnimation(DisplayVolumeProperty, null);
            DisplayVolume = state.VolumePercent;
        }
    }

    /// <summary>Entrada: fundido + deslizamiento con ligero rebote + escala "pop".</summary>
    public void AnimateIn(bool fromBottom)
    {
        WindowInterop.BringToTopMost(this);
        if (!_animationsEnabled)
        {
            Opacity = 1; SlideTransform.Y = 0;
            PopScale.ScaleX = PopScale.ScaleY = 1;
            return;
        }

        var overshoot = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.45 };
        var duration = new Duration(TimeSpan.FromMilliseconds(280));

        Opacity = 0;
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, ShowDuration) { EasingFunction = EaseOut });
        SlideTransform.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(fromBottom ? 28 : -28, 0, duration) { EasingFunction = overshoot });
        var pop = new DoubleAnimation(0.9, 1, duration) { EasingFunction = overshoot };
        PopScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        PopScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
    }

    /// <summary>Fundido de salida; oculta la ventana al terminar.</summary>
    public void AnimateOutAndHide()
    {
        if (!_animationsEnabled) { Hide(); return; }
        var fade = new DoubleAnimation(0, HideDuration) { EasingFunction = EaseOut };
        fade.Completed += (_, _) => { Hide(); BeginAnimation(OpacityProperty, null); };
        BeginAnimation(OpacityProperty, fade);
    }

    private void RenderVolume()
    {
        double fraction = Math.Clamp(DisplayVolume / 100.0, 0, 1);
        BarFill.Width = BarTrack.ActualWidth * fraction;
        PercentText.Text = ((int)Math.Round(DisplayVolume)).ToString();
    }
}
