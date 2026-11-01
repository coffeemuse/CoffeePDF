using Avalonia.Controls;
using Avalonia.Input;
using CoffeePdf.Core.Helper;
using CoffeePdf.Core.Settings;
using CoffeePdf.Dialogs;
using CoffeePdf.Windows;

namespace CoffeePdf.Platform;

/// <summary>
/// The menu bar macOS shows while no document window is in front, with none open or with Settings in front: the
/// CoffeePDF menu, then File and Healdan, so Open… (⌘O) and Open Recent work without an empty window first (owner,
/// 2026-11-01). File has a window's items, with those that need a document greyed out. Healdan shows while Healdan
/// support is on, with its items greyed out, as in an empty window.
/// </summary>
/// <remarks>
/// <para>
/// Avalonia.Native builds the bar it shows without a window's menu from the menu that holds the app's, if the app's
/// menu has one, so <see cref="Menu"/> holds the CoffeePDF menu first. A window's bar borrows that first item while the
/// window is in front, so the bar's own items never change once it's built: Healdan is hidden, not removed.
/// </para>
/// <para>
/// It relies on issue #9's patch (Avalonia#22285), which puts the CoffeePDF menu back first when the bar returns.
/// Avalonia 12.1.4's own code puts it back last, which would show File's menu under CoffeePDF's name.
/// </para>
/// </remarks>
internal sealed class AppMenuBar
{
    private readonly HealdanLink _healdan;
    private readonly Action<string> _openFile;

    /// <summary>
    /// The bar holding <paramref name="appMenu"/>, the CoffeePDF menu. Open Recent lists <paramref name="recent"/>, and
    /// the files chosen from it or Open… go to <paramref name="openFile"/>.
    /// </summary>
    public AppMenuBar(NativeMenu appMenu, HealdanLink healdan, RecentDocuments recent, Action<string> openFile)
    {
        _healdan = healdan;
        _openFile = openFile;
        var openRecent = new NativeMenu();
        var healdanMenu = new NativeMenuItem
        {
            Header = "Healdan",
            IsVisible = healdan.Enabled,
            Menu = new NativeMenu
            {
                Items = { Off("Save to Healdan…"), Off("Save as New Version"), Off("Save as New Document…"), new NativeMenuItemSeparator(), Off("Properties…") },
            },
        };
        Menu = new NativeMenu
        {
            Items =
            {
                new NativeMenuItem { Menu = appMenu },
                new NativeMenuItem
                {
                    Header = "File",
                    Menu = new NativeMenu
                    {
                        Items =
                        {
                            new NativeMenuItem
                            {
                                Header = "Open…",
                                Gesture = new KeyGesture(Key.O, KeyModifiers.Meta),
                                Command = new ActionCommand(async () => await OpenAsync()),
                            },
                            new NativeMenuItem { Header = "Open Recent", Menu = openRecent },
                            new NativeMenuItemSeparator(),
                            Off("Close", new KeyGesture(Key.W, KeyModifiers.Meta)),
                            Off("Save", new KeyGesture(Key.S, KeyModifiers.Meta)),
                            Off("Save As…", new KeyGesture(Key.S, KeyModifiers.Meta | KeyModifiers.Shift)),
                            new NativeMenuItemSeparator(),
                            Off("Print…", new KeyGesture(Key.P, KeyModifiers.Meta)),
                        },
                    },
                },
                healdanMenu,
            },
        };
        SharedMenu.FillOpenRecent(openRecent, recent.Paths, openFile, recent.Clear);
        recent.Changed += (_, _) => SharedMenu.FillOpenRecent(openRecent, recent.Paths, openFile, recent.Clear);
        healdan.Settings.HealdanEnabledChanged += (_, _) => healdanMenu.IsVisible = healdan.Enabled;
    }

    /// <summary>The bar, which holds the CoffeePDF menu first.</summary>
    public NativeMenu Menu { get; }

    /// <summary>The Mac's Open panel, on its own as there's no window to show it on. The tests set a stand-in.</summary>
    internal Func<Task<IReadOnlyList<string>>> PickFilesToOpen { get; set; } = OpenPanel.PickPdfsAsync;

    /// <summary>Shows a problem in a window of its own, as there's none to show it over. The tests collect it instead.</summary>
    internal Func<string, string, Task> ReportError { get; set; } = (heading, detail) => MessageDialog.ShowErrorAsync(null, heading, detail);

    /// <summary>The bar's item with <paramref name="header"/>, at any depth.</summary>
    internal NativeMenuItem MenuItem(string header) =>
        SharedMenu.Find(Menu, header) ?? throw new InvalidOperationException($"No menu item {header}");

    /// <summary>
    /// Open… (⌘O) with no document window: the Helper's Open window while Healdan support is on (spec §6.3). Its way
    /// back, a Helper that doesn't answer, an error (shown first), or support off shows the Mac's Open panel.
    /// </summary>
    internal async Task OpenAsync()
    {
        // A Helper that doesn't answer has no window here to show it in, so the Mac's panel just follows.
        if (_healdan.Enabled && !await SharedMenu.OpenThroughHelperAsync(_healdan.Channel, away: null, ReportError))
            return;
        foreach (var path in await PickFilesToOpen())
            _openFile(path);
    }

    /// <summary>An item that needs a document, so it's greyed out here.</summary>
    private static NativeMenuItem Off(string header, KeyGesture? gesture = null) =>
        new() { Header = header, Gesture = gesture, IsEnabled = false };
}
