namespace Epiforge.Extensions.Expressions.Observable;

readonly struct DirectSubscriptionSite
{
    internal const int Argument = -1;
    internal const int Constant = -2;

    internal DirectSubscriptionSite(DirectSubscription subscription, int valueIndex, object? constant, bool forcesNotification, int link)
    {
        this.constant = constant;
        this.subscription = subscription;
        this.valueIndex = valueIndex;
        ForcesNotification = forcesNotification;
        Link = link;
    }

    readonly object? constant;
    readonly DirectSubscription subscription;
    readonly int valueIndex;

    internal readonly bool ForcesNotification;

    internal readonly int Link;

    internal int DeferredGroup =>
        subscription.DeferredGroup;

    internal string? PropertyName =>
        subscription.PropertyName;

    internal DirectSubscriptionKind ResolveKind(object? value) =>
        subscription.ResolveKind(value);

    internal object? ResolveSource(object? argument, object?[] values, object?[] links) =>
        Link >= 0 ? links[Link] : valueIndex switch
        {
            Argument => argument,
            Constant => constant,
            _ => values[valueIndex]
        };

    public override string ToString() =>
        Link >= 0 ? $"{subscription.Kind} of link {Link}" : valueIndex switch
        {
            Argument => $"{subscription.Kind} of the argument",
            Constant => $"{subscription.Kind} of {constant}",
            _ => $"{subscription.Kind} of frozen value {valueIndex}"
        };
}
