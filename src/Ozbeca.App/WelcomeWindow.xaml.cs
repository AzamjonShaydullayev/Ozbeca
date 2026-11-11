using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Ozbeca.App;

/// <summary>Birinchi ishga tushirishda: o'rnatish, yorliqlar, avtomatik ishga tushirish va avto-yozish so'raladi.</summary>
public partial class WelcomeWindow : Window
{
    internal WelcomeWindow(string hotkey)
    {
        InitializeComponent();
        Theme.Apply(this);
        Theme.RoundClip(HeroClip, 16);
        HotkeyCaps.ItemsSource = HotkeyService.Caps(hotkey);
        if (Install.IsInstalledCopy)
        {
            InstallBox.Visibility = Visibility.Collapsed;
            InstallBox.IsChecked = false;
        }
    }

    internal bool DoInstall => InstallBox.IsChecked == true;
    internal bool DoDesktop => DesktopBox.IsChecked == true;
    internal bool DoStartMenu => StartMenuBox.IsChecked == true;
    internal bool DoAutostart => AutostartBox.IsChecked == true;
    internal bool DoAutoType => AutoTypeBox.IsChecked == true;

    /// <summary>Bitta ko'rinish: eski yozuv xiralashadi, yangisi pastdan ko'tariladi.</summary>
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!SystemParameters.ClientAreaAnimation) { DemoArrow.Opacity = DemoNew.Opacity = 1; DemoNewMove.Y = 0; DemoOld.Opacity = 0.6; return; }
        var start = TimeSpan.FromMilliseconds(450);
        var dur = new Duration(TimeSpan.FromMilliseconds(420));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        DemoOld.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.6, dur) { BeginTime = start });
        DemoArrow.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur) { BeginTime = start });
        DemoNew.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur) { BeginTime = start + TimeSpan.FromMilliseconds(140) });
        DemoNewMove.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(8, 0, dur) { BeginTime = start + TimeSpan.FromMilliseconds(140), EasingFunction = ease });
    }

    private void Done_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
