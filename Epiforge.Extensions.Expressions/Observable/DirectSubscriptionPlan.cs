namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Represents the analysis of an expression together with the subscriptions an observation of it would make directly, in the order the graph would make them
/// </summary>
/// <remarks>
/// A default instance is ineligible and empty, since eligibility is established rather than assumed
/// </remarks>
public readonly record struct DirectSubscriptionPlan
{
    internal DirectSubscriptionPlan(DirectSubscriptionAnalysis analysis, IReadOnlyList<DirectSubscription>? subscriptions)
    {
        Analysis = analysis;
        this.subscriptions = subscriptions;
    }

    readonly IReadOnlyList<DirectSubscription>? subscriptions;

    /// <summary>
    /// Gets whether the expression can be observed by subscribing directly to its change sources, and when it cannot, which part of it is responsible
    /// </summary>
    public DirectSubscriptionAnalysis Analysis { get; }

    /// <summary>
    /// Gets whether the expression can be observed by subscribing directly to its change sources
    /// </summary>
    public bool IsEligible =>
        Analysis.IsEligible;

    /// <summary>
    /// Gets the subscriptions the observation would make, which is empty when the expression is ineligible
    /// </summary>
    public IReadOnlyList<DirectSubscription> Subscriptions =>
        subscriptions ?? [];

    /// <inheritdoc/>
    public override string ToString() =>
        IsEligible ? $"eligible for direct subscription, by {Subscriptions.Count} subscriptions" : Analysis.ToString();
}
