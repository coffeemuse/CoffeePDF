using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Pdf.Core;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace CoffeePdf.Tests.Toolbar;

/// <summary>
/// Pointer and keyboard input for the toolbar's probes and tests, given as a person gives it, and where a Letter page's
/// points are in the window. Call these on the UI thread.
/// </summary>
internal static class ToolbarTesting
{
    /// <summary>
    /// The control that draws the first page: the smallest Letter-shaped control in the viewer's page host.
    /// PART_PagesContainer is the page host in the PdfViewer 12.0.1 template; the thumbnails, which are Letter-shaped
    /// too, are outside it.
    /// </summary>
    public static Control FirstPage(PdfViewer viewer)
    {
        var host = viewer.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == "PART_PagesContainer")
            ?? throw new InvalidOperationException("The viewer has no PART_PagesContainer: has its template changed?");
        return host.GetVisualDescendants().OfType<Control>()
            .Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 100 && Math.Abs(c.Bounds.Height / c.Bounds.Width - 792.0 / 612) < 0.01)
            .MinBy(c => c.Bounds.Width) ?? throw new InvalidOperationException("No page is drawn yet");
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PdfViewer, System.Runtime.CompilerServices.StrongBox<Rect>> LastBounds = new();

    /// <summary>
    /// Whether the first page is drawn and settled, so a test can wait for it before clicking on it. The page is laid out
    /// at its own size and then fitted to the window, and a click lands on it only after that, so this is true once the
    /// page's bounds are the same on two checks running, and a click on its centre reaches it. Poll it
    /// (<c>TestWindows.WaitForAsync(() =&gt; HasPage(viewer))</c>): the first check of a viewer is never true.
    /// </summary>
    public static bool HasPage(PdfViewer viewer)
    {
        try
        {
            var page = FirstPage(viewer);
            var last = LastBounds.GetOrCreateValue(viewer);
            var unchanged = last.Value == page.Bounds;
            last.Value = page.Bounds;
            if (!unchanged || page.Bounds.Width <= 100 || TopLevel.GetTopLevel(viewer) is not { } top)
                return false;
            var centre = page.TranslatePoint(new Point(page.Bounds.Width / 2, page.Bounds.Height / 2), top);
            return centre is { } at && top.InputHitTest(at) is Visual hit && (hit == page || page.IsVisualAncestorOf(hit));
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>The point in <paramref name="window"/> over (<paramref name="pdfX"/>, <paramref name="pdfY"/>) on a Letter page.</summary>
    public static Point OnPage(PdfViewer viewer, Window window, double pdfX, double pdfY)
    {
        var page = FirstPage(viewer);
        var scale = page.Bounds.Width / 612;
        return page.TranslatePoint(new Point(pdfX * scale, (792 - pdfY) * scale), window)
            ?? throw new InvalidOperationException("The page isn't in the window");
    }

    public static void Click(Window window, Point point)
    {
        window.MouseMove(point);
        Dispatcher.UIThread.RunJobs();
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Clicks <paramref name="control"/> <paramref name="across"/> of the way across it. A split button's main part is on
    /// its left, so its tests click at 0.3.
    /// </summary>
    public static void Click(Window window, Control control, double across = 0.5) =>
        Click(window, control.TranslatePoint(new Point(control.Bounds.Width * across, control.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException($"{control.Name ?? control.GetType().Name} isn't in the window"));

    public static void Drag(Window window, Point from, Point to)
    {
        window.MouseMove(from);
        Dispatcher.UIThread.RunJobs();
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2), RawInputModifiers.LeftMouseButton);
        window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        window.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    public static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var physical = key switch
        {
            Key.Enter => PhysicalKey.Enter,
            Key.Escape => PhysicalKey.Escape,
            Key.Space => PhysicalKey.Space,
            Key.Down => PhysicalKey.ArrowDown,
            Key.F => PhysicalKey.F,
            Key.H => PhysicalKey.H,
            Key.N => PhysicalKey.N,
            Key.D0 => PhysicalKey.Digit0,
            Key.OemPlus => PhysicalKey.Equal,
            _ => PhysicalKey.None,
        };
        window.KeyPress(key, modifiers, physical, null);
        window.KeyRelease(key, modifiers, physical, null);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Puts the cursor in <paramref name="box"/>, replaces its text with <paramref name="text"/>, and presses Return when
    /// <paramref name="enter"/>.
    /// </summary>
    public static void Type(Window window, TextBox box, string text, bool enter = true)
    {
        Dispatcher.UIThread.RunJobs(); // layout and focus pending from opening the window, or the text is lost
        box.Focus();
        box.SelectAll();
        Dispatcher.UIThread.RunJobs();
        window.KeyTextInput(text);
        if (enter)
            Press(window, Key.Enter);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Drags with the armed tool until the viewer has the annotation it draws. Now and then (about one drag in six, in
    /// the headless run) the viewer drops a drag, as probe 5 saw, and a test that waits for what the drag made would wait
    /// for nothing; so a drag that made nothing is made again.
    /// </summary>
    public static async Task DragUntilAnnotatedAsync(Window window, PdfViewer viewer, Point from, Point to, PdfAnnotationType type)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Drag(window, from, to);
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(1);
            while (DateTime.UtcNow < deadline)
            {
                if ((await viewer.GetAnnotationsAsync(0, CancellationToken.None)).Any(a => a.Type == type))
                    return;
                await Task.Delay(1);
                Dispatcher.UIThread.RunJobs();
            }
        }
        throw new TimeoutException($"No {type} annotation came from five drags");
    }

    /// <summary>The viewer's states, such as <c>:shape-selected</c>, as they stand.</summary>
    public static string States(PdfViewer viewer) => string.Join(' ', viewer.Classes.Where(c => c.StartsWith(':')).Order());
}
