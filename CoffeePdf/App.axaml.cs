using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CoffeePdf.Core.Documents;
using CoffeePdf.Core.Files;
using CoffeePdf.Core.Helper;
using CoffeePdf.Core.Logging;
using CoffeePdf.Core.Settings;
using CoffeePdf.Dialogs;
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

    private bool _quitting; // a quit is running: it ends CoffeePDF itself, and another request starts nothing

    private readonly List<Task> _releases = []; // the closed windows' Released, so a quit waits for each, whoever closed it

    /// <summary>The running app, from anywhere in it.</summary>
    public static new App Current => (App)Application.Current!;

    /// <summary>The preferences (spec §5), in <see cref="SettingsLocation.Folder"/>.</summary>
    public SettingsStore Settings { get; private set; } = null!;

    /// <summary>File › Open Recent's list (spec §2.1).</summary>
    public RecentDocuments Recent { get; private set; } = null!;

    /// <summary>CoffeePDF's log (spec §6.9).</summary>
    public CoffeeLog Log { get; private set; } = null!;

    /// <summary>Healdan support, the Helper's folders and its channel (spec §5, §9). A test's window gets a fake Helper's instead.</summary>
    public HealdanLink Healdan { get; private set; } = null!;

    /// <summary>The document windows open.</summary>
    internal IReadOnlyList<DocumentWindow> Windows => _windows;

    /// <summary>The name each save writes into new markups: the one set in Settings, else the macOS full name (spec §5).</summary>
    public string MarkupAuthor => Settings.MarkupAuthor ?? _fullName.Value;

    /// <summary>Whether the Healdan Helper is installed, for the first launch's question (spec §5).</summary>
    internal IHelperInstall HelperInstall { get; set; } = new MacHelperInstall();

    private bool _healdanQuestionDue = true; // the first window this run shows asks, if the question is still to ask

    /// <summary>
    /// At the first launch with the Helper installed, asks once over <paramref name="owner"/> whether to use CoffeePDF
    /// with Healdan (spec §5). Turning support on raises <see cref="SettingsStore.HealdanEnabledChanged"/>, which the
    /// windows follow.
    /// </summary>
    private async Task AskAboutHealdanAsync(Window owner)
    {
        try
        {
            await FirstLaunch.AskAboutHealdanAsync(Settings, HelperInstall.IsInstalled, async () =>
                await MessageDialog.ShowAsync(owner, "Use CoffeePDF with Healdan?",
                    "CoffeePDF can open and save documents in Healdan, through Healdan Helper. You can change this in Settings.",
                    ["Not Now", "Use with Healdan"]) == "Use with Healdan");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // settings.json can't be written: the question comes again at the next launch, and nothing else is lost.
        }
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Settings = new SettingsStore(SettingsLocation.Folder());
        Recent = new RecentDocuments(SettingsLocation.Folder());
        Log = new CoffeeLog(CoffeeLog.Folder());
        Healdan = new HealdanLink(Settings, HelperFolders.ForThisUser(), Log);

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
        if (!Healdan.Folders.HoldsCopy(path))
            Recent.Add(path); // Healdan's copies stay out (spec §6.10): the Helper deletes each once it's checked in
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
        // Windows with changes ask first, and every copy is let go before CoffeePDF ends (spec §6.8): also one a closed
        // window is still letting go, and a quit running already, which calls Shutdown itself when it is done.
        var mustWait = _quitting || _releases.Any(r => !r.IsCompleted) || _windows.Any(w => w.HasUnsavedChanges || w.HoldsCopy);
        if (_quitConfirmed || !mustWait)
            return;
        e.Cancel = true;
        if (_quitting)
            return;
        _quitting = true;
        _ = QuitAsync(sender as IClassicDesktopStyleApplicationLifetime);
    }

    private async Task QuitAsync(IClassicDesktopStyleApplicationLifetime? desktop)
    {
        try
        {
            if (!await CloseAllAndWaitAsync(() => _windows, () => [.. _releases]))
                return;
            _quitConfirmed = true;
            desktop?.Shutdown();
        }
        finally
        {
            _quitting = false;
        }
    }

    /// <summary>
    /// For a quit: closes every window, asking those with changes in turn (spec §6.8), and waits until the Helper has
    /// heard of every copy that closed, <paramref name="releases"/> being the closed windows' <c>Released</c>, those
    /// closed by the prompts included. Returns false if a window stayed open.
    /// </summary>
    internal static async Task<bool> CloseAllAndWaitAsync(Func<IReadOnlyList<DocumentWindow>> windows, Func<IReadOnlyList<Task>> releases)
    {
        if (!await CloseAllEditedAsync(windows))
            return false;
        // No window still open has changes, and nothing awaits before they're closed, so none can be edited in between.
        await CloseAndLetGoAsync([.. windows()]);
        await Task.WhenAll(releases());
        return true;
    }

    /// <summary>
    /// Closes <paramref name="windows"/>, which have no unsaved changes, and waits until each has let its copy go, so
    /// the Helper hears of each before CoffeePDF ends (spec §6.8). Each <c>/v1/closed</c> waits 10 seconds at most.
    /// </summary>
    internal static async Task CloseAndLetGoAsync(IReadOnlyList<DocumentWindow> windows)
    {
        foreach (var window in windows)
            window.Close();
        await Task.WhenAll(windows.Select(w => w.Released));
    }

    private void OpenEmptyWindowIfNone()
    {
        if (_windows.Count == 0)
            NewWindow().Show();
    }

    private DocumentWindow NewWindow()
    {
        var window = new DocumentWindow();
        window.Closed += (_, _) =>
        {
            _windows.Remove(window);
            _releases.RemoveAll(r => r.IsCompleted);
            _releases.Add(window.Released);
        };
        _windows.Add(window);
        if (_healdanQuestionDue)
        {
            _healdanQuestionDue = false;
            window.Opened += async (_, _) => await AskAboutHealdanAsync(window);
        }
        return window;
    }
}
