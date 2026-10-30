using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CoffeePdf.Core.Documents;
using CoffeePdf.Core.Files;
using CoffeePdf.Core.Settings;
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
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Subscribed before anything awaits, or the file Finder launched the app with is missed.
            if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
                activatable.Activated += OnActivated;
            foreach (var arg in desktop.Args ?? [])
                OpenFile(arg);
            DispatcherTimer.RunOnce(OpenEmptyWindowIfNone, EmptyWindowDelay);
        }
        base.OnFrameworkInitializationCompleted();
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
        if (e is not FileActivatedEventArgs { Kind: ActivationKind.File } opened)
            return;
        foreach (var file in opened.Files)
            if (file.TryGetLocalPath() is { } path)
                OpenFile(path);
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
