namespace Epiforge.Extensions.Expressions;

/// <summary>
/// An exception was thrown while processing an exception
/// </summary>
/// <remarks>
/// Instantiates a new instance of the <see cref="ExpressionProcessingException"/>
/// </remarks>
/// <param name="expression"></param>
/// <param name="innerException"></param>
public sealed class ExpressionProcessingException(Expression expression, Exception innerException) :
    Exception("An unexpected exception occurred while processing an expression", innerException)
{

    /// <summary>
    /// Gets the expression that was being processed when the exception was thrown
    /// </summary>
    public Expression Expression { get; } = expression;
}
