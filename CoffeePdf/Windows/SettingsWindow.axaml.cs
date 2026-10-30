using Avalonia.Controls;
using Avalonia.Interactivity;
using CoffeePdf.Core.Settings;
using CoffeePdf.Platform;

namespace CoffeePdf.Windows;

/// <summary>
/// Settings (spec §5): the Markup author, and the Default PDF app. Healdan support joins it in Phase 2, when it does
/// something. Each change is kept at once.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly IDefaultPdfApp? _defaultApp;

    public SettingsWindow() => InitializeComponent();

    internal SettingsWindow(SettingsStore store, string fullName, IDefaultPdfApp defaultApp) : this()
    {
        _defaultApp = defaultApp;
        AuthorBox.Text = store.MarkupAuthor ?? "";
        AuthorBox.PlaceholderText = fullName;
        // Not TextChanged: Avalonia raises that later, on the dispatcher, and a change is kept the moment it's made.
        AuthorBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
                store.MarkupAuthor = AuthorBox.Text;
        };
        DefaultAppSection.IsVisible = defaultApp.IsSupported;
        ShowDefaultApp();
    }

    /// <summary>The Default PDF app button.</summary>
    internal void MakeDefault()
    {
        _defaultApp?.MakeCoffeePdf();
        ShowDefaultApp();
    }

    private void ShowDefaultApp()
    {
        var isDefault = _defaultApp?.IsCoffeePdf() == true;
        DefaultAppStatus.Text = isDefault ? "CoffeePDF opens PDFs." : "Another app opens PDFs.";
        MakeDefaultButton.IsVisible = !isDefault;
    }

    private void OnMakeDefault(object? sender, RoutedEventArgs e) => MakeDefault();
}
