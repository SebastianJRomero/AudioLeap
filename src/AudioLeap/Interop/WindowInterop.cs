// WindowInterop.cs — P/Invoke de ventana para el OSD:
// topmost real, no robar foco, invisible en Alt-Tab, y acrílico/blur opcional.
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AudioLeap.Interop;

public static class WindowInterop
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x2, SWP_NOSIZE = 0x1, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    /// <summary>OSD: nunca roba el foco, no aparece en Alt-Tab, y los clics lo atraviesan.</summary>
    public static void MakeOsd(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE,
            ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_TRANSPARENT);
    }

    /// <summary>Mezclador: topmost y sin robar foco de teclado, pero SÍ recibe clics (sin WS_EX_TRANSPARENT).</summary>
    public static void MakeInteractiveOsd(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST);
    }

    /// <summary>Reafirma HWND_TOPMOST sin activar; llamar en cada aparición del OSD.</summary>
    public static void BringToTopMost(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    // Nota: se descartó el blur acrílico vía SetWindowCompositionAttribute.
    // En varios equipos esa API pinta un rectángulo blanco/opaco detrás de las
    // ventanas por capas de WPF (AllowsTransparency). El fondo semitransparente
    // del OSD logra la estética Fluent sin ese artefacto.
}
