using System.Windows;

namespace Ozbeca.App;

/// <summary>Birinchi ishga tushirishda: o'rnatish, yorliqlar va avtomatik ishga tushirish so'raladi.</summary>
public partial class WelcomeWindow : Window
{
    internal WelcomeWindow(string hotkey)
    {
        InitializeComponent();
        Theme.Apply(this);
        HowTo.Text = $"Istalgan joyda matnni belgilang va {hotkey} ni bosing — u yangi alifboga o'tadi. " +
                     "Dastur ekranning pastki o'ng burchagida, soat yonidagi Ö belgisida jim ishlaydi.";
        InstallNote.Text = $"Dastur {Install.Dir} papkasiga ko'chiriladi. Yuklab olingan faylni keyin o'chirib tashlasangiz bo'ladi.";
        if (Install.IsInstalledCopy)
        {
            InstallBox.Visibility = InstallNote.Visibility = Visibility.Collapsed;
            InstallBox.IsChecked = false;
        }
    }

    internal bool DoInstall => InstallBox.IsChecked == true;
    internal bool DoDesktop => DesktopBox.IsChecked == true;
    internal bool DoStartMenu => StartMenuBox.IsChecked == true;
    internal bool DoAutostart => AutostartBox.IsChecked == true;

    private void Done_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
