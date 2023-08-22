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

    /// <summary>
    /// Continuously applies a transform function to the result of the query
    /// </summary>
    /// <typeparam name="TTransform">The type returned by the transform function</typeparam>
    /// <param name="transform">The transform function</param>
    /// <returns>The transformation of the result of the query</returns>
    IObservableScalarQuery<TTransform> ObserveTransform<TTransform>(Expression<Func<TResult, TTransform>> transform);
}
