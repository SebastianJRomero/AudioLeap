// SettingsManager.cs — Persistencia en %APPDATA%\AudioLeap\settings.json.
// Los módulos se suscriben a SettingsChanged; nadie relee el disco.
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioLeap.Core.Settings;

public sealed class SettingsManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    public AppSettings Current { get; private set; } = new();

    /// <summary>Se dispara tras guardar cambios para que cada módulo se reconfigure.</summary>
    public event Action<AppSettings>? SettingsChanged;

    public SettingsManager()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AudioLeap");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_path))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions) ?? new();
        }
        catch { Current = new AppSettings(); } // JSON corrupto → valores por defecto
    }

    /// <summary>Aplica y persiste una nueva configuración, notificando a los suscriptores.</summary>
    public void Save(AppSettings settings)
    {
        Current = settings;
        try { File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOptions)); }
        catch { /* disco lleno / sin permisos: se mantiene en memoria */ }
        SettingsChanged?.Invoke(settings);
    }
}
