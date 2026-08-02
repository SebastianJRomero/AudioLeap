// AppMixerWindow.xaml.cs — Panel de volumen por aplicación. Misma mecánica que el
// mezclador de dispositivos: no roba el foco del teclado (WS_EX_NOACTIVATE) pero
// acepta el ratón. Cada fila controla una aplicación; el clic en el icono silencia.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using AudioLeap.Core.Audio;
using AudioLeap.Core.Localization;
using AudioLeap.Interop;
using AudioLeap.UI.Common;

namespace AudioLeap.UI.AppMixer;

public partial class AppMixerWindow : Window
{
    private static readonly Duration ShowDuration = new(TimeSpan.FromMilliseconds(340));
    private static readonly Duration HideDuration = new(TimeSpan.FromMilliseconds(180));
    private static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };
    private static readonly IEasingFunction EaseIn = new CubicEase { EasingMode = EasingMode.EaseIn };
    private static readonly IEasingFunction Overshoot = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.55 };

    // Glifo genérico (AllApps) para aplicaciones sin icono extraíble.
    private static readonly string GlyphApp = char.ConvertFromUtf32(0xE71D);

    private bool _animationsEnabled = true;

    public AppMixerWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowInterop.MakeInteractiveOsd(this);
    }

    public void Configure(double scale, bool animationsEnabled)
    {
        _animationsEnabled = animationsEnabled;
        ScaleTransform.ScaleX = ScaleTransform.ScaleY = scale;
    }

    /// <summary>Reconstruye las filas a partir de las sesiones actuales.</summary>
    public void Populate(IReadOnlyList<AppAudioSession> sessions,
        Action<string, int> onVolumeChanged, Action<string> onToggleMute)
    {
        SessionList.Children.Clear();

        if (sessions.Count == 0)
        {
            SessionList.Children.Add(new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
                FontSize = 13,
                Margin = new Thickness(12, 6, 12, 8),
                Opacity = 0.7,
                Foreground = (Brush)FindResource("OsdForegroundBrush"),
                Text = Loc.T("AppMixerEmpty"),
            });
            return;
        }

        int index = 0;
        foreach (var session in sessions)
        {
            var row = BuildRow(session, onVolumeChanged, onToggleMute);
            SessionList.Children.Add(row);
            if (_animationsEnabled) AnimateRowEntrance(row, index++);
        }
    }

    private UIElement BuildRow(AppAudioSession session,
        Action<string, int> onVolumeChanged, Action<string> onToggleMute)
    {
        var accent = (Brush)FindResource("AccentBrush");
        var foreground = (Brush)FindResource("OsdForegroundBrush");
        var secondary = (Brush)FindResource("OsdSecondaryBrush");

        var grid = new Grid { Margin = new Thickness(10, 6, 10, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Icono clicable: silencia/reactiva la aplicación.
        var iconButton = new Button
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0),
            Content = BuildIconVisual(session, secondary),
            ToolTip = Loc.T("Mute"),
        };
        iconButton.Template = TransparentButtonTemplate();
        string id = session.Id;
        iconButton.Click += (_, _) => onToggleMute(id);
        Grid.SetColumn(iconButton, 0);
        grid.Children.Add(iconButton);

        var column = new StackPanel();
        Grid.SetColumn(column, 1);
        grid.Children.Add(column);

        // Nombre de la aplicación.
        column.Children.Add(new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 13,
            Foreground = foreground,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Text = DisplayNameFor(session),
        });

        // Slider + porcentaje.
        var sliderRow = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var percent = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 12,
            MinWidth = 30,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = secondary,
            Text = session.VolumePercent.ToString(),
        };
        Grid.SetColumn(percent, 1);

        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = session.VolumePercent,
            IsMoveToPointEnabled = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Style = (Style)FindResource("FlyoutSlider"),
        };
        // Hook DESPUÉS de fijar Value: no dispara con el valor inicial.
        slider.ValueChanged += (_, e) =>
        {
            int v = (int)Math.Round(e.NewValue);
            percent.Text = v.ToString();
            onVolumeChanged(id, v);
        };
        Grid.SetColumn(slider, 0);
        sliderRow.Children.Add(slider);
        sliderRow.Children.Add(percent);
        column.Children.Add(sliderRow);

        return grid;
    }

    /// <summary>Icono a mostrar: si está silenciada, el glifo de mute; si no, el icono real
    /// del ejecutable, y como reserva un glifo genérico de aplicación.</summary>
    private UIElement BuildIconVisual(AppAudioSession session, Brush glyphBrush)
    {
        if (!session.IsMuted)
        {
            BitmapSource? icon = AppIconResolver.FromExecutable(session.ExecutablePath);
            if (icon is not null)
                return new Image { Source = icon, Width = 24, Height = 24, VerticalAlignment = VerticalAlignment.Center };
        }

        return new TextBlock
        {
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 20,
            Width = 24,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = glyphBrush,
            Text = session.IsMuted ? Glyphs.Mute : GlyphApp,
        };
    }

    private string DisplayNameFor(AppAudioSession session) =>
        session.IsSystemSounds ? Loc.T("SystemSounds") : session.DisplayName;

    /// <summary>Plantilla mínima de botón sin chrome (solo el contenido, con feedback de opacidad).</summary>
    private static ControlTemplate TransparentButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        template.VisualTree = presenter;

        var trigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        trigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.7));
        template.Triggers.Add(trigger);
        return template;
    }

    private static void AnimateRowEntrance(UIElement row, int index)
    {
        var delay = TimeSpan.FromMilliseconds(70 + index * 45);
        var translate = new TranslateTransform(0, 16);
        row.RenderTransform = translate;
        row.Opacity = 0;

        var fade = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(240)))
        { BeginTime = delay, EasingFunction = EaseOut };
        var slide = new DoubleAnimation(16, 0, new Duration(TimeSpan.FromMilliseconds(300)))
        { BeginTime = delay, EasingFunction = Overshoot };

        row.BeginAnimation(OpacityProperty, fade);
        translate.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    /// <summary>Entrada: fundido + deslizamiento con rebote + escala "pop".</summary>
    public void AnimateIn(bool fromBottom)
    {
        WindowInterop.BringToTopMost(this);
        if (!_animationsEnabled)
        {
            Opacity = 1; SlideTransform.Y = 0;
            PopScale.ScaleX = PopScale.ScaleY = 1;
            return;
        }

        Opacity = 0;
        double fromY = fromBottom ? 42 : -42;

        BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200))) { EasingFunction = EaseOut });
        SlideTransform.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(fromY, 0, ShowDuration) { EasingFunction = Overshoot });
        var pop = new DoubleAnimation(0.82, 1, ShowDuration) { EasingFunction = Overshoot };
        PopScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        PopScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
    }

    /// <summary>Salida: fundido + encogimiento + deslizamiento.</summary>
    public void AnimateOutAndHide()
    {
        if (!_animationsEnabled) { Hide(); return; }

        var fade = new DoubleAnimation(0, HideDuration) { EasingFunction = EaseIn };
        fade.Completed += (_, _) => { Hide(); BeginAnimation(OpacityProperty, null); };
        BeginAnimation(OpacityProperty, fade);

        var shrink = new DoubleAnimation(0.88, HideDuration) { EasingFunction = EaseIn };
        PopScale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
        PopScale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
        SlideTransform.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(18, HideDuration) { EasingFunction = EaseIn });
    }
}
