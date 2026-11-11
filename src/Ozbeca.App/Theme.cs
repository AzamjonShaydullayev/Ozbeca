using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace Ozbeca.App;

/// <summary>
/// Yorug'/qorong'i mavzu va Windows 11 Mica fon. Ranglar oyna resurslariga yoziladi (DynamicResource):
/// Bg, Fg, Muted, Card, CardLine, Line, Field, KeyCap, KeyCapLine, Accent, AccentHover, AccentText, Good, ToggleOff.
/// </summary>
internal static class Theme
{
    public static readonly Color Accent = Color.FromRgb(0x00, 0x99, 0xB5);       // bayroqdagi moviy
    public static readonly Color AccentDeep = Color.FromRgb(0x00, 0x72, 0x8A);
    public static readonly Color Good = Color.FromRgb(0x1E, 0xB5, 0x3A);         // bayroqdagi yashil

    public static bool IsDark => Registry.GetValue(
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int light && light == 0;

    /// <summary>Mica — Windows 11 22H2 (build 22621) va undan yangi.</summary>
    private static bool SupportsMica =>
        int.TryParse(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuildNumber", "0") as string, out int b)
        && b >= 22621;

    public static void Apply(Window w)
    {
        bool dark = IsDark, mica = SupportsMica;
        void Set(string k, uint argb) => w.Resources[k] = new SolidColorBrush(Color.FromArgb(
            (byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));

        Set("Bg", mica ? 0x00000000u : dark ? 0xFF202020 : 0xFFF3F3F3);
        Set("Fg", dark ? 0xFFF3F3F3 : 0xFF1A1A1A);
        Set("Muted", dark ? 0xFFA6A6A6 : 0xFF5C5C5C);
        Set("Card", dark ? (mica ? 0x0DFFFFFFu : 0xFF2B2B2Bu) : (mica ? 0xB3FFFFFFu : 0xFFFFFFFFu));
        Set("CardLine", dark ? 0x1AFFFFFFu : 0x14000000u);
        Set("Line", dark ? 0x14FFFFFFu : 0x0F000000u);
        Set("Field", dark ? 0x14FFFFFFu : 0xFFFFFFFFu);
        Set("KeyCap", dark ? 0x1FFFFFFFu : 0xFFFFFFFFu);
        Set("KeyCapLine", dark ? 0x33FFFFFFu : 0x33000000u);
        Set("ToggleOff", dark ? 0xFF9A9A9A : 0xFF6E6E6E);
        w.Resources["Accent"] = new SolidColorBrush(dark ? Color.FromRgb(0x2E, 0xC4, 0xDE) : Accent);
        w.Resources["AccentHover"] = new SolidColorBrush(dark ? Accent : AccentDeep);
        w.Resources["AccentText"] = new SolidColorBrush(dark ? Color.FromRgb(0x00, 0x1E, 0x24) : Colors.White);
        w.Resources["Good"] = new SolidColorBrush(Good);

        w.SourceInitialized += (_, _) =>
        {
            IntPtr hwnd = new WindowInteropHelper(w).Handle;
            int on = dark ? 1 : 0; // DWMWA_USE_IMMERSIVE_DARK_MODE
            Native.DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int));
            if (!mica) return;
            HwndSource.FromHwnd(hwnd)!.CompositionTarget.BackgroundColor = Colors.Transparent;
            var m = new Native.MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            Native.DwmExtendFrameIntoClientArea(hwnd, ref m);
            int backdrop = 2; // DWMWA_SYSTEMBACKDROP_TYPE = Mica
            Native.DwmSetWindowAttribute(hwnd, 38, ref backdrop, sizeof(int));
        };
    }

    /// <summary>"1.1.0" — exe versiyasi (commit hash'siz).</summary>
    public static string Version
    {
        get
        {
            var v = typeof(Theme).Assembly.GetName().Version;
            return v == null ? "" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }
}
