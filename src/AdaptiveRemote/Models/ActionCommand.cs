using System.Windows.Input;

namespace AdaptiveRemote.Models;

internal class ActionCommand : ICommand
{
    private readonly Action _action;

    internal ActionCommand(Action action)
    {
        _action = action;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _action();
}
