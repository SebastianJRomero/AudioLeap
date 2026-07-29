// DeviceIcons.cs — Catálogo de iconos predeterminados que el usuario puede asignar
// a un dispositivo de salida. Glifos de Segoe Fluent Icons / Segoe MDL2 Assets,
// definidos por código numérico para mantener el archivo fuente en ASCII puro.
using AudioLeap.Core.Audio;

namespace AudioLeap.UI.Common;

/// <summary>Una opción de icono: su clave persistida, el glifo a pintar y la clave de
/// etiqueta localizada que se muestra en el selector.</summary>
public sealed record DeviceIconOption(string Key, string Glyph, string LabelKey);

public static class DeviceIcons
{
    /// <summary>Clave especial: dejar que el icono lo decida el tipo de dispositivo.</summary>
    public const string AutoKey = "auto";

    /// <summary>Opciones ofrecidas en el selector (además de "Automático"). Glifos elegidos
    /// entre los disponibles tanto en Segoe MDL2 Assets (Win10) como en Segoe Fluent Icons (Win11).</summary>
    public static readonly IReadOnlyList<DeviceIconOption> Options = new[]
    {
        new DeviceIconOption("speakers",   char.ConvertFromUtf32(0xE7F5), "IconSpeakers"),
        new DeviceIconOption("headphones", char.ConvertFromUtf32(0xE7F6), "IconHeadphones"),
        new DeviceIconOption("headset",    char.ConvertFromUtf32(0xE95B), "IconHeadset"),
        new DeviceIconOption("tv",         char.ConvertFromUtf32(0xE7F4), "IconTv"),
        new DeviceIconOption("bluetooth",  char.ConvertFromUtf32(0xE702), "IconBluetooth"),
        new DeviceIconOption("game",       char.ConvertFromUtf32(0xE7FC), "IconGame"),
        new DeviceIconOption("phone",      char.ConvertFromUtf32(0xE717), "IconPhone"),
        new DeviceIconOption("music",      char.ConvertFromUtf32(0xEC4F), "IconMusic"),
        new DeviceIconOption("generic",    char.ConvertFromUtf32(0xE772), "IconGeneric"),
    };

    private static readonly Dictionary<string, string> GlyphByKey =
        Options.ToDictionary(o => o.Key, o => o.Glyph);

    /// <summary>Glifo para un dispositivo: usa la clave personalizada si es válida;
    /// si no (nula, "auto" o desconocida), cae al icono automático según el tipo.</summary>
    public static string GlyphFor(string? iconKey, AudioFormFactor fallback) =>
        iconKey is not null && GlyphByKey.TryGetValue(iconKey, out var glyph)
            ? glyph
            : Glyphs.For(fallback);
}
