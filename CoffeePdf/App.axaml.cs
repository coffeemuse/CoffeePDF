using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CoffeePdf.Core.Documents;
using CoffeePdf.Core.Files;
using CoffeePdf.Core.Settings;
using CoffeePdf.Platform;
using CoffeePdf.Windows;

namespace CoffeePdf;

public partial class App : Application
{
    /// <summary>
    /// How long a launch waits for a file before showing an empty window: a launch from Finder delivers the file as
    /// an event just after the start.
    /// </summary>
    private static readonly TimeSpan EmptyWindowDelay = TimeSpan.FromMilliseconds(500);

    private readonly List<DocumentWindow> _windows = [];

    private readonly Lazy<string> _fullName = new(UserNames.FullName);

    private SettingsWindow? _settingsWindow;

    private bool _quitConfirmed; // the windows with changes have been asked: the shutdown now goes through

    /// <summary>The running app, from anywhere in it.</summary>
    public static new App Current => (App)Application.Current!;

    /// <summary>The preferences (spec §5), in <see cref="SettingsLocation.Folder"/>.</summary>
    public SettingsStore Settings { get; private set; } = null!;

    /// <summary>File › Open Recent's list (spec §2.1).</summary>
    public RecentDocuments Recent { get; private set; } = null!;

    /// <summary>The document windows open.</summary>
    internal IReadOnlyList<DocumentWindow> Windows => _windows;

    /// <summary>The name each save writes into new markups: the one set in Settings, else the macOS full name (spec §5).</summary>
    public string MarkupAuthor => Settings.MarkupAuthor ?? _fullName.Value;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Settings = new SettingsStore(SettingsLocation.Folder());
        Recent = new RecentDocuments(SettingsLocation.Folder());

        // The app menu. macOS adds Services, Hide and Quit after these. Avalonia's own About Avalonia goes once the app
        // sets a menu, so About CoffeePDF opens macOS's About panel instead.
        var appMenu = new NativeMenu();
        if (AboutPanel.IsSupported)
        {
            var about = new NativeMenuItem { Header = "About CoffeePDF" };
            about.Click += (_, _) => AboutPanel.Show();
            appMenu.Items.Add(about);
            appMenu.Items.Add(new NativeMenuItemSeparator());
        }
        var settings = new NativeMenuItem { Header = "Settings…", Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Meta) };
        settings.Click += (_, _) => ShowSettings();
        appMenu.Items.Add(settings);
        NativeMenu.SetMenu(this, appMenu);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Subscribed before anything awaits, or the file Finder launched the app with is missed.
            if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
                activatable.Activated += OnActivated;
            desktop.ShutdownRequested += OnShutdownRequested;
            foreach (var arg in desktop.Args ?? [])
                OpenFile(arg);
            DispatcherTimer.RunOnce(OpenEmptyWindowIfNone, EmptyWindowDelay);
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Settings… (⌘,): one window, brought forward if it's open.</summary>
    public void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(Settings, _fullName.Value, new MacDefaultPdfApp());
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        _settingsWindow.Activate();
    }

    /// <summary>Shows <paramref name="path"/>: in its window if one shows it, else in an empty window, else in a new one.</summary>
    public void OpenFile(string path)
    {
        if (!PdfFile.IsPdf(path))
        {
            Recent.Remove(path); // a recent file that's gone, or no longer a PDF
            return;
        }
        Recent.Add(path);
        var choice = WindowChoice.For(_windows.Select(w => w.FilePath).ToList(), path);
        switch (choice.Action)
        {
            case WindowAction.Activate:
                _windows[choice.Index].Activate();
                break;
            case WindowAction.LoadInto:
                _windows[choice.Index].Load(path);
                _windows[choice.Index].Activate();
                break;
            default:
                var window = NewWindow();
                window.Load(path);
                window.Show();
                break;
        }
    }

    private void OnActivated(object? sender, ActivatedEventArgs e)
    {
        if (e.Kind == ActivationKind.Reopen)
        {
            // The Dock icon, clicked with no window open: an empty window, where ⌘O and Open Recent work.
            if (_windows.Count == 0)
                NewWindow().Show();
            return;
        }
        if (e is not FileActivatedEventArgs { Kind: ActivationKind.File } opened)
            return;
        foreach (var file in opened.Files)
            if (file.TryGetLocalPath() is { } path)
                OpenFile(path);
    }

    /// <summary>
    /// Closes <paramref name="windows"/> one at a time, each asking to save its changes (spec §6.8). Stops at the first
    /// that stays open, and returns whether all closed.
    /// </summary>
    internal static async Task<bool> CloseAllAsync(IReadOnlyList<DocumentWindow> windows)
    {
        foreach (var window in windows)
        {
            window.Activate();
            if (!await window.CloseWithPromptAsync())
                return false;
        }
        return true;
    }

    /// <summary>
    /// Closes every window with unsaved changes, asking each in turn. The prompts take as long as the user does, and a
    /// window can be edited meanwhile, so <paramref name="windows"/> is read again after each round, until none that is
    /// still open has changes. Returns false at the first window that stays open.
    /// </summary>
    internal static async Task<bool> CloseAllEditedAsync(Func<IReadOnlyList<DocumentWindow>> windows)
    {
        // Windows this has closed are left out of the next round, so a window that still reports changes after the user
        // chose Don’t Save can't be asked again.
        var closed = new HashSet<DocumentWindow>();
        while (true)
        {
            var edited = windows().Where(w => !closed.Contains(w) && w.HasUnsavedChanges).ToList();
            if (edited.Count == 0)
                return true;
            if (!await CloseAllAsync(edited))
                return false;
            closed.UnionWith(edited);
        }
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_quitConfirmed || !_windows.Any(w => w.HasUnsavedChanges))
            return;
        e.Cancel = true;
        _ = QuitAsync(sender as IClassicDesktopStyleApplicationLifetime);
    }

    private async Task QuitAsync(IClassicDesktopStyleApplicationLifetime? desktop)
    {
        if (!await CloseAllEditedAsync(() => _windows))
            return;
        // Nothing awaits between the last check and the shutdown, so no window can be edited in between.
        _quitConfirmed = true;
        desktop?.Shutdown();
    }

    private void OpenEmptyWindowIfNone()
    {
        if (_windows.Count == 0)
            NewWindow().Show();
    }

    private DocumentWindow NewWindow()
    {
        var window = new DocumentWindow();
        window.Closed += (_, _) => _windows.Remove(window);
        _windows.Add(window);
        return window;
    }
}
