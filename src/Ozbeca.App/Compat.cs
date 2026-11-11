using System.Security.Principal;

namespace Ozbeca.App;

/// <summary>.NET Framework 4.8 da yo'q, yangi .NET dagi kichik yordamchilar o'rnini bosadi.</summary>
internal static class Compat
{
    /// <summary>Environment.ProcessPath o'rnida — ishlayotgan exe'ning to'liq yo'li.</summary>
    public static readonly string ProcessPath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;

    /// <summary>Environment.IsPrivilegedProcess o'rnida — dastur administrator sifatida ishga tushganmi.</summary>
    public static readonly bool IsElevatedSelf = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    public static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
}
