using System.Windows;
using System.Windows.Controls;

namespace Ozbeca.App;

/// <summary>O'girilmaydigan so'zlar ro'yxati: har qatorda bitta so'z (ism, brend, login).</summary>
internal static class ExceptionsDialog
{
    /// <summary>Yangi ro'yxat yoki null (bekor qilindi).</summary>
    public static List<string>? Edit(Window owner, IEnumerable<string> words)
    {
        var box = new TextBox
        {
            Text = string.Join(Environment.NewLine, words),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 220,
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 13,
        };
        var hint = new TextBlock
        {
            Text = "Har qatorga bitta so'z. Ular hech qachon o'girilmaydi: ismlar (Shohjahon), brendlar (Chevrolet), loginlar.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
            Opacity = 0.75,
        };
        var save = new Button { Content = "Saqlash", IsDefault = true, Padding = new Thickness(18, 6, 18, 6), Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Bekor qilish", IsCancel = true, Padding = new Thickness(14, 6, 14, 6) };
        if (owner.TryFindResource("PrimaryButton") is Style primary) save.Style = primary;
        if (owner.TryFindResource("SubtleButton") is Style subtle) cancel.Style = subtle;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(save);
        buttons.Children.Add(cancel);
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(hint);
        panel.Children.Add(box);
        panel.Children.Add(buttons);

        var w = new Window
        {
            Title = "O'girilmaydigan so'zlar",
            Owner = owner,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            Content = panel,
        };
        Theme.Apply(w, useMica: false); // Bg/Fg ranglar har oynaning o'z resurslarida (tungi rejim ham)
        w.SetResourceReference(Control.BackgroundProperty, "Bg");
        w.SetResourceReference(Control.ForegroundProperty, "Fg");
        if (owner.TryFindResource("UiFont") is System.Windows.Media.FontFamily font) w.FontFamily = font;
        save.Click += (_, _) => w.DialogResult = true;
        w.Loaded += (_, _) => { box.Focus(); box.CaretIndex = box.Text.Length; };
        if (w.ShowDialog() != true) return null;
        return box.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
    }
}
