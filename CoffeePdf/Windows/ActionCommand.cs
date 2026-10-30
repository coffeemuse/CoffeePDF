using System.Windows.Input;

namespace CoffeePdf.Windows;

/// <summary>A command for a menu item built in code, such as Open Recent's, so the tests can run it.</summary>
internal sealed class ActionCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    // Nothing changes CanExecute while a menu is shown: each window rebuilds its Open Recent instead.
    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => execute();
}
