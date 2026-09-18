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
using WorkItems.Contracts;
using static WorkItems.Desktop.Theme;

namespace WorkItems.Desktop;

internal sealed class MainWindow : Window
{
    private static readonly string[] StatusLabels = ["Todo", "In progress", "Done"];
    private static readonly (string Label, WorkItemSort Sort, bool Desc)[] SortOptions =
        [("Due date", WorkItemSort.DueDate, false), ("Newest", WorkItemSort.CreatedAt, true), ("Title", WorkItemSort.Title, false), ("Status", WorkItemSort.Status, false)];

    private readonly WorkItemsClient _client;
    private readonly ListBox _items = new() { Background = Brushes.Transparent };
    private readonly TextBox _search = new() { PlaceholderText = "Search items…  (⌘F)" };
    private static readonly WorkItemStatus?[] FilterValues = [null, WorkItemStatus.Todo, WorkItemStatus.InProgress, WorkItemStatus.Done];
    private readonly ToggleButton[] _filters = new[] { "All" }.Concat(StatusLabels).Select(label => new ToggleButton { Content = label }).ToArray();
    private readonly ComboBox _sort = new() { ItemsSource = SortOptions.Select(option => option.Label).ToArray(), SelectedIndex = 0, MinWidth = 130 };
    private readonly Button _loadMore = new() { Content = "Load more", HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0), IsVisible = false };
    private readonly DispatcherTimer _searchDelay = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly StackPanel _empty = new() { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _emptyText = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Button _emptyCreate = new() { Content = "Create your first item", HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBox _title = new() { PlaceholderText = "What needs to be done?", MaxLength = WorkItemLimits.TitleMaxLength };
    private readonly TextBlock _titleError = new() { Text = "Title is required.", FontSize = 12, IsVisible = false };
    private readonly TextBox _description = new() { PlaceholderText = "Add some context", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 110, MaxLength = WorkItemLimits.DescriptionMaxLength };
    private readonly TextBox _tags = new() { PlaceholderText = "Comma-separated, e.g. learning, ef-core" };
    private readonly Button _tagFilterChip = new() { IsVisible = false, CornerRadius = new CornerRadius(99), Padding = new Thickness(12, 4), FontSize = 13, Classes = { "accent" } };
    private string? _tagFilter;
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
    private readonly Border _conflict = new() { IsVisible = false, CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 10) };
    private readonly Button _conflictReload = new() { Content = "Reload" };
    private readonly Button _conflictOverwrite = new() { Content = "Overwrite", Classes = { "danger" } };
    private bool _conflictOnDelete;
    private readonly TextBlock _count = new();
    private readonly TextBlock _editorHeading = new() { Text = "New item", FontSize = 19, FontWeight = FontWeight.SemiBold };
    private readonly Border _banner = new() { IsVisible = false, CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 8) };
    private readonly TextBlock _bannerText = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _toast = new() { IsVisible = false, CornerRadius = new CornerRadius(99), Padding = new Thickness(18, 9), Margin = new Thickness(0, 0, 0, 24), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly TextBlock _toastText = new() { FontWeight = FontWeight.SemiBold };
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(2.2) };

    private readonly List<WorkItemResponse> _loaded = [];
    private int _total;
    private int _page;
    private int _requestId; // Ignore responses that arrive after a newer query was sent.
    private WorkItemResponse? _selected;
    private WorkItemStatus? _statusFilter;
    private bool _syncingList;
    private int _busyCount; // A counter, not a bool: a list load finishing must not re-enable Save mid-save.
    private bool _saving;   // Blocks a second save/delete while one is in flight (double ⌘S → duplicate item).

    public MainWindow() : this(new WorkItemsClient()) { }

    /// <summary>Takes the API client so tests can point the window at an in-memory API.</summary>
    internal MainWindow(WorkItemsClient client)
    {
        _client = client;
        Title = "Work Items";
        Width = 1000;
        Height = 700;
        MinWidth = 760;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Themed(this, BackgroundProperty, "Canvas");
        Themed(this, ForegroundProperty, "Ink");
        _items.ItemTemplate = new FuncDataTemplate<WorkItemResponse>((item, _) => ItemCard(item));

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
            if (!_syncingList && _items.SelectedItem is WorkItemResponse item) Edit(item);
        };
        _search.TextChanged += (_, _) => { _searchDelay.Stop(); _searchDelay.Start(); };
        _searchDelay.Tick += async (_, _) => { _searchDelay.Stop(); await RefreshAsync(); };
        _sort.SelectionChanged += async (_, _) => await RefreshAsync();
        _loadMore.Click += async (_, _) => await LoadPageAsync(_page + 1);
        for (var i = 0; i < _filters.Length; i++)
        {
            var status = FilterValues[i];
            _filters[i].Click += async (_, _) => { _statusFilter = status; await RefreshAsync(); };
        }
        _title.TextChanged += (_, _) => { if (!string.IsNullOrWhiteSpace(_title.Text)) _titleError.IsVisible = false; };
        _save.Click += async (_, _) => await SaveAsync();
        _delete.Click += async (_, _) => await DeleteAsync();
        _new.Click += (_, _) => NewItem();
        _emptyCreate.Click += (_, _) => NewItem();
        _cancel.Click += (_, _) => NewItem();
        _retry.Click += async (_, _) => await RefreshAsync();
        _tagFilterChip.Click += async (_, _) => await FilterByTagAsync(null);
        _conflictReload.Click += async (_, _) => await ResolveConflictAsync(overwrite: false);
        _conflictOverwrite.Click += async (_, _) => await ResolveConflictAsync(overwrite: true);
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); _toast.IsVisible = false; };
        AddHandler(KeyDownEvent, OnShortcut, RoutingStrategies.Tunnel);
        Opened += async (_, _) => { _title.Focus(); await RefreshAsync(); };
        Closed += (_, _) => _client.Dispose();
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
        _tagFilterChip.SetValue(ToolTip.TipProperty, "Clear tag filter");
        filters.Children.Add(_tagFilterChip);

        Themed(_emptyText, TextBlock.ForegroundProperty, "Muted");
        _empty.Children.Add(_emptyText);
        _empty.Children.Add(_emptyCreate);
        var list = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Children = { _items, _empty, _loadMore } };
        Grid.SetRowSpan(_empty, 2);
        Grid.SetRow(_loadMore, 1);
        var searchRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8, Children = { _search, _sort } };
        Grid.SetColumn(_sort, 1);

        var panel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"),
            RowSpacing = 12,
            Children = { heading, searchRow, filters, list }
        };
        Grid.SetRow(searchRow, 1);
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
            Children = { _conflict, titleField, Field("Description", _description, optional: true), row, Field("Tags", _tags, optional: true), _message }
        };
        Themed(_conflict, Border.BackgroundProperty, "DangerSoft");
        _conflict.Child = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Themed(new TextBlock
                {
                    Text = "This item was changed elsewhere. Reload to see the latest version, or overwrite it with yours.",
                    TextWrapping = TextWrapping.Wrap
                }, TextBlock.ForegroundProperty, "Danger"),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _conflictReload, _conflictOverwrite } }
            }
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

    private Control ItemCard(WorkItemResponse? item)
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
            Child = Themed(new TextBlock { Text = Display.StatusLabel(item.Status), FontSize = 11, FontWeight = FontWeight.SemiBold }, TextBlock.ForegroundProperty, fg)
        }, Border.BackgroundProperty, bg);
        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { badge } };
        if (item.DueDate is { } due)
        {
            var overdue = IsOverdue(item);
            meta.Children.Add(Themed(new TextBlock
            {
                Text = $"{(overdue ? "Overdue" : "Due")} {Display.FormatDate(due)}",
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
        if (item.Tags is { Count: > 0 } tags)
        {
            var chips = new WrapPanel { ItemSpacing = 4, LineSpacing = 4, Margin = new Thickness(0, 4, 0, 0) };
            foreach (var tag in tags)
            {
                // A button inside the list item: clicking it filters instead of selecting the item.
                var chip = Themed(new Button
                {
                    Content = "#" + tag, FontSize = 11, Padding = new Thickness(7, 1), CornerRadius = new CornerRadius(99),
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0)
                }, ForegroundProperty, "Accent");
                chip.SetValue(ToolTip.TipProperty, $"Show items tagged {tag}");
                chip.Click += async (_, _) => await FilterByTagAsync(tag);
                chips.Children.Add(chip);
            }
            content.Children.Add(chips);
        }
        return content;
    }

    private static bool IsOverdue(WorkItemResponse item) =>
        item.Status != WorkItemStatus.Done && item.DueDate is { } due && due.UtcDateTime.Date < DateTime.Today;

    private async Task FilterByTagAsync(string? tag)
    {
        _tagFilter = tag;
        _tagFilterChip.Content = $"#{tag}  ✕";
        _tagFilterChip.IsVisible = tag is not null;
        await RefreshAsync();
    }

    private void ApplyFilterChips()
    {
        for (var i = 0; i < _filters.Length; i++) _filters[i].IsChecked = FilterValues[i] == _statusFilter;
    }

    private void RenderList()
    {
        ApplyFilterChips();
        _syncingList = true;
        _items.ItemsSource = _loaded.ToList();
        _items.SelectedItem = _loaded.FirstOrDefault(item => item.Id == _selected?.Id);
        _syncingList = false;

        var filtered = _statusFilter is not null || _tagFilter is not null || !string.IsNullOrWhiteSpace(_search.Text);
        _count.Text = _total == 1 ? "1 item" : $"{_total} items";
        _loadMore.IsVisible = _loaded.Count < _total;
        _empty.IsVisible = _loaded.Count == 0 && !_banner.IsVisible;
        _emptyText.Text = filtered ? "No items match your filters." : "No work items yet.";
        _emptyCreate.IsVisible = !filtered;
    }

    private void Edit(WorkItemResponse? item)
    {
        _selected = item;
        _title.Text = item?.Title ?? "";
        _description.Text = item?.Description ?? "";
        _status.SelectedIndex = (int)(item?.Status ?? WorkItemStatus.Todo);
        _dueDate.SelectedDate = item?.DueDate?.UtcDateTime.Date;
        _tags.Text = string.Join(", ", item?.Tags ?? []);
        _editorHeading.Text = item is null ? "New item" : "Edit item";
        _save.Content = item is null ? "Add item" : "Save changes";
        _cancel.IsVisible = item is not null;
        _delete.IsVisible = item is not null;
        _titleError.IsVisible = false;
        _conflict.IsVisible = false;
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
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Meta) && !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            // Esc cancels an edit, never wipes an unsaved new item, and leaves open dropdowns alone.
            if (e.Key == Key.Escape && _selected is not null && !_status.IsDropDownOpen && !_dueDate.IsDropDownOpen)
            {
                e.Handled = true;
                NewItem();
            }
            return;
        }

        switch (e.Key)
        {
            case Key.S or Key.Enter: _ = SaveAsync(); break;
            case Key.N: NewItem(); break;
            case Key.F: _search.Focus(); _search.SelectAll(); break;
            default: return;
        }
        e.Handled = true;
    }

    private void ShowToast(string text)
    {
        _toastText.Text = text;
        _toast.IsVisible = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private Task RefreshAsync() => LoadPageAsync(1);

    private async Task LoadPageAsync(int page)
    {
        var requestId = ++_requestId;
        var (_, sort, desc) = SortOptions[Math.Max(_sort.SelectedIndex, 0)];
        var query = new WorkItemListQuery(_statusFilter, _search.Text, _tagFilter, sort, desc, page);
        SetBusy(true);
        try
        {
            var result = await _client.GetItemsAsync(query);
            if (requestId != _requestId) return;
            if (page == 1) _loaded.Clear();
            _loaded.AddRange(result.Items);
            (_total, _page) = (result.Total, result.Page);
            _banner.IsVisible = false;
        }
        catch (Exception error)
        {
            if (requestId != _requestId) return;
            // Only a failed connection means the API is down; a server error has its own message.
            _bannerText.Text = error is HttpRequestException { StatusCode: null }
                ? "Cannot reach the Work Items API. Start WorkItems.Api and retry."
                : error.Message;
            _banner.IsVisible = true;
        }
        finally
        {
            if (requestId == _requestId) RenderList();
            SetBusy(false);
        }
    }

    private async Task SaveAsync()
    {
        if (_saving) return;
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
            (WorkItemStatus)_status.SelectedIndex, due,
            (_tags.Text ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        var editing = _selected;

        _saving = true;
        SetBusy(true);
        try
        {
            // Keep editing the saved item even if the current filter or page hides it.
            Edit(await _client.SaveAsync(editing, input));
            await RefreshAsync();
            ShowToast(editing is null ? "Item added" : "Changes saved");
        }
        catch (VersionConflictException) { ShowConflict(onDelete: false); }
        catch (Exception error) { _message.Text = error.Message; }
        finally { SetBusy(false); _saving = false; }
    }

    private async Task DeleteAsync()
    {
        if (_selected is not { } item) return;
        if (!await ConfirmDeleteAsync(item.Title)) return;
        await DeleteSelectedAsync();
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selected is not { } item || _saving) return;
        _saving = true;
        SetBusy(true);
        try
        {
            await _client.DeleteAsync(item);
            Edit(null);
            await RefreshAsync();
            ShowToast("Item deleted");
        }
        catch (VersionConflictException) { ShowConflict(onDelete: true); }
        catch (Exception error) { _message.Text = error.Message; }
        finally { SetBusy(false); _saving = false; }
    }

    private void ShowConflict(bool onDelete)
    {
        _conflictOnDelete = onDelete;
        _conflictOverwrite.Content = onDelete ? "Delete anyway" : "Overwrite";
        _conflict.IsVisible = true;
    }

    // Both options start from the server's latest version. Overwrite keeps the form as typed and
    // retries against that version; Reload discards the form and shows the latest.
    private async Task ResolveConflictAsync(bool overwrite)
    {
        if (_selected is not { } item) return;
        WorkItemResponse latest;
        try { latest = await _client.GetItemAsync(item.Id); }
        catch (Exception error)
        {
            _conflict.IsVisible = false;
            _message.Text = error.Message;
            return;
        }

        if (!overwrite)
        {
            Edit(latest);
            await RefreshAsync();
            return;
        }
        _selected = latest;
        _conflict.IsVisible = false;
        await (_conflictOnDelete ? DeleteSelectedAsync() : SaveAsync());
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
        _busyCount += busy ? 1 : -1;
        busy = _busyCount > 0;
        _save.IsEnabled = !busy;
        _new.IsEnabled = !busy;
        _delete.IsEnabled = !busy;
        _retry.IsEnabled = !busy;
        _conflictReload.IsEnabled = !busy;
        _conflictOverwrite.IsEnabled = !busy;
        _loadMore.IsEnabled = !busy;
    }
}
