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
}
