using Avalonia.Controls;
using Avalonia.Interactivity;
using CoffeePdf.Dialogs;

namespace CoffeePdf.Tests.Dialogs;

public sealed class MessageDialogTests
{
    [Fact]
    public async Task It_answers_with_the_button_chosen_and_puts_the_last_on_the_right_as_the_default()
    {
        var (answer, rightmostIsDefault) = await Headless.RunAsync(async () =>
        {
            var owner = new Window();
            owner.Show();
            var answering = MessageDialog.ShowAsync(owner, "Heading", "Detail", ["Cancel", "Save"], ["Don't Save"]);
            var dialog = (MessageDialog)owner.OwnedWindows.Single();
            var save = (Button)dialog.RightButtons.Children[^1];
            var isDefault = save.IsDefault;
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var result = await answering;
            owner.Close();
            return (result, isDefault);
        });

        Assert.Equal("Save", answer);
        Assert.True(rightmostIsDefault);
    }

    [Fact]
    public async Task Save_changes_offers_Dont_Save_on_the_left_and_Save_as_the_default()
    {
        var (left, right, answer) = await Headless.RunAsync(async () =>
        {
            var owner = new Window();
            owner.Show();
            var asking = MessageDialog.AskToSaveAsync(owner, "Report.pdf");
            var dialog = (MessageDialog)owner.OwnedWindows.Single();
            var labels = (
                dialog.LeftButtons.Children.Cast<Button>().Select(b => (string)b.Content!).ToList(),
                dialog.RightButtons.Children.Cast<Button>().Select(b => (string)b.Content!).ToList());
            ((Button)dialog.LeftButtons.Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var choice = await asking;
            owner.Close();
            return (labels.Item1, labels.Item2, choice);
        });

        Assert.Equal(new[] { "Don’t Save" }, left);
        Assert.Equal(new[] { "Cancel", "Save" }, right);
        Assert.Equal(SaveChoice.DontSave, answer);
    }
}
