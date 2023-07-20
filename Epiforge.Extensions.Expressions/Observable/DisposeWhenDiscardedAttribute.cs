namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Specifies that expression observers should dispose of the returned value when it is discarded
/// </summary>
[AttributeUsage(AttributeTargets.ReturnValue)]
public sealed class DisposeWhenDiscardedAttribute :
    Attribute
{
}
