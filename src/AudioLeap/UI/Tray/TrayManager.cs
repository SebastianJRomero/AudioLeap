// TrayManager.cs — Icono en el área de notificación con menú contextual.
// Único uso de WinForms (NotifyIcon); no hay message pump adicional.
using System.Drawing;
using System.Windows.Forms;
using AudioLeap.Core.Audio;
using AudioLeap.Core.Localization;

namespace AudioLeap.UI.Tray;

public sealed class TrayManager : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly AudioService _audio;
    private Icon? _drawnIcon;

    public event Action? OpenSettingsRequested;
    public event Action? ExitRequested;
    /// <summary>El usuario eligió un dispositivo concreto en el menú.</summary>
    public event Action<string>? DeviceSelected;

    public TrayManager(AudioService audio)
    {
        _audio = audio;
        _icon = new NotifyIcon
        {
            Icon = CreateSpeakerIcon(),
            Text = "AudioLeap",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip(),
        };
        _icon.ContextMenuStrip.Opening += (_, _) => RebuildMenu();
        _icon.DoubleClick += (_, _) => OpenSettingsRequested?.Invoke();
    }

    public void ShowBalloon(string title, string text) =>
        _icon.ShowBalloonTip(3000, title, text, ToolTipIcon.Info);

    /// <summary>Menú regenerado al abrirse: lista de dispositivos siempre al día sin polling.</summary>
    private void RebuildMenu()
    {
        var menu = _icon.ContextMenuStrip!;
        menu.Items.Clear();

        // Textos resueltos aquí y no en el constructor: el menú se regenera al abrirse,
        // por lo que un cambio de idioma se refleja sin reiniciar.
        var header = new ToolStripMenuItem(Loc.T("OutputDevices")) { Enabled = false };
        menu.Items.Add(header);

        foreach (var device in _audio.GetActiveDevices())
        {
            var item = new ToolStripMenuItem(device.DisplayName) { Checked = device.IsDefault };
            string id = device.Id;
            item.Click += (_, _) => DeviceSelected?.Invoke(id);
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripSeparator());

        var settings = new ToolStripMenuItem(Loc.T("TraySettings"));
        settings.Click += (_, _) => OpenSettingsRequested?.Invoke();
        menu.Items.Add(settings);

        menu.Items.Add(new ToolStripSeparator());

        var exit = new ToolStripMenuItem(Loc.T("TrayExit"));
        exit.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(exit);
    }

    /// <summary>Dibuja un icono de altavoz simple en runtime (sin recursos binarios en el repo).</summary>
    private Icon CreateSpeakerIcon()
    {
        var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            bool dark = Core.Theme.ThemeManager.IsSystemDark();
            using var brush = new SolidBrush(dark ? Color.White : Color.Black);
            using var pen = new Pen(dark ? Color.White : Color.Black, 2.4f);

            // Cuerpo del altavoz
            g.FillPolygon(brush, new[]
            {
                new PointF(5, 12), new PointF(12, 12), new PointF(19, 5),
                new PointF(19, 27), new PointF(12, 20), new PointF(5, 20),
            });
            // Ondas de sonido
            g.DrawArc(pen, 20, 9, 8, 14, -55, 110);
            g.DrawArc(pen, 23, 6, 12, 20, -50, 100);
        }
        _drawnIcon = Icon.FromHandle(bmp.GetHicon());
        bmp.Dispose();
        return _drawnIcon;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _drawnIcon?.Dispose();
    }
}
