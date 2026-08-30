namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Describes why an expression cannot be observed by subscribing directly to its change sources
/// </summary>
public enum DirectSubscriptionIneligibility
{
    /// <summary>
    /// The expression can be observed by subscribing directly to its change sources
    /// </summary>
    None,

    /// <summary>
    /// A member is accessed on a value which can be replaced, so what must be subscribed to changes as that value changes
    /// </summary>
    ChangeableMemberTarget,

    /// <summary>
    /// An indexer is accessed on a value which can be replaced, so what must be subscribed to changes as that value changes
    /// </summary>
    ChangeableIndexTarget,

    /// <summary>
    /// The value produced is registered for disposal, which only the graph performs
    /// </summary>
    ValueRequiresDisposal,

    /// <summary>
    /// An operator implemented by a method is applied, the evaluation of which the graph localizes and disposes
    /// </summary>
    UserDefinedOperator,

    /// <summary>
    /// The expression contains a kind of node for which eligibility has not been established
    /// </summary>
    UnsupportedExpressionKind
}
