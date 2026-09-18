using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace WorkItems.Desktop;

internal sealed class MainWindow : Window
{
    // Same palette as WorkItems.Api/wwwroot/css/styles.css — keep the two in sync.
    // Key → (light, dark). Some keys override Fluent's own resources so built-in controls match.
    private static readonly Dictionary<string, (string Light, string Dark)> Palette = new()
    {
        ["Canvas"] = ("#F4F6FA", "#0F131B"),
        ["Surface"] = ("#FFFFFF", "#171C26"),
        ["SurfaceHover"] = ("#F6F8FC", "#1D2330"),
        ["Selected"] = ("#EEF2FF", "#232C45"),
        ["Ink"] = ("#19233B", "#E7EBF3"),
        ["Muted"] = ("#657189", "#9099AD"),
        ["Line"] = ("#E2E7F0", "#272E3C"),
        ["Accent"] = ("#405BD8", "#6D84F0"),
        ["AccentSoft"] = ("#E7EDFF", "#232C45"),
        ["Danger"] = ("#B13245", "#F07183"),
        ["DangerSoft"] = ("#FBEAEC", "#3A1D24"),
        ["TodoBg"] = ("#EDF0F5", "#262D3B"), ["TodoFg"] = ("#4F5A70", "#B4BCCD"),
        ["ProgressBg"] = ("#E7EDFF", "#232C4D"), ["ProgressFg"] = ("#3249B8", "#9AABFF"),
        ["DoneBg"] = ("#E4F5EC", "#15352A"), ["DoneFg"] = ("#1B7950", "#6FD3A4"),
        ["AccentButtonBackground"] = ("#405BD8", "#6D84F0"),
        ["AccentButtonBackgroundPointerOver"] = ("#3249B8", "#8497F3"),
        ["AccentButtonBackgroundPressed"] = ("#2A3D9C", "#5A71DD"),
        ["AccentButtonForeground"] = ("#FFFFFF", "#FFFFFF"),
        ["AccentButtonForegroundPointerOver"] = ("#FFFFFF", "#FFFFFF"),
        ["AccentButtonForegroundPressed"] = ("#FFFFFF", "#FFFFFF"),
        ["SystemControlHighlightListLowBrush"] = ("#F6F8FC", "#1D2330"),
        ["SystemControlHighlightListAccentLowBrush"] = ("#EEF2FF", "#232C45"),
        ["SystemControlHighlightListAccentMediumBrush"] = ("#E4EAFF", "#2A3452"),
        ["SystemControlHighlightListAccentHighBrush"] = ("#DAE2FF", "#303B5C"),
        // Toggle buttons are only used as filter chips: outlined, accent-tinted when checked.
        ["ToggleButtonBackground"] = ("#00FFFFFF", "#00000000"),
        ["ToggleButtonBackgroundPointerOver"] = ("#F6F8FC", "#1D2330"),
        ["ToggleButtonBackgroundPressed"] = ("#EEF2FF", "#232C45"),
        ["ToggleButtonForeground"] = ("#657189", "#9099AD"),
        ["ToggleButtonForegroundPointerOver"] = ("#19233B", "#E7EBF3"),
        ["ToggleButtonForegroundPressed"] = ("#19233B", "#E7EBF3"),
        ["ToggleButtonBorderBrush"] = ("#E2E7F0", "#333B4C"),
        ["ToggleButtonBorderBrushPointerOver"] = ("#CDD5E2", "#434C5F"),
        ["ToggleButtonBorderBrushPressed"] = ("#CDD5E2", "#434C5F"),
        ["ToggleButtonBackgroundChecked"] = ("#E7EDFF", "#232C45"),
        ["ToggleButtonBackgroundCheckedPointerOver"] = ("#DAE2FF", "#2A3452"),
        ["ToggleButtonBackgroundCheckedPressed"] = ("#DAE2FF", "#2A3452"),
        ["ToggleButtonForegroundChecked"] = ("#405BD8", "#9AABFF"),
        ["ToggleButtonForegroundCheckedPointerOver"] = ("#405BD8", "#9AABFF"),
        ["ToggleButtonForegroundCheckedPressed"] = ("#405BD8", "#9AABFF"),
        ["ToggleButtonBorderBrushChecked"] = ("#00FFFFFF", "#00000000"),
        ["ToggleButtonBorderBrushCheckedPointerOver"] = ("#00FFFFFF", "#00000000"),
        ["ToggleButtonBorderBrushCheckedPressed"] = ("#00FFFFFF", "#00000000"),
    };

    private static readonly string[] StatusLabels = ["Todo", "In progress", "Done"];

    private readonly WorkItemsClient _client = new();
    private readonly ListBox _items = new() { Background = Brushes.Transparent, ItemTemplate = new FuncDataTemplate<WorkItem>((item, _) => ItemCard(item)) };
    private readonly TextBox _search = new() { PlaceholderText = "Search items…  (⌘F)" };
    private readonly ToggleButton[] _filters = new[] { "All" }.Concat(StatusLabels).Select(label => new ToggleButton { Content = label }).ToArray();
    private readonly StackPanel _empty = new() { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _emptyText = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Button _emptyCreate = new() { Content = "Create your first item", HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBox _title = new() { PlaceholderText = "What needs to be done?", MaxLength = 200 };
    private readonly TextBlock _titleError = new() { Text = "Title is required.", FontSize = 12, IsVisible = false };
    private readonly TextBox _description = new() { PlaceholderText = "Add some context", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 110 };
    private readonly ComboBox _status = new() { ItemsSource = StatusLabels, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly CalendarDatePicker _dueDate = new()
    {
        PlaceholderText = "DD-MM-YYYY",
        SelectedDateFormat = CalendarDatePickerFormat.Custom,
        CustomDateFormatString = "dd-MM-yyyy",
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private readonly Button _save = new() { Content = "Add item", Classes = { "accent" } };
    private readonly Button _cancel = new() { Content = "Cancel", IsVisible = false };
    private readonly Button _new = new() { Content = "+ New item", Classes = { "accent" } };
    private readonly Button _delete = new() { Content = "Delete", Classes = { "danger-text" }, IsVisible = false };
    private readonly Button _retry = new() { Content = "Retry" };
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _count = new();
    private readonly TextBlock _editorHeading = new() { Text = "New item", FontSize = 19, FontWeight = FontWeight.SemiBold };
    private readonly Border _banner = new() { IsVisible = false, CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 8) };
    private readonly TextBlock _bannerText = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _toast = new() { IsVisible = false, CornerRadius = new CornerRadius(99), Padding = new Thickness(18, 9), Margin = new Thickness(0, 0, 0, 24), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly TextBlock _toastText = new() { FontWeight = FontWeight.SemiBold };
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(2.2) };

    private List<WorkItem> _all = [];
    private WorkItem? _selected;
    private int _filter = -1; // -1 = all, otherwise a WorkItemStatus
    private bool _syncingList;
    private bool _busy;

    public MainWindow()
    {
        Title = "Work Items";
        Width = 1000;
        Height = 700;
        MinWidth = 760;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Themed(this, BackgroundProperty, "Canvas");
        Themed(this, ForegroundProperty, "Ink");

        var panels = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1*,1.2*"),
            ColumnSpacing = 18,
            Children = { ListPanel(), EditorPanel() }
        };
        Grid.SetColumn(panels.Children[1], 1);
        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            RowSpacing = 16,
            Margin = new Thickness(24),
            Children = { Header(), Banner(), panels }
        };
        Grid.SetRow(layout.Children[1], 1);
        Grid.SetRow(panels, 2);

        Themed(_toast, Border.BackgroundProperty, "Ink");
        Themed(_toastText, TextBlock.ForegroundProperty, "Canvas");
        _toast.Child = _toastText;
        Content = new Panel { Children = { layout, _toast } };

        _items.SelectionChanged += (_, _) =>
        {
            if (!_syncingList && _items.SelectedItem is WorkItem item) Edit(item);
        };
        _search.TextChanged += (_, _) => ApplyFilter();
        for (var i = 0; i < _filters.Length; i++)
        {
            var status = i - 1;
            _filters[i].Click += (_, _) => { _filter = status; ApplyFilter(); };
        }
        _title.TextChanged += (_, _) => { if (!string.IsNullOrWhiteSpace(_title.Text)) _titleError.IsVisible = false; };
        _save.Click += async (_, _) => await SaveAsync();
        _delete.Click += async (_, _) => await DeleteAsync();
        _new.Click += (_, _) => NewItem();
        _emptyCreate.Click += (_, _) => NewItem();
        _cancel.Click += (_, _) => NewItem();
        _retry.Click += async (_, _) => await RefreshAsync();
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); _toast.IsVisible = false; };
        AddHandler(KeyDownEvent, OnShortcut, RoutingStrategies.Tunnel);
        Opened += async (_, _) => { _title.Focus(); await RefreshAsync(); };
        Closed += (_, _) => _client.Dispose();
    }

    /// <summary>Registers the light/dark palette and shared styles. Fluent follows the macOS appearance setting.</summary>
    public static void ApplyTheme(Application app)
    {
        var resources = app.Resources;
        var light = new ResourceDictionary();
        var dark = new ResourceDictionary();
        foreach (var (key, (lightHex, darkHex)) in Palette)
        {
            light[key] = Brush(lightHex);
            dark[key] = Brush(darkHex);
        }
        resources.ThemeDictionaries[ThemeVariant.Light] = light;
        resources.ThemeDictionaries[ThemeVariant.Dark] = dark;
        resources["ListBoxItemPadding"] = new Thickness(12, 10);
        AddStyles(app.Styles);
    }

    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));

    /// <summary>Binds a property to a palette key so it updates when the system theme changes.</summary>
    private static T Themed<T>(T control, AvaloniaProperty property, string key) where T : Control
    {
        control.Bind(property, control.GetResourceObservable(key));
        return control;
    }

    private static void AddStyles(Styles styles)
    {
        // Fixed reds: readable on both light and dark surfaces.
        var danger = Brush("#C23B4E");
        var dangerHover = Brush("#A93243");
        var dangerText = Brush("#D9485B");
        var presenter = (Selector x) => x.Template().OfType<ContentPresenter>();
        styles.Add(new Style(x => x.OfType<Button>())
        {
            Setters = { new Setter(PaddingProperty, new Thickness(14, 7)), new Setter(CornerRadiusProperty, new CornerRadius(8)) }
        });
        styles.Add(new Style(x => x.OfType<TextBox>())
        {
            Setters = { new Setter(CornerRadiusProperty, new CornerRadius(8)) }
        });
        styles.Add(new Style(x => x.OfType<ListBoxItem>())
        {
            Setters = { new Setter(CornerRadiusProperty, new CornerRadius(10)), new Setter(MarginProperty, new Thickness(0, 0, 0, 4)) }
        });
        styles.Add(new Style(x => presenter(x.OfType<Button>().Class("danger")))
        {
            Setters = { new Setter(ContentPresenter.BackgroundProperty, danger), new Setter(ContentPresenter.ForegroundProperty, Brushes.White) }
        });
        styles.Add(new Style(x => presenter(x.OfType<Button>().Class("danger").Class(":pointerover")))
        {
            Setters = { new Setter(ContentPresenter.BackgroundProperty, dangerHover) }
        });
        styles.Add(new Style(x => presenter(x.OfType<Button>().Class("danger-text")))
        {
            Setters = { new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent), new Setter(ContentPresenter.ForegroundProperty, dangerText), new Setter(ContentPresenter.BorderBrushProperty, Brushes.Transparent) }
        });
        styles.Add(new Style(x => presenter(x.OfType<Button>().Class("danger-text").Class(":pointerover")))
        {
            Setters = { new Setter(ContentPresenter.BackgroundProperty, new SolidColorBrush(Color.Parse("#D9485B"), 0.12)) }
        });
    }

    private static Control Header() => new StackPanel
    {
        Spacing = 2,
        Children =
        {
            new TextBlock { Text = "Work Items", FontSize = 28, FontWeight = FontWeight.Bold },
            Themed(new TextBlock { Text = "Plan what matters, keep it moving." }, TextBlock.ForegroundProperty, "Muted")
        }
    };

    private Control Banner()
    {
        Themed(_banner, Border.BackgroundProperty, "DangerSoft");
        Themed(_bannerText, TextBlock.ForegroundProperty, "Danger");
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { _bannerText, _retry } };
        Grid.SetColumn(_retry, 1);
        _banner.Child = row;
        return _banner;
    }

    private Control ListPanel()
    {
        Themed(_count, TextBlock.ForegroundProperty, "Muted");
        var heading = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel
                {
                    Spacing = 2,
                    Children = { new TextBlock { Text = "Your items", FontSize = 19, FontWeight = FontWeight.SemiBold }, _count }
                },
                _new
            }
        };
        _new.VerticalAlignment = VerticalAlignment.Center;
        _new.SetValue(ToolTip.TipProperty, "New item (⌘N)");
        Grid.SetColumn(_new, 1);

        var filters = new WrapPanel { ItemSpacing = 6, LineSpacing = 6 };
        foreach (var chip in _filters)
        {
            chip.CornerRadius = new CornerRadius(99);
            chip.Padding = new Thickness(12, 4);
            chip.BorderThickness = new Thickness(1);
            chip.FontSize = 13;
            filters.Children.Add(chip);
        }

        Themed(_emptyText, TextBlock.ForegroundProperty, "Muted");
        _empty.Children.Add(_emptyText);
        _empty.Children.Add(_emptyCreate);
        var list = new Panel { Children = { _items, _empty } };

        var panel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"),
            RowSpacing = 12,
            Children = { heading, _search, filters, list }
        };
        Grid.SetRow(_search, 1);
        Grid.SetRow(filters, 2);
        Grid.SetRow(list, 3);
        ApplyFilterChips();
        return Card(panel);
    }

    private Control EditorPanel()
    {
        Themed(_titleError, TextBlock.ForegroundProperty, "Danger");
        Themed(_message, TextBlock.ForegroundProperty, "Danger");
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { _editorHeading, _delete } };
        _editorHeading.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_delete, 1);

        var titleField = Field("Title", _title);
        titleField.Children.Add(_titleError);
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 14,
            Children = { Field("Status", _status), Field("Due date", _dueDate, optional: true) }
        };
        Grid.SetColumn(row.Children[1], 1);
        var fields = new StackPanel
        {
            Spacing = 16,
            Children = { titleField, Field("Description", _description, optional: true), row, _message }
        };

        var hint = Themed(new TextBlock
        {
            Text = "⌘S save · Esc cancel",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        }, TextBlock.ForegroundProperty, "Muted");
        var actions = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { _save, _cancel } },
                hint
            }
        };
        Grid.SetColumn(hint, 1);

        var scroll = new ScrollViewer { Content = fields, Padding = new Thickness(0, 0, 12, 0) };
        var panel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 16,
            Children = { heading, scroll, actions }
        };
        Grid.SetRow(scroll, 1);
        Grid.SetRow(actions, 2);
        return Card(panel);
    }

    private static Border Card(Control content)
    {
        var card = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(20),
            Child = content
        };
        Themed(card, Border.BackgroundProperty, "Surface");
        return Themed(card, Border.BorderBrushProperty, "Line");
    }

    private static StackPanel Field(string label, Control input, bool optional = false)
    {
        var text = new TextBlock { FontWeight = FontWeight.SemiBold };
        text.Inlines!.Add(new Avalonia.Controls.Documents.Run(label));
        if (optional)
        {
            var hint = new Avalonia.Controls.Documents.Run("  optional") { FontWeight = FontWeight.Normal };
            hint.Bind(Avalonia.Controls.Documents.TextElement.ForegroundProperty, text.GetResourceObservable("Muted"));
            text.Inlines.Add(hint);
        }
        return new StackPanel { Spacing = 6, Children = { text, input } };
    }

    private static Control ItemCard(WorkItem? item)
    {
        if (item is null) return new TextBlock();
        var done = item.Status == WorkItemStatus.Done;
        var (bg, fg) = item.Status switch
        {
            WorkItemStatus.Done => ("DoneBg", "DoneFg"),
            WorkItemStatus.InProgress => ("ProgressBg", "ProgressFg"),
            _ => ("TodoBg", "TodoFg")
        };
        var badge = Themed(new Border
        {
            CornerRadius = new CornerRadius(99),
            Padding = new Thickness(8, 2),
            Child = Themed(new TextBlock { Text = WorkItem.StatusLabel(item.Status), FontSize = 11, FontWeight = FontWeight.SemiBold }, TextBlock.ForegroundProperty, fg)
        }, Border.BackgroundProperty, bg);
        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { badge } };
        if (item.DueDate is { } due)
        {
            var overdue = IsOverdue(item);
            meta.Children.Add(Themed(new TextBlock
            {
                Text = $"{(overdue ? "Overdue" : "Due")} {WorkItem.FormatDate(due)}",
                FontSize = 12,
                FontWeight = overdue ? FontWeight.SemiBold : FontWeight.Normal,
                VerticalAlignment = VerticalAlignment.Center
            }, TextBlock.ForegroundProperty, overdue ? "Danger" : "Muted"));
        }

        var title = Themed(new TextBlock
        {
            Text = item.Title,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextDecorations = done ? TextDecorations.Strikethrough : null
        }, TextBlock.ForegroundProperty, done ? "Muted" : "Ink");
        var content = new StackPanel { Spacing = 3, Children = { title } };
        if (!string.IsNullOrWhiteSpace(item.Description))
            content.Children.Add(Themed(new TextBlock
            {
                Text = item.Description.ReplaceLineEndings(" "),
                FontSize = 13,
                TextTrimming = TextTrimming.CharacterEllipsis
            }, TextBlock.ForegroundProperty, "Muted"));
        meta.Margin = new Thickness(0, 5, 0, 0);
        content.Children.Add(meta);
        return content;
    }

    private static bool IsOverdue(WorkItem item) =>
        item.Status != WorkItemStatus.Done && item.DueDate is { } due && due.UtcDateTime.Date < DateTime.Today;

    private void ApplyFilterChips()
    {
        for (var i = 0; i < _filters.Length; i++) _filters[i].IsChecked = i - 1 == _filter;
    }

    private void ApplyFilter()
    {
        ApplyFilterChips();
        var query = _search.Text?.Trim() ?? "";
        var visible = _all.Where(item =>
            (_filter < 0 || (int)item.Status == _filter) &&
            (query.Length == 0 ||
             item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             (item.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))).ToList();

        _syncingList = true;
        _items.ItemsSource = visible;
        _items.SelectedItem = visible.FirstOrDefault(item => item.Id == _selected?.Id);
        _syncingList = false;

        _count.Text = _all.Count == 1 ? "1 item" : $"{_all.Count} items";
        _empty.IsVisible = visible.Count == 0 && !_banner.IsVisible;
        _emptyText.Text = _all.Count == 0 ? "No work items yet." : "No items match your filters.";
        _emptyCreate.IsVisible = _all.Count == 0;
    }

    private void Edit(WorkItem? item)
    {
        _selected = item;
        _title.Text = item?.Title ?? "";
        _description.Text = item?.Description ?? "";
        _status.SelectedIndex = (int)(item?.Status ?? WorkItemStatus.Todo);
        _dueDate.SelectedDate = item?.DueDate?.UtcDateTime.Date;
        _editorHeading.Text = item is null ? "New item" : "Edit item";
        _save.Content = item is null ? "Add item" : "Save changes";
        _cancel.IsVisible = item is not null;
        _delete.IsVisible = item is not null;
        _titleError.IsVisible = false;
        _message.Text = "";
        if (item is null)
        {
            _syncingList = true;
            _items.SelectedItem = null;
            _syncingList = false;
        }
    }

    private void NewItem()
    {
        Edit(null);
        _title.Focus();
    }

    private void OnShortcut(object? sender, KeyEventArgs e)
    {
        var mod = e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (mod && e.Key is Key.S or Key.Enter) { e.Handled = true; _ = SaveAsync(); }
        else if (mod && e.Key == Key.N) { e.Handled = true; NewItem(); }
        else if (mod && e.Key == Key.F) { e.Handled = true; _search.Focus(); _search.SelectAll(); }
        // Esc cancels an edit, never wipes an unsaved new item, and leaves open dropdowns alone.
        else if (e.Key == Key.Escape && _selected is not null && !_status.IsDropDownOpen && !_dueDate.IsDropDownOpen)
        {
            e.Handled = true;
            NewItem();
        }
    }

    private void ShowToast(string text)
    {
        _toastText.Text = text;
        _toast.IsVisible = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private async Task RefreshAsync()
    {
        SetBusy(true);
        try
        {
            _all = await _client.GetItemsAsync();
            _banner.IsVisible = false;
            // Keep editing the same item if it still exists.
            if (_selected is not null)
                _selected = _all.FirstOrDefault(item => item.Id == _selected.Id);
        }
        catch (Exception error)
        {
            _bannerText.Text = error is HttpRequestException
                ? "Cannot reach the Work Items API. Start WorkItems.Api and retry."
                : error.Message;
            _banner.IsVisible = true;
        }
        finally
        {
            ApplyFilter();
            SetBusy(false);
        }
    }

    private async Task SaveAsync()
    {
        if (_busy) return;
        var title = _title.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            _titleError.IsVisible = true;
            _title.Focus();
            return;
        }
        DateTimeOffset? due = _dueDate.SelectedDate is { } date
            ? new DateTimeOffset(DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified), TimeSpan.Zero)
            : null;
        var input = new WorkItemInput(title, string.IsNullOrWhiteSpace(_description.Text) ? null : _description.Text.Trim(),
            (WorkItemStatus)_status.SelectedIndex, due);
        var editing = _selected;

        SetBusy(true);
        try
        {
            await _client.SaveAsync(editing?.Id, input);
            var before = _all.Select(item => item.Id).ToHashSet();
            await RefreshAsync();
            // A new item has the one id we had not seen before.
            Edit(editing is null
                ? _all.FirstOrDefault(item => !before.Contains(item.Id))
                : _all.FirstOrDefault(item => item.Id == editing.Id));
            ApplyFilter();
            ShowToast(editing is null ? "Item added" : "Changes saved");
        }
        catch (Exception error) { _message.Text = error.Message; }
        finally { SetBusy(false); }
    }

    private async Task DeleteAsync()
    {
        if (_selected is not { } item) return;
        if (!await ConfirmDeleteAsync(item.Title)) return;
        SetBusy(true);
        try
        {
            await _client.DeleteAsync(item.Id);
            Edit(null);
            await RefreshAsync();
            ShowToast("Item deleted");
        }
        catch (Exception error) { _message.Text = error.Message; }
        finally { SetBusy(false); }
    }

    private async Task<bool> ConfirmDeleteAsync(string title)
    {
        var dialog = new Window
        {
            Title = "Delete work item",
            Width = 380,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        Themed(dialog, BackgroundProperty, "Surface");
        Themed(dialog, ForegroundProperty, "Ink");
        var cancel = new Button { Content = "Cancel", IsDefault = true };
        var confirm = new Button { Content = "Delete", Classes = { "danger" } };
        cancel.Click += (_, _) => dialog.Close(false);
        confirm.Click += (_, _) => dialog.Close(true);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(22),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Delete item?", FontSize = 17, FontWeight = FontWeight.SemiBold },
                Themed(new TextBlock { Text = $"“{title}” will be permanently removed.", TextWrapping = TextWrapping.Wrap }, TextBlock.ForegroundProperty, "Muted"),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 10,
                    Margin = new Thickness(0, 12, 0, 0),
                    Children = { cancel, confirm }
                }
            }
        };
        dialog.Opened += (_, _) => cancel.Focus();
        return await dialog.ShowDialog<bool>(this);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _save.IsEnabled = !busy;
        _new.IsEnabled = !busy;
        _delete.IsEnabled = !busy;
        _retry.IsEnabled = !busy;
    }
}
