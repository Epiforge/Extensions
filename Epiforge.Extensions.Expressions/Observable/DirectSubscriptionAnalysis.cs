namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Represents whether an expression can be observed by subscribing directly to its change sources, and when it cannot, which part of it is responsible
/// </summary>
public sealed class DirectSubscriptionAnalysis
{
    internal static readonly DirectSubscriptionAnalysis Eligible = new(null, DirectSubscriptionIneligibility.None);

    internal DirectSubscriptionAnalysis(Expression? ineligibleExpression, DirectSubscriptionIneligibility ineligibility)
    {
        IneligibleExpression = ineligibleExpression;
        Ineligibility = ineligibility;
    }

    /// <summary>
    /// Gets why the expression cannot be observed by subscribing directly to its change sources
    /// </summary>
    public DirectSubscriptionIneligibility Ineligibility { get; }

    /// <summary>
    /// Gets the part of the expression which cannot be observed by subscribing directly to its change sources, or <c>null</c> when all of it can
    /// </summary>
    public Expression? IneligibleExpression { get; }

    /// <summary>
    /// Gets whether the expression can be observed by subscribing directly to its change sources
    /// </summary>
    public bool IsEligible =>
        Ineligibility is DirectSubscriptionIneligibility.None;

    /// <inheritdoc/>
    public override string ToString() =>
        IsEligible ? "eligible for direct subscription" : $"ineligible for direct subscription ({Ineligibility}): {IneligibleExpression}";
}
