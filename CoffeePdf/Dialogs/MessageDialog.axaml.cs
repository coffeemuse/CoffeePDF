using Avalonia.Controls;

namespace CoffeePdf.Dialogs;

/// <summary>A modal message over a window, with its buttons in macOS's places: the default on the right.</summary>
public partial class MessageDialog : Window
{
    private string? _answer; // the label of the button chosen

    public MessageDialog() => InitializeComponent();

    /// <summary>
    /// Shows <paramref name="heading"/> and <paramref name="detail"/> over <paramref name="owner"/>, or on its own
    /// with no window to show it over, and answers with the label of the button chosen, or null if the dialog was
    /// closed. The last of <paramref name="rightButtons"/> is the default (Return), and a button labelled Cancel
    /// answers Escape.
    /// </summary>
    public static Task<string?> ShowAsync(Window? owner, string heading, string detail, IReadOnlyList<string> rightButtons, IReadOnlyList<string>? leftButtons = null) =>
        Build(heading, detail, rightButtons, leftButtons).ShowOverAsync(owner);

    /// <summary>A dialog with <paramref name="heading"/>, <paramref name="detail"/> and its buttons, not yet shown.</summary>
    internal static MessageDialog Build(string heading, string detail, IReadOnlyList<string> rightButtons, IReadOnlyList<string>? leftButtons = null)
    {
        var dialog = new MessageDialog();
        dialog.Heading.Text = heading;
        dialog.Detail.Text = detail;
        dialog.Detail.IsVisible = detail.Length > 0;
        foreach (var label in leftButtons ?? [])
            dialog.LeftButtons.Children.Add(dialog.AddButton(label));
        foreach (var label in rightButtons)
            dialog.RightButtons.Children.Add(dialog.AddButton(label));
        if (dialog.RightButtons.Children.LastOrDefault() is Button primary)
            primary.IsDefault = true;
        return dialog;
    }

    /// <summary>
    /// Shows the dialog modal over <paramref name="owner"/>, and answers with the label of the button chosen, or null
    /// if it was closed. With no window to show it over, as for File › Open… with no document open, it shows on its own.
    /// </summary>
    internal Task<string?> ShowOverAsync(Window? owner)
    {
        if (owner is not null)
            return ShowDialog<string?>(owner);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var answered = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Closed += (_, _) => answered.TrySetResult(_answer);
        Show();
        return answered.Task;
    }

    /// <summary>Tells the user something went wrong, with an OK button, over <paramref name="owner"/> or on its own.</summary>
    public static Task ShowErrorAsync(Window? owner, string heading, string detail) => ShowAsync(owner, heading, detail, ["OK"]);

    /// <summary>The usual Save changes? prompt (spec §6.8): Don’t Save on the left, Cancel and Save on the right.</summary>
    public static async Task<SaveChoice> AskToSaveAsync(Window owner, string fileName) =>
        await ShowAsync(owner,
            $"Do you want to save the changes made to the document “{fileName}”?",
            "Your changes will be lost if you don’t save them.",
            ["Cancel", "Save"], ["Don’t Save"]) switch
        {
            "Save" => SaveChoice.Save,
            "Don’t Save" => SaveChoice.DontSave,
            _ => SaveChoice.Cancel,
        };

    private Button AddButton(string label)
    {
        var button = new Button { Content = label, MinWidth = 88, IsCancel = label == "Cancel" };
        button.Click += (_, _) =>
        {
            _answer = label;
            Close(label);
        };
        return button;
    }
}
