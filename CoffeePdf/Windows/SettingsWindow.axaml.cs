using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using CoffeePdf.Core.Settings;
using CoffeePdf.Platform;

namespace CoffeePdf.Windows;

/// <summary>Settings (spec §5): the Markup author, Healdan support, and the Default PDF app. Each change is kept at once.</summary>
public partial class SettingsWindow : Window
{
    private readonly IDefaultPdfApp? _defaultApp;

    public SettingsWindow()
    {
        InitializeComponent();
        // ⌘W closes Settings, as it closes any Mac window. This window has no menu of its own to carry Close (the owner's
        // check 13).
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.W, KeyModifiers.Meta), Command = new ActionCommand(Close) });
    }

    internal SettingsWindow(SettingsStore store, string fullName, IDefaultPdfApp defaultApp) : this()
    {
        _defaultApp = defaultApp;
        AuthorBox.Text = store.MarkupAuthor ?? "";
        AuthorBox.PlaceholderText = fullName;
        // Not TextChanged: Avalonia raises that later, on the dispatcher, and a change is kept the moment it's made.
        AuthorBox.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty)
                return;
            try
            {
                store.MarkupAuthor = AuthorBox.Text;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A full disk or a folder that can't be written: a convenience setting must never crash the app mid-keystroke.
                // The typed text stays in the box, and the store keeps its last good value.
            }
        };
        HealdanBox.IsChecked = store.HealdanEnabled;
        var reverting = false;
        HealdanBox.PropertyChanged += (_, e) =>
        {
            if (e.Property != ToggleButton.IsCheckedProperty || reverting)
                return;
            try
            {
                store.HealdanEnabled = HealdanBox.IsChecked == true; // the windows follow through HealdanEnabledChanged
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Unlike the author, this switch decides where documents open and save, so the box must not show a state the
                // app isn't in: it goes back to what the store keeps, without writing that again.
                reverting = true;
                try
                {
                    HealdanBox.IsChecked = store.HealdanEnabled;
                }
                finally
                {
                    reverting = false;
                }
            }
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
