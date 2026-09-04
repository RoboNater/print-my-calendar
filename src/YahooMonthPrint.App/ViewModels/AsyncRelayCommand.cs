using System.Windows.Input;

namespace YahooMonthPrint.App.ViewModels;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> execute;
    private readonly Action<Exception> handleException;
    private readonly Func<bool>? canExecute;

    public AsyncRelayCommand(
        Func<Task> execute,
        Action<Exception> handleException,
        Func<bool>? canExecute = null)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.handleException = handleException ?? throw new ArgumentNullException(nameof(handleException));
        this.canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter)
    {
        try
        {
            await execute();
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            handleException(exception);
        }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and not AccessViolationException;
}
