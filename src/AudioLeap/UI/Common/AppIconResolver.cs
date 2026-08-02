// AppIconResolver.cs — Extrae el icono de una aplicación desde su ejecutable y lo
// cachea por ruta. Devuelve un ImageSource listo para WPF, o null si no se puede
// (proceso elevado/sistema): en ese caso la UI usa un glifo de reserva.
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace AudioLeap.UI.Common;

public static class AppIconResolver
{
    // Caché por ruta de ejecutable (los iconos no cambian mientras corre la app).
    private static readonly Dictionary<string, BitmapSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Icono de la aplicación en <paramref name="executablePath"/>, o null si no se
    /// puede extraer. El resultado (incluido el fallo) se cachea para no repetir el trabajo.</summary>
    public static BitmapSource? FromExecutable(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return null;
        if (Cache.TryGetValue(executablePath, out var cached)) return cached;

        BitmapSource? result = null;
        try
        {
            if (File.Exists(executablePath))
            {
                using var icon = Icon.ExtractAssociatedIcon(executablePath);
                if (icon is not null)
                {
                    result = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    result.Freeze(); // inmutable → compartible entre hilos y más rápido
                }
            }
        }
        catch { result = null; } // sin acceso al ejecutable: la UI pondrá un glifo

        Cache[executablePath] = result;
        return result;
    }
}
