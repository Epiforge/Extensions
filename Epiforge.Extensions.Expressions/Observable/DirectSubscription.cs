namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Represents one event to which an observation subscribes directly, named as the graph would name it
/// </summary>
/// <remarks>
/// A subscription names a site rather than an attachment; whether anything is attached there depends on which notification interfaces the source's value implements, which is why the analyzer resolves the kind only when the source is a constant whose value it can read without invoking anything
/// </remarks>
public readonly record struct DirectSubscription
{
    internal DirectSubscription(Expression source, DirectSubscriptionKind kind, string? propertyName)
    {
        Source = source;
        Kind = kind;
        PropertyName = propertyName;
    }

    /// <summary>
    /// Gets the event to which the subscription attaches
    /// </summary>
    public DirectSubscriptionKind Kind { get; }

    /// <summary>
    /// Gets the name of the member the handler acts upon, or <c>null</c> when the kind reports no name
    /// </summary>
    public string? PropertyName { get; }

    /// <summary>
    /// Gets the expression whose value, resolved once, is the object subscribed to
    /// </summary>
    public Expression? Source { get; }

    /// <inheritdoc/>
    public override string ToString() =>
        PropertyName is null ? $"{Kind} of {Source}" : $"{Kind} ({PropertyName}) of {Source}";
}
