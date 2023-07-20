namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Wraps an exception which occurred while evaluating an element
/// </summary>
[Serializable]
public class EvaluationFaultException :
    Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="EvaluationFaultException"/>
    /// </summary>
    /// <param name="element">The element for which the exception during evaluation occurred</param>
    /// <param name="innerException">The exception that occurred</param>
    public EvaluationFaultException(object? element, Exception innerException) :
        base($"Encountered exception while evaluating element: {element}", innerException) =>
        Element = element;

    /// <summary>
    /// Gets the element for which the exception during evaluation occurred
    /// </summary>
    public object? Element { get; }
}
