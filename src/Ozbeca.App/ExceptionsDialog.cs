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
            Style = (Style)owner.FindResource("Field"),
        };
        var hint = new TextBlock
        {
            Text = "Har qatorga bitta so'z. Ular hech qachon o'girilmaydi: ismlar (Shohjahon), brendlar (Chevrolet), loginlar.",
            Style = (Style)owner.FindResource("RowHint"),
            Margin = new Thickness(0, 0, 0, 10),
        };
        var save = new Button { Content = "Saqlash", IsDefault = true, Style = (Style)owner.FindResource("PrimaryButton"), Padding = new Thickness(22, 8, 22, 8), Margin = new Thickness(0, 0, 10, 0) };
        var cancel = new Button { Content = "Bekor qilish", IsCancel = true, Style = (Style)owner.FindResource("SubtleButton"), Padding = new Thickness(14, 8, 14, 8) };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        buttons.Children.Add(save);
        buttons.Children.Add(cancel);
        var panel = new StackPanel { Margin = new Thickness(20, 12, 20, 18) };
        panel.Children.Add(hint);
        panel.Children.Add(box);
        panel.Children.Add(buttons);

        var w = new Window
        {
            Title = "O'girilmaydigan so'zlar",
            Owner = owner,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            FontFamily = (System.Windows.Media.FontFamily)owner.FindResource("UiFont"),
            FontSize = 13,
            Content = panel,
        };
        Theme.Apply(w);
        w.SetResourceReference(Control.BackgroundProperty, "Bg");
        w.SetResourceReference(Control.ForegroundProperty, "Fg");
        save.Click += (_, _) => w.DialogResult = true;
        w.Loaded += (_, _) => { box.Focus(); box.CaretIndex = box.Text.Length; };
        if (w.ShowDialog() != true) return null;
        return box.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
    }
}
