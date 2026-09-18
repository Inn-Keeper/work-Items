using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace WorkItems.Desktop;

/// <summary>Light/dark palette, app-wide styles and the helper that binds a control to a palette key.</summary>
internal static class Theme
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
        // Form fields sit just above the card surface instead of Fluent's near-black dark default.
        ["TextControlBackground"] = ("#FFFFFF", "#1E2431"), ["TextControlBackgroundPointerOver"] = ("#F6F8FC", "#232A38"),
        ["TextControlBackgroundFocused"] = ("#FFFFFF", "#232A38"), ["TextControlBorderBrush"] = ("#CDD5E2", "#333B4C"),
        ["TextControlBorderBrushPointerOver"] = ("#B8C2D3", "#434C5F"), ["TextControlBorderBrushFocused"] = ("#405BD8", "#6D84F0"),
        ["ComboBoxBackground"] = ("#FFFFFF", "#1E2431"), ["ComboBoxBackgroundPointerOver"] = ("#F6F8FC", "#232A38"),
        ["ComboBoxBackgroundPressed"] = ("#EEF2FF", "#2A3141"), ["ComboBoxBackgroundUnfocused"] = ("#FFFFFF", "#1E2431"),
        ["ComboBoxBorderBrush"] = ("#CDD5E2", "#333B4C"), ["ComboBoxBorderBrushPointerOver"] = ("#B8C2D3", "#434C5F"),
        ["ComboBoxBorderBrushPressed"] = ("#405BD8", "#6D84F0"), ["ComboBoxDropDownBackground"] = ("#FFFFFF", "#1E2431"),
        ["CalendarDatePickerBackground"] = ("#FFFFFF", "#1E2431"), ["CalendarDatePickerBackgroundPointerOver"] = ("#F6F8FC", "#232A38"),
        ["CalendarDatePickerBackgroundFocused"] = ("#FFFFFF", "#232A38"), ["CalendarDatePickerBackgroundPressed"] = ("#EEF2FF", "#2A3141"),
        ["CalendarDatePickerBorderBrush"] = ("#CDD5E2", "#333B4C"), ["CalendarDatePickerBorderBrushPointerOver"] = ("#B8C2D3", "#434C5F"),
        ["CalendarDatePickerBorderBrushPressed"] = ("#405BD8", "#6D84F0"),
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

    /// <summary>Registers the light/dark palette and shared styles. Fluent follows the macOS appearance setting.</summary>
    public static void Apply(Application app)
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

    public static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));

    /// <summary>Binds a property to a palette key so it updates when the system theme changes.</summary>
    public static T Themed<T>(T control, AvaloniaProperty property, string key) where T : Control
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
            Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(14, 7)), new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(8)) }
        });
        styles.Add(new Style(x => x.OfType<TextBox>())
        {
            Setters = { new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(8)) }
        });
        styles.Add(new Style(x => x.OfType<ListBoxItem>())
        {
            Setters = { new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(10)), new Setter(Layoutable.MarginProperty, new Thickness(0, 0, 0, 4)) }
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
}
