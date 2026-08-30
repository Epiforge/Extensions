namespace Epiforge.Extensions.Expressions.Observable;

readonly struct DirectSubscriptionSite
{
    internal const int Argument = -1;
    internal const int Constant = -2;

    internal DirectSubscriptionSite(DirectSubscription subscription, int valueIndex, object? constant, bool forcesNotification)
    {
        this.constant = constant;
        this.subscription = subscription;
        this.valueIndex = valueIndex;
        ForcesNotification = forcesNotification;
    }

    readonly object? constant;
    readonly DirectSubscription subscription;
    readonly int valueIndex;

    internal readonly bool ForcesNotification;

    internal string? PropertyName =>
        subscription.PropertyName;

    internal DirectSubscriptionKind ResolveKind(object? value) =>
        subscription.ResolveKind(value);

    internal object? ResolveSource(object? argument, object?[] values) =>
        valueIndex switch
        {
            Argument => argument,
            Constant => constant,
            _ => values[valueIndex]
        };

    public override string ToString() =>
        valueIndex switch
        {
            Argument => $"{subscription.Kind} of the argument",
            Constant => $"{subscription.Kind} of {constant}",
            _ => $"{subscription.Kind} of frozen value {valueIndex}"
        };
}
