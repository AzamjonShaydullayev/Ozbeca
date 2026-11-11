using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Ozbeca.App;

/// <summary>
/// Avto-o'girish qaysi dasturlarda ishlashini tanlash. Har belgilash darhol saqlanadi.
/// Hech narsa belgilanmasa — hamma dasturlarda ishlaydi.
/// </summary>
public partial class AppPickerWindow : Window
{
    internal sealed class Item : INotifyPropertyChanged
    {
        private bool _checked;
        public string Name { get; init; } = "";
        public string Process { get; init; } = "";
        public ImageSource? Icon { get; init; }
        public Visibility FallbackVisibility => Icon == null ? Visibility.Visible : Visibility.Collapsed;
        public Action? Changed;

        public bool IsChecked
        {
            get => _checked;
            set
            {
                if (_checked == value) return;
                _checked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                Changed?.Invoke();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private readonly App _app;
    private List<Item> _items = new();

    internal AppPickerWindow(App app)
    {
        InitializeComponent();
        _app = app;
        Theme.Apply(this);
        UpdateState();
        Loaded += async (_, _) =>
        {
            SearchBox.Focus();
            var installed = await AppCatalog.LoadAsync();
            BuildItems(installed);
        };
    }

    private void BuildItems(List<InstalledApp> installed)
    {
        var chosen = _app.Settings.AutoTypeApps;
        var chosenSet = new HashSet<string>(chosen.Select(a => a.Process), StringComparer.OrdinalIgnoreCase);
        var items = installed.Select(a => new Item { Name = a.Name, Process = a.Process, Icon = a.Icon, IsChecked = chosenSet.Contains(a.Process) }).ToList();
        // Tanlangan, lekin ro'yxatda yo'q (o'chirilgan yoki ko'chma dastur) — baribir ko'rinsin, olib tashlasa bo'lsin.
        var known = new HashSet<string>(items.Select(i => i.Process), StringComparer.OrdinalIgnoreCase);
        items.AddRange(chosen.Where(a => !known.Contains(a.Process)).Select(a => new Item { Name = a.Name, Process = a.Process, IsChecked = true }));
        // Ochilganda tanlanganlar tepada; keyin tartib o'zgarmaydi (bosganda chip sakrab ketmasin).
        _items = items.OrderByDescending(i => i.IsChecked).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        foreach (var i in _items) i.Changed = Save;
        ApplyFilter();
    }

    private void Save()
    {
        _app.SetAutoTypeApps(_items.Where(i => i.IsChecked).Select(i => new AllowedApp { Name = i.Name, Process = i.Process }).ToList());
        UpdateState();
    }

    private void UpdateState()
    {
        var chosen = _app.Settings.AutoTypeApps;
        StateText.Text = chosen.Count == 0
            ? "Hamma dasturlarda ishlaydi"
            : $"Faqat tanlangan {chosen.Count} ta dasturda ishlaydi";
        StateDot.Fill = chosen.Count == 0 ? (Brush)FindResource("Good") : (Brush)FindResource("Accent");
        ClearButton.IsEnabled = chosen.Count > 0;
        ClearButton.Opacity = chosen.Count > 0 ? 1 : 0.5;
    }

    private void ApplyFilter()
    {
        string q = SearchBox.Text.Trim();
        SearchHint.Visibility = q.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var shown = q.Length == 0 ? _items : _items.Where(i =>
            i.Name.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
            i.Process.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        AppList.ItemsSource = shown;
        EmptyText.Text = _items.Count == 0 ? "Dasturlar topilmadi" : "Hech narsa topilmadi";
        EmptyText.Visibility = shown.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (IsLoaded && _items.Count > 0) ApplyFilter();
        else SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        foreach (var i in _items) i.Changed = null;
        foreach (var i in _items) i.IsChecked = false;
        foreach (var i in _items) i.Changed = Save;
        Save();
    }

    private void Done_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }
}
