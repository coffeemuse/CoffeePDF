using Avalonia.Controls;

namespace CoffeePdf.Dialogs;

/// <summary>A modal message over a window, with its buttons in macOS's places: the default on the right.</summary>
public partial class MessageDialog : Window
{
    public MessageDialog() => InitializeComponent();

    /// <summary>
    /// Shows <paramref name="heading"/> and <paramref name="detail"/> over <paramref name="owner"/>, and answers with
    /// the label of the button chosen, or null if the dialog was closed. The last of <paramref name="rightButtons"/>
    /// is the default (Return), and a button labelled Cancel answers Escape.
    /// </summary>
    public static Task<string?> ShowAsync(Window owner, string heading, string detail, IReadOnlyList<string> rightButtons, IReadOnlyList<string>? leftButtons = null)
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
        return dialog.ShowDialog<string?>(owner);
    }

    /// <summary>Tells the user something went wrong, with an OK button.</summary>
    public static Task ShowErrorAsync(Window owner, string heading, string detail) => ShowAsync(owner, heading, detail, ["OK"]);

    private Button AddButton(string label)
    {
        var button = new Button { Content = label, MinWidth = 88, IsCancel = label == "Cancel" };
        button.Click += (_, _) => Close(label);
        return button;
    }
}
