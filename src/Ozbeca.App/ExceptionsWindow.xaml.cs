using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ozbeca.App;

/// <summary>O'girilmaydigan so'zlar: qo'shish (Enter, bir nechtasi vergul bilan) va × bilan olib tashlash.</summary>
public partial class ExceptionsWindow : Window
{
    private readonly ObservableCollection<string> _words;

    /// <summary>Saqlangan ro'yxat; null — bekor qilindi.</summary>
    internal List<string>? Result { get; private set; }

    internal ExceptionsWindow(IEnumerable<string> words)
    {
        InitializeComponent();
        Theme.Apply(this);
        _words = new ObservableCollection<string>(words);
        _words.CollectionChanged += (_, _) => Refresh();
        WordList.ItemsSource = _words;
        Refresh();
        Loaded += (_, _) => InputBox.Focus();
    }

    private void Refresh()
    {
        EmptyState.Visibility = _words.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountText.Text = _words.Count == 0 ? "" : $"{_words.Count} ta so'z";
    }

    /// <summary>Kiritilgan matn: vergul, nuqtali vergul yoki yangi qator bilan bir nechta so'z.</summary>
    private void AddInput()
    {
        foreach (string w in InputBox.Text.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string t = w.Trim();
            if (t.Length > 0 && !_words.Contains(t, StringComparer.OrdinalIgnoreCase)) _words.Add(t);
        }
        InputBox.Clear();
        InputBox.Focus();
    }

    private void Add_Click(object sender, RoutedEventArgs e) => AddInput();

    private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        if (InputBox.Text.Trim().Length > 0) AddInput();
        else Save_Click(sender, e); // bo'sh maydonda Enter — saqlash
    }

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e) =>
        InputHint.Visibility = InputBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is string w) _words.Remove(w);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (InputBox.Text.Trim().Length > 0) AddInput(); // yozib, "Qo'shish"ni bosmay saqlagan bo'lsa ham yo'qolmasin
        Result = _words.ToList();
        DialogResult = true;
    }
}
