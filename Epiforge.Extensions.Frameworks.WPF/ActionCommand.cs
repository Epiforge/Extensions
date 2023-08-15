namespace Epiforge.Extensions.Frameworks.WPF;

/// <summary>
/// A command that can be manipulated by its caller
/// </summary>
public sealed class ActionCommand :
    PropertyChangeNotifier,
    ICommand
{
    /// <summary>
    /// Initializes a new instance of <see cref="ActionCommand"/>
    /// </summary>
    /// <param name="executeAction">The action to invoke when the command is executed</param>
    /// <param name="executable"><c>true</c> if the command starts as executable; otherwise, <c>false</c></param>
    public ActionCommand(Action executeAction, bool executable)
    {
        this.executeAction = executeAction;
        this.executable = executable;
    }

    bool executable;
    readonly Action executeAction;

    /// <summary>
    /// Gets whether the command is executable
    /// </summary>
    public bool Executable
    {
        get => executable;
        set
        {
            if (SetBackedProperty(ref executable, in value))
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Occurs when changes occur that affect whether or not the command should execute
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Defines the method that determines whether the command can execute in its current state
    /// </summary>
    /// <param name="parameter">Data used by the command</param>
    public bool CanExecute(object? parameter) =>
        executable;

    /// <summary>
    /// Defines the method to be called when the command is invoked
    /// </summary>
    /// <param name="parameter">Data used by the command</param>
    public void Execute(object? parameter) =>
        executeAction();
}
