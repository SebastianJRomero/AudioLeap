// MixerWindow.xaml.cs — Widget interactivo: lista de dispositivos activos con
// slider de volumen por dispositivo y clic para elegir el predeterminado.
// No roba el foco del teclado (WS_EX_NOACTIVATE) pero sí acepta el ratón.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AudioLeap.Core.Audio;
using AudioLeap.Interop;
using AudioLeap.UI.Common;

namespace AudioLeap.UI.Mixer;

public partial class MixerWindow : Window
{
    private static readonly Duration ShowDuration = new(TimeSpan.FromMilliseconds(340));
    private static readonly Duration HideDuration = new(TimeSpan.FromMilliseconds(180));
    private static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };
    private static readonly IEasingFunction EaseIn = new CubicEase { EasingMode = EasingMode.EaseIn };
    // Rebote sutil al final del movimiento: la firma de las animaciones "llamativas"
    private static readonly IEasingFunction Overshoot = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.55 };

    private bool _animationsEnabled = true;

    public MixerWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowInterop.MakeInteractiveOsd(this);
    }

    public void Configure(double scale, bool animationsEnabled)
    {
        _animationsEnabled = animationsEnabled;
        ScaleTransform.ScaleX = ScaleTransform.ScaleY = scale;
    }

    /// <summary>Reconstruye las filas. Se llama al mostrar y tras seleccionar dispositivo.</summary>
    public void Populate(IReadOnlyList<DeviceVolume> devices,
        Action<string> onSelect, Action<string, int> onVolumeChanged)
    {
        DeviceList.Children.Clear();
        int index = 0;
        foreach (var item in devices)
        {
            var row = BuildRow(item, onSelect, onVolumeChanged);
            DeviceList.Children.Add(row);
            if (_animationsEnabled) AnimateRowEntrance((UIElement)row, index++);
        }
    }

    /// <summary>Entrada escalonada de filas: cada una aparece deslizándose con un pequeño retardo.</summary>
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

    private UIElement BuildRow(DeviceVolume item, Action<string> onSelect, Action<string, int> onVolumeChanged)
    {
        var device = item.Device;
        var accent = (Brush)FindResource("AccentBrush");
        var foreground = (Brush)FindResource("OsdForegroundBrush");
        var secondary = (Brush)FindResource("OsdSecondaryBrush");

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Icono (acento si es el predeterminado; mute si está silenciado)
        var icon = new TextBlock
        {
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 18,
            Margin = new Thickness(2, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = device.IsDefault ? accent : secondary,
            Text = item.IsMuted ? Glyphs.Mute : DeviceIcons.GlyphFor(device.IconKey, device.FormFactor),
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var column = new StackPanel();
        Grid.SetColumn(column, 1);
        grid.Children.Add(column);

        // Nombre + check del predeterminado
        var nameRow = new Grid();
        var name = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 13,
            FontWeight = device.IsDefault ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = foreground,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 24, 0),
            Text = device.DisplayName,
        };
        nameRow.Children.Add(name);
        if (device.IsDefault)
        {
            nameRow.Children.Add(new TextBlock
            {
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = accent,
                Text = Glyphs.Check,
            });
        }
        column.Children.Add(nameRow);

        // Slider + porcentaje
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
            Text = item.VolumePercent.ToString(),
        };
        Grid.SetColumn(percent, 1);

        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = item.VolumePercent,
            IsMoveToPointEnabled = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Style = (Style)FindResource("ModernSlider"),
        };
        // Hook DESPUÉS de fijar Value: no dispara el evento con el valor inicial.
        string id = device.Id;
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

        // Fila completa clicable para elegir el predeterminado.
        // El Slider captura el ratón al arrastrar, por lo que no dispara el Click del botón.
        var row = new Button { Style = (Style)FindResource("DeviceRowStyle"), Content = grid };
        row.Click += (_, _) => onSelect(id);
        return row;
    }

    /// <summary>Entrada llamativa: fundido + deslizamiento con rebote + escala "pop" (0.82 → 1).</summary>
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

    /// <summary>Salida: fundido + encogimiento + deslizamiento hacia el borde.</summary>
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
