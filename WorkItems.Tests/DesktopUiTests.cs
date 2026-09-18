using System.Net.Http.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using WorkItems.Contracts;
using WorkItems.Desktop;
using Xunit;

namespace WorkItems.Tests;

// Entry point for the headless Avalonia session: same theme setup as the real app, no windowing system.
public static class HeadlessApp
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Application>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions())
        .AfterSetup(builder =>
        {
            builder.Instance!.Styles.Add(new FluentTheme());
            Theme.Apply(builder.Instance);
        });
}

public sealed class HeadlessSession : IDisposable
{
    public HeadlessUnitTestSession Session { get; } = HeadlessUnitTestSession.StartNew(typeof(HeadlessApp));
    public void Dispose() => Session.Dispose();
}

/// <summary>Drives MainWindow on a headless UI thread against the in-memory API, only through its controls.</summary>
public sealed class DesktopUiTests(HeadlessSession headless) : IClassFixture<HeadlessSession>, IDisposable
{
    private readonly TestApi _api = new();

    public void Dispose() => _api.Dispose();

    // Every test body runs on the Avalonia UI thread.
    private Task Run(Func<Task> test) => headless.Session.Dispatch(async () => { await test(); return true; }, CancellationToken.None);

    private async Task<Window> OpenWindow(params DelegatingHandler[] handlers)
    {
        var window = new MainWindow(new WorkItemsClient(_api.Factory.CreateDefaultClient(handlers)));
        window.Show();
        await Until(() => Find<TextBlock>(window, "Count").Text is { Length: > 0 });
        return window;
    }

    private static T Find<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void PressSave(Window window) => window.KeyPress(Key.S, RawInputModifiers.Meta, PhysicalKey.S, "s");

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++) await Task.Delay(25);
        Assert.True(condition(), "UI never reached the expected state.");
    }

    private async Task<WorkItemResponse> Seed(string title, params string[] tags)
    {
        using var client = _api.Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/workitems/", new WorkItemInput(title, null, WorkItemStatus.Todo, null, tags));
        return (await response.Content.ReadFromJsonAsync<WorkItemResponse>())!;
    }

    private async Task<int> ServerTotal()
    {
        using var client = _api.Factory.CreateClient();
        return (await client.GetFromJsonAsync<PagedResponse<WorkItemResponse>>("/workitems/"))!.Total;
    }

    private async Task OtherClientRenames(WorkItemResponse item, string title)
    {
        using var client = new WorkItemsClient(_api.Factory.CreateClient());
        await client.SaveAsync(item, new WorkItemInput(title, null, item.Status, null));
    }

    // The in-memory server answers synchronously, so hold POSTs back to keep a save in flight.
    private sealed class SlowPosts : DelegatingHandler
    {
        public const int Delay = 300;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Post) await Task.Delay(Delay, ct);
            return await base.SendAsync(request, ct);
        }
    }

    [Fact]
    public Task RepeatedSaveShortcutCreatesOneItem() => Run(async () =>
    {
        var window = await OpenWindow(new SlowPosts());
        Find<TextBox>(window, "Title").Text = "Only once";

        // The first press starts the save; the others land while it is still in flight.
        PressSave(window);
        PressSave(window);
        PressSave(window);
        await Until(() => Find<Border>(window, "Toast").IsVisible);
        await Task.Delay(SlowPosts.Delay * 2); // let any extra POSTs land before counting

        Assert.Equal(1, await ServerTotal());
    });

    [Fact]
    public Task StaleSaveOffersReloadThenOverwrite() => Run(async () =>
    {
        var seeded = await Seed("Original");
        var window = await OpenWindow();
        var list = Find<ListBox>(window, "Items");
        var title = Find<TextBox>(window, "Title");
        list.SelectedIndex = 0;

        await OtherClientRenames(seeded, "Theirs");
        title.Text = "Mine";
        PressSave(window);
        await Until(() => Find<Border>(window, "Conflict").IsVisible);

        Click(Find<Button>(window, "ConflictReload"));
        await Until(() => title.Text == "Theirs");
        Assert.False(Find<Border>(window, "Conflict").IsVisible);

        await OtherClientRenames(await new WorkItemsClient(_api.Factory.CreateClient()).GetItemAsync(seeded.Id), "Theirs again");
        title.Text = "Mine wins";
        PressSave(window);
        await Until(() => Find<Border>(window, "Conflict").IsVisible);
        Click(Find<Button>(window, "ConflictOverwrite"));
        await Until(() => Find<Border>(window, "Toast").IsVisible);

        var saved = await new WorkItemsClient(_api.Factory.CreateClient()).GetItemAsync(seeded.Id);
        Assert.Equal("Mine wins", saved.Title);
    });

    [Fact]
    public Task LoadMoreAppendsTheNextPage() => Run(async () =>
    {
        for (var i = 0; i < WorkItemLimits.DefaultPageSize + 5; i++) await Seed($"Item {i}");
        var window = await OpenWindow();
        var list = Find<ListBox>(window, "Items");
        var loadMore = Find<Button>(window, "LoadMore");

        Assert.Equal(WorkItemLimits.DefaultPageSize, list.ItemCount);
        Assert.True(loadMore.IsVisible);

        Click(loadMore);
        await Until(() => list.ItemCount == WorkItemLimits.DefaultPageSize + 5);
        Assert.False(loadMore.IsVisible);
    });

    [Fact]
    public Task TagChipFiltersTheListAndClears() => Run(async () =>
    {
        await Seed("Tagged", "linq");
        await Seed("Other", "async");
        var window = await OpenWindow();
        var list = Find<ListBox>(window, "Items");
        await Until(() => window.GetVisualDescendants().OfType<Button>().Any(button => button.Content as string == "#linq"));

        Click(window.GetVisualDescendants().OfType<Button>().First(button => button.Content as string == "#linq"));
        await Until(() => list.ItemCount == 1);
        var chip = Find<Button>(window, "TagFilter");
        Assert.True(chip.IsVisible);

        Click(chip);
        await Until(() => list.ItemCount == 2);
        Assert.False(chip.IsVisible);
    });
}
