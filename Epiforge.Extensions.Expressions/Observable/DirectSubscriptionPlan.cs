namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Represents the analysis of an expression together with the subscriptions an observation of it would make directly, in the order the graph would make them
/// </summary>
/// <remarks>
/// A default instance is ineligible and empty, since eligibility is established rather than assumed
/// </remarks>
public readonly record struct DirectSubscriptionPlan
{
    internal DirectSubscriptionPlan(DirectSubscriptionAnalysis analysis, IReadOnlyList<DirectSubscription>? subscriptions, IReadOnlyList<Expression>? deferredGroups, IReadOnlyList<Expression>? links, IReadOnlyList<(Expression Expression, bool Disposed)>? held)
    {
        Analysis = analysis;
        this.deferredGroups = deferredGroups;
        this.held = held;
        this.links = links;
        this.subscriptions = subscriptions;
    }

    readonly IReadOnlyList<Expression>? deferredGroups;
    readonly IReadOnlyList<(Expression Expression, bool Disposed)>? held;
    readonly IReadOnlyList<Expression>? links;
    readonly IReadOnlyList<DirectSubscription>? subscriptions;

    /// <summary>
    /// Gets the subexpressions the observation resolves once and then holds, each paired with whether the observer disposes of what it produced
    /// </summary>
    internal IReadOnlyList<(Expression Expression, bool Disposed)> Held =>
        held ?? [];

    /// <summary>
    /// Gets whether the expression can be observed by subscribing directly to its change sources, and when it cannot, which part of it is responsible
    /// </summary>
    public DirectSubscriptionAnalysis Analysis { get; }

    /// <summary>
    /// Gets the operands whose evaluation the expression defers, each of which attaches the subscriptions belonging to it the first time it is evaluated; a subscription names one of these by its one-based position
    /// </summary>
    public IReadOnlyList<Expression> DeferredGroups =>
        deferredGroups ?? [];

    /// <summary>
    /// Gets the subexpressions whose value the observation must follow as it changes, each being the target of a member read through something which can notify; a subscription naming one of these attaches to whatever it holds and moves when that changes
    /// </summary>
    public IReadOnlyList<Expression> Links =>
        links ?? [];

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
