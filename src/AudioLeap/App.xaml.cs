// App.xaml.cs — Composition root: crea los módulos y los conecta entre sí.
// Es el ÚNICO lugar donde los módulos se conocen; cada uno es independiente.
using System.Windows;
using System.Windows.Threading;
using AudioLeap.Core.Audio;
using AudioLeap.Core.Hotkeys;
using AudioLeap.Core.Localization;
using AudioLeap.Core.Settings;
using AudioLeap.Core.Theme;
using AudioLeap.UI.Mixer;
using AudioLeap.UI.Osd;
using AudioLeap.UI.Settings;
using AudioLeap.UI.Tray;

namespace AudioLeap;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private SettingsManager _settings = null!;
    private AudioService _audio = null!;
    private HotkeyManager _hotkeys = null!;
    private OsdManager _osd = null!;
    private MixerManager _mixer = null!;
    private TrayManager _tray = null!;
    private ThemeManager _theme = null!;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Instancia única
        _singleInstanceMutex = new Mutex(true, @"Local\AudioLeap.SingleInstance", out bool isNew);
        if (!isNew) { Shutdown(); return; }

        base.OnStartup(e);

        // ── Crear módulos ────────────────────────────────────────────────
        _settings = new SettingsManager();
        _theme = new ThemeManager();
        _audio = new AudioService();
        _osd = new OsdManager(_settings.Current);
        _mixer = new MixerManager(_audio, _settings.Current);
        _hotkeys = new HotkeyManager();
        _tray = new TrayManager(_audio);

        // ── Conectar módulos ─────────────────────────────────────────────
        Loc.Apply(_settings.Current.Language);
        _theme.Apply(_settings.Current.Theme);
        _audio.AlsoSetCommunicationsRole = _settings.Current.AlsoSetCommunicationsRole;
        _audio.ExcludedDeviceIds = new HashSet<string>(_settings.Current.ExcludedDeviceIds);
        _audio.DeviceCustomizations = _settings.Current.DeviceCustomizations;

        _hotkeys.HotkeyPressed += OnHotkeyPressed;
        RegisterHotkeys(_settings.Current);

        _tray.DeviceSelected += id =>
        {
            _audio.SetDefaultDevice(id);
            if (_audio.GetState() is { } st) _osd.ShowDevice(st);
        };
        _tray.OpenSettingsRequested += OpenSettings;
        _tray.ExitRequested += Shutdown;

        // Cambio externo del predeterminado (p. ej. al conectar auriculares): mostrar OSD
        _audio.ExternalDefaultChanged += () => Dispatcher.BeginInvoke(() =>
        {
            if (_audio.GetState() is { } st) _osd.ShowDevice(st);
        });

        // Reconfiguración en caliente al guardar ajustes
        _settings.SettingsChanged += s =>
        {
            Loc.Apply(s.Language);
            _osd.UpdateSettings(s);
            _mixer.UpdateSettings(s);
            _theme.Apply(s.Theme);
            _audio.AlsoSetCommunicationsRole = s.AlsoSetCommunicationsRole;
            _audio.ExcludedDeviceIds = new HashSet<string>(s.ExcludedDeviceIds);
            _audio.DeviceCustomizations = s.DeviceCustomizations;
            RegisterHotkeys(s);
            try { StartupManager.SetEnabled(s.RunAtStartup); } catch { }
        };

        // Sincronizar el estado real de inicio con Windows con lo configurado
        try
        {
            if (_settings.Current.RunAtStartup != StartupManager.IsEnabled())
                StartupManager.SetEnabled(_settings.Current.RunAtStartup);
        }
        catch { }
    }

    private void OnHotkeyPressed(HotkeyAction action)
    {
        if (action == HotkeyAction.ShowMixer)
        {
            _mixer.Toggle();
            return;
        }

        int step = Math.Max(1, _settings.Current.VolumeStepPercent);
        AudioState? state = action switch
        {
            HotkeyAction.VolumeUp => _audio.ChangeVolume(+step),
            HotkeyAction.VolumeDown => _audio.ChangeVolume(-step),
            HotkeyAction.ToggleMute => _audio.ToggleMute(),
            HotkeyAction.NextDevice => _audio.CycleDevice(+1),
            HotkeyAction.PreviousDevice => _audio.CycleDevice(-1),
            _ => null,
        };
        if (state is null) return;

        bool isDeviceAction = action is HotkeyAction.NextDevice or HotkeyAction.PreviousDevice;
        if (isDeviceAction) _osd.ShowDevice(state);
        else _osd.ShowVolume(state);
    }

    private void RegisterHotkeys(AppSettings s)
    {
        var map = new Dictionary<HotkeyAction, HotkeyDefinition>();
        void Add(HotkeyAction action, string text)
        {
            if (HotkeyDefinition.TryParse(text, out var def)) map[action] = def;
        }
        Add(HotkeyAction.VolumeUp, s.HotkeyVolumeUp);
        Add(HotkeyAction.VolumeDown, s.HotkeyVolumeDown);
        Add(HotkeyAction.ToggleMute, s.HotkeyMute);
        Add(HotkeyAction.NextDevice, s.HotkeyNextDevice);
        Add(HotkeyAction.PreviousDevice, s.HotkeyPreviousDevice);
        Add(HotkeyAction.ShowMixer, s.HotkeyShowMixer);

        var failed = _hotkeys.RegisterAll(map);
        if (failed.Count > 0)
            _tray.ShowBalloon("AudioLeap", Loc.T("HotkeysBusy"));
    }

    private void OpenSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(_settings, _audio);

        // Suspender los atajos globales mientras la configuración está abierta:
        // así se pueden capturar combinaciones sin disparar acciones ni chocar
        // con los registros existentes.
        _hotkeys.UnregisterAll();
        _settingsWindow.Closed += (_, _) => RegisterHotkeys(_settings.Current);

        _settingsWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _hotkeys?.Dispose();
        _osd?.Dispose();
        _mixer?.Dispose();
        _audio?.Dispose();
        _theme?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
