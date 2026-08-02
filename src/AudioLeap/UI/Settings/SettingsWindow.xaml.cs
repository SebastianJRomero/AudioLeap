// SettingsWindow.xaml.cs — Edita una copia de AppSettings; solo persiste al pulsar Guardar.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AudioLeap.Core.Audio;
using AudioLeap.Core.Hotkeys;
using AudioLeap.Core.Localization;
using AudioLeap.Core.Settings;
using AudioLeap.UI.Common;

namespace AudioLeap.UI.Settings;

public partial class SettingsWindow : Window
{
    private readonly SettingsManager _settingsManager;
    private readonly AudioService _audio;

    // Referencias a los controles por dispositivo, para releerlos al guardar.
    private readonly List<DeviceRow> _deviceRows = new();

    /// <summary>Controles editables de un dispositivo en la sección "Dispositivos".</summary>
    private sealed record DeviceRow(string Id, CheckBox Favorite, TextBox NameBox, ComboBox IconCombo);

    // Los combos guardan la CLAVE de localización; la etiqueta se resuelve con Loc.T
    // al construir la ventana (se reabre tras guardar, así que no hace falta refrescar).
    private static readonly (OsdPosition Value, string Key)[] Positions =
    {
        (OsdPosition.TopLeft, "PosTopLeft"),
        (OsdPosition.TopCenter, "PosTopCenter"),
        (OsdPosition.TopRight, "PosTopRight"),
        (OsdPosition.BottomLeft, "PosBottomLeft"),
        (OsdPosition.BottomCenter, "PosBottomCenter"),
        (OsdPosition.BottomRight, "PosBottomRight"),
    };

    private static readonly (AppTheme Value, string Key)[] Themes =
    {
        (AppTheme.Auto, "ThemeAuto"),
        (AppTheme.Light, "ThemeLight"),
        (AppTheme.Dark, "ThemeDark"),
    };

    // Cada idioma se muestra en su propio nombre, sin traducir.
    private static readonly (AppLanguage Value, string Label)[] Languages =
    {
        (AppLanguage.Spanish, "Español"),
        (AppLanguage.English, "English"),
    };

    public SettingsWindow(SettingsManager settingsManager, AudioService audio)
    {
        InitializeComponent();
        _settingsManager = settingsManager;
        _audio = audio;

        foreach (var (_, key) in Positions) PositionCombo.Items.Add(Loc.T(key));
        foreach (var (_, key) in Themes) ThemeCombo.Items.Add(Loc.T(key));
        foreach (var (_, label) in Languages) LanguageCombo.Items.Add(label);

        LoadFrom(_settingsManager.Current);
        LoadDevices(_settingsManager.Current);
    }

    /// <summary>Por cada dispositivo: check de favorito con su nombre REAL (arriba) y, debajo,
    /// selector de icono + campo de nombre personalizado. El favorito controla su visibilidad
    /// en el mezclador y en la rotación siguiente/anterior.</summary>
    private void LoadDevices(AppSettings s)
    {
        DevicesPanel.Children.Clear();
        _deviceRows.Clear();

        var devices = _audio.GetActiveDevices();
        for (int i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            s.DeviceCustomizations.TryGetValue(device.Id, out var custom);

            var block = new StackPanel { Margin = new Thickness(0, i == 0 ? 0 : 10, 0, 0) };

            // Fila superior: favorito + nombre real de Windows.
            var favorite = new CheckBox
            {
                Content = device.Name, // siempre el nombre real, solo visible aquí
                IsChecked = !s.ExcludedDeviceIds.Contains(device.Id),
                FontWeight = FontWeights.SemiBold,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            block.Children.Add(favorite);

            // Fila inferior: icono + nombre personalizado (indentada bajo el nombre real).
            var editors = new Grid { Margin = new Thickness(24, 6, 0, 0) };
            editors.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            editors.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconCombo = BuildIconCombo(custom?.IconKey);
            Grid.SetColumn(iconCombo, 0);
            editors.Children.Add(iconCombo);

            var nameBox = new TextBox
            {
                Text = custom?.Name ?? "",
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(6, 4, 6, 4),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = Loc.T("CustomNamePlaceholder"),
            };
            Grid.SetColumn(nameBox, 1);
            editors.Children.Add(nameBox);

            block.Children.Add(editors);
            DevicesPanel.Children.Add(block);

            _deviceRows.Add(new DeviceRow(device.Id, favorite, nameBox, iconCombo));
        }

        if (_deviceRows.Count == 0)
            DevicesPanel.Children.Add(new TextBlock
            { Text = Loc.T("NoActiveDevices"), Opacity = 0.7 });
    }

    /// <summary>Combo de iconos: primero "Automático", luego cada icono del catálogo mostrando
    /// su glifo y su etiqueta. Deja seleccionado el icono guardado (o Automático).</summary>
    private static ComboBox BuildIconCombo(string? selectedKey)
    {
        var combo = new ComboBox { Width = 150, VerticalContentAlignment = VerticalAlignment.Center };

        combo.Items.Add(BuildIconItem(glyph: null, label: Loc.T("IconAuto")));
        foreach (var option in DeviceIcons.Options)
            combo.Items.Add(BuildIconItem(option.Glyph, Loc.T(option.LabelKey)));

        int index = 0; // Automático por defecto
        if (selectedKey is not null && selectedKey != DeviceIcons.AutoKey)
        {
            int found = -1;
            for (int i = 0; i < DeviceIcons.Options.Count; i++)
                if (DeviceIcons.Options[i].Key == selectedKey) { found = i; break; }
            if (found >= 0) index = found + 1; // +1 por la entrada "Automático"
        }
        combo.SelectedIndex = index;
        return combo;
    }

    private static ComboBoxItem BuildIconItem(string? glyph, string label)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        if (glyph is not null)
        {
            row.Children.Add(new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 15,
                Width = 22,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        return new ComboBoxItem { Content = row };
    }

    private void LoadFrom(AppSettings s)
    {
        HkVolumeUp.Text = s.HotkeyVolumeUp;
        HkVolumeDown.Text = s.HotkeyVolumeDown;
        HkMute.Text = s.HotkeyMute;
        HkNextDevice.Text = s.HotkeyNextDevice;
        HkPrevDevice.Text = s.HotkeyPreviousDevice;
        HkShowMixer.Text = s.HotkeyShowMixer;
        HkShowAppMixer.Text = s.HotkeyShowAppMixer;

        PositionCombo.SelectedIndex = Array.FindIndex(Positions, p => p.Value == s.OsdPosition);
        DurationSlider.Value = s.OsdDurationMs;
        ScaleSlider.Value = s.OsdScale;
        AnimationsCheck.IsChecked = s.AnimationsEnabled;

        StepSlider.Value = s.VolumeStepPercent;
        ThemeCombo.SelectedIndex = Array.FindIndex(Themes, t => t.Value == s.Theme);
        LanguageCombo.SelectedIndex = Array.FindIndex(Languages, l => l.Value == s.Language);
        StartupCheck.IsChecked = s.RunAtStartup;
        CommsRoleCheck.IsChecked = s.AlsoSetCommunicationsRole;
    }

    /// <summary>Captura la combinación pulsada en el campo. Requiere al menos un modificador.
    /// Supr/Retroceso borran el atajo (vacío = deshabilitado).</summary>
    private void OnHotkeyBoxKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var box = (TextBox)sender;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Borrar el atajo para evitar conflictos con otras aplicaciones
        if (key is Key.Delete or Key.Back && Keyboard.Modifiers == ModifierKeys.None)
        {
            box.Text = "";
            return;
        }

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.None)
            return; // solo modificadores aún: esperar la tecla final

        var def = new HotkeyDefinition(Keyboard.Modifiers, key);
        if (def.Modifiers == ModifierKeys.None)
            return; // exigir al menos un modificador para atajos globales

        box.Text = def.ToString();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        // Validar atajos antes de guardar (vacío = deshabilitado, permitido)
        foreach (var (text, name) in new[]
        {
            (HkVolumeUp.Text, Loc.T("VolumeUp")), (HkVolumeDown.Text, Loc.T("VolumeDown")),
            (HkMute.Text, Loc.T("Mute")), (HkNextDevice.Text, Loc.T("NextDevice")),
            (HkPrevDevice.Text, Loc.T("PrevDevice")), (HkShowMixer.Text, Loc.T("ShowMixer")),
            (HkShowAppMixer.Text, Loc.T("ShowAppMixer")),
        })
        {
            if (!string.IsNullOrWhiteSpace(text) && !HotkeyDefinition.TryParse(text, out _))
            {
                MessageBox.Show(this, string.Format(Loc.T("InvalidHotkey"), name), "AudioLeap",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        // Favoritos y personalizaciones: conservar lo de dispositivos no conectados ahora
        // mismo y actualizar lo de los listados según sus controles.
        var excluded = new List<string>(_settingsManager.Current.ExcludedDeviceIds);
        var customizations = new Dictionary<string, DeviceCustomization>(_settingsManager.Current.DeviceCustomizations);
        foreach (var row in _deviceRows)
        {
            if (row.Favorite.IsChecked == true) excluded.Remove(row.Id);
            else if (!excluded.Contains(row.Id)) excluded.Add(row.Id);

            string customName = row.NameBox.Text.Trim();
            string? iconKey = row.IconCombo.SelectedIndex > 0
                ? DeviceIcons.Options[row.IconCombo.SelectedIndex - 1].Key // -1 por la entrada "Automático"
                : null;

            // Sin nombre ni icono personalizados → no guardar entrada (usa los valores reales).
            if (string.IsNullOrEmpty(customName) && iconKey is null)
                customizations.Remove(row.Id);
            else
                customizations[row.Id] = new DeviceCustomization { Name = customName, IconKey = iconKey };
        }

        var s = new AppSettings
        {
            HotkeyVolumeUp = HkVolumeUp.Text,
            HotkeyVolumeDown = HkVolumeDown.Text,
            HotkeyMute = HkMute.Text,
            HotkeyNextDevice = HkNextDevice.Text,
            HotkeyPreviousDevice = HkPrevDevice.Text,
            HotkeyShowMixer = HkShowMixer.Text,
            HotkeyShowAppMixer = HkShowAppMixer.Text,
            OsdPosition = Positions[Math.Max(0, PositionCombo.SelectedIndex)].Value,
            OsdDurationMs = (int)DurationSlider.Value,
            OsdScale = Math.Round(ScaleSlider.Value, 2),
            AnimationsEnabled = AnimationsCheck.IsChecked == true,
            VolumeStepPercent = (int)StepSlider.Value,
            Theme = Themes[Math.Max(0, ThemeCombo.SelectedIndex)].Value,
            Language = Languages[Math.Max(0, LanguageCombo.SelectedIndex)].Value,
            RunAtStartup = StartupCheck.IsChecked == true,
            AlsoSetCommunicationsRole = CommsRoleCheck.IsChecked == true,
            ExcludedDeviceIds = excluded,
            DeviceCustomizations = customizations,
        };

        _settingsManager.Save(s); // notifica a todos los módulos
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}
