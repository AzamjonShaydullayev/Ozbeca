using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace Ozbeca.App;

/// <summary>Windows'ning yorug'/qorong'i mavzusiga moslash (Bg, Fg, Muted, Field, Line resurslari).</summary>
internal static class Theme
{
    public static void Apply(Window w)
    {
        bool dark = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 1) is int light && light == 0;
        void Set(string k, uint argb) => w.Resources[k] = new SolidColorBrush(Color.FromArgb(
            (byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        Set("Bg", dark ? 0xFF202020 : 0xFFF9F9F9);
        Set("Fg", dark ? 0xFFF2F2F2 : 0xFF1B1B1B);
        Set("Muted", dark ? 0xFFA0A0A0 : 0xFF5F5F5F);
        Set("Field", dark ? 0xFF2D2D2D : 0xFFFFFFFF);
        Set("Line", dark ? 0xFF3D3D3D : 0xFFD0D0D0);

        if (dark)
            w.SourceInitialized += (_, _) =>
            {
                int on = 1; // DWMWA_USE_IMMERSIVE_DARK_MODE — qorong'i sarlavha
                Native.DwmSetWindowAttribute(new WindowInteropHelper(w).Handle, 20, ref on, sizeof(int));
            };
    }
}
