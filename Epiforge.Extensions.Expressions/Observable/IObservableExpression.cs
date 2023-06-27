namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Represents an observable lambda expression
/// </summary>
/// <typeparam name="TResult">The type of the value returned by the lambda expression upon which this observable lambda expression</typeparam>
public interface IObservableExpression<TResult> :
    IDisposable,
    IDisposalStatus,
    INotifyDisposalOverridden,
    INotifyDisposed,
    INotifyDisposing,
    INotifyPropertyChanged,
    INotifyPropertyChanging
{
    /// <summary>
    /// Gets the arguments that were passed to the lambda expression
    /// </summary>
    IReadOnlyList<object?> Arguments { get; }

    /// <summary>
    /// Gets the outcome of evaluating the lambda expression
    /// </summary>
    (Exception? Fault, TResult? Result) Evaluation { get; }

    /// <summary>
    /// Gets the observer that is observing the lambda expression
    /// </summary>
    IExpressionObserver Observer { get; }
}

/// <summary>
/// Represents an observable strongly-typed lambda expression with a single argument
/// </summary>
/// <typeparam name="TArgument">The type of the argument passed to the lambda expression</typeparam>
/// <typeparam name="TResult">The type of the value returned by the lambda expression upon which this observable lambda expression</typeparam>
public interface IObservableExpression<TArgument, TResult> :
    IObservableExpression<TResult>,
    IDisposable,
    IDisposalStatus,
    INotifyDisposalOverridden,
    INotifyDisposed,
    INotifyDisposing,
    INotifyPropertyChanged,
    INotifyPropertyChanging
{
    /// <summary>
    /// Gets the argument that was passed to the lambda expression
    /// </summary>
    TArgument Argument { get; }
}

/// <summary>
/// Represents an observable strongly-typed lambda expression with two arguments
/// </summary>
/// <typeparam name="TArgument1">The type of the first argument passed to the lambda expression</typeparam>
/// <typeparam name="TArgument2">The type of the second argument passed to the lambda expression</typeparam>
/// <typeparam name="TResult">The type of the value returned by the lambda expression upon which this observable lambda expression</typeparam>
[SuppressMessage("Code Analysis", "CA1005: Avoid excessive parameters on generic types")]
public interface IObservableExpression<TArgument1, TArgument2, TResult> :
    IObservableExpression<TResult>,
    IDisposable,
    IDisposalStatus,
    INotifyDisposalOverridden,
    INotifyDisposed,
    INotifyDisposing,
    INotifyPropertyChanged,
    INotifyPropertyChanging
{
    /// <summary>
    /// Gets the first argument that was passed to the lambda expression
    /// </summary>
    TArgument1 Argument1 { get; }

    /// <summary>
    /// Gets the second argument that was passed to the lambda expression
    /// </summary>
    TArgument2 Argument2 { get; }
}

/// <summary>
/// Represents an observable strongly-typed lambda expression with three arguments
/// </summary>
/// <typeparam name="TArgument1">The type of the first argument passed to the lambda expression</typeparam>
/// <typeparam name="TArgument2">The type of the second argument passed to the lambda expression</typeparam>
/// <typeparam name="TArgument3">The type of the third argument passed to the lambda expression</typeparam>
/// <typeparam name="TResult">The type of the value returned by the lambda expression upon which this observable lambda expression</typeparam>
[SuppressMessage("Code Analysis", "CA1005: Avoid excessive parameters on generic types")]
public interface IObservableExpression<TArgument1, TArgument2, TArgument3, TResult> :
    IObservableExpression<TArgument1, TArgument2, TResult>
{
    /// <summary>
    /// Gets the third argument that was passed to the lambda expression
    /// </summary>
    TArgument3 Argument3 { get; }
}
