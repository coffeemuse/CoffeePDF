using Avalonia.Controls;
using CoffeePdf.Core.Helper;

namespace CoffeePdf.Windows;

/// <summary>What a document window's menu bar and the app's (<see cref="Platform.AppMenuBar"/>) have in common.</summary>
internal static class SharedMenu
{
    /// <summary>The item with <paramref name="header"/> in <paramref name="menu"/>, at any depth.</summary>
    public static NativeMenuItem? Find(NativeMenu menu, string header)
    {
        foreach (var item in menu.Items.OfType<NativeMenuItem>())
        {
            if (item.Header == header)
                return item;
            if (item.Menu is { } submenu && Find(submenu, header) is { } found)
                return found;
        }
        return null;
    }

    /// <summary>
    /// Fills File › Open Recent's <paramref name="menu"/> with <paramref name="paths"/>, newest first, each opening
    /// through <paramref name="open"/>, and Clear Menu.
    /// </summary>
    public static void FillOpenRecent(NativeMenu menu, IReadOnlyList<string> paths, Action<string> open, Action clear)
    {
        menu.Items.Clear();
        foreach (var path in paths)
            menu.Items.Add(new NativeMenuItem { Header = Path.GetFileName(path), Command = new ActionCommand(() => open(path)) });
        if (paths.Count > 0)
            menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(new NativeMenuItem { Header = "Clear Menu", Command = new ActionCommand(clear, () => paths.Count > 0) });
    }

    /// <summary>
    /// Open…'s first step while Healdan support is on (spec §6.3): the Helper's Open window. Returns whether the Mac's
    /// Open panel follows, as it does on the Helper's way back, with a Helper that doesn't answer
    /// (<paramref name="away"/> runs), and after an error, which <paramref name="tell"/> shows first.
    /// </summary>
    public static async Task<bool> OpenThroughHelperAsync(HelperChannel channel, Action? away, Func<string, string, Task> tell)
    {
        try
        {
            // Opened: the Helper opens what was chosen in the app for its kind, so a PDF comes back to CoffeePDF.
            return await channel.OpenDialogAsync() is not (OpenDialogAction.Opened or OpenDialogAction.Cancel);
        }
        catch (HelperUnreachableException)
        {
            away?.Invoke();
        }
        catch (HelperErrorException e)
        {
            await tell("Healdan Helper couldn’t show its Open window.", e.Message);
        }
        return true;
    }
}
