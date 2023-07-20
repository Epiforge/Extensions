namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Represents the result of a scalar observable query
/// </summary>
/// <typeparam name="TResult">The type of the result</typeparam>
public interface IObservableScalarQuery<TResult> :
    IObservableQuery
{
    /// <summary>
    /// Gets the outcome of aggregating the query
    /// </summary>
    (Exception? Fault, TResult Result) Evaluation { get; }
}
