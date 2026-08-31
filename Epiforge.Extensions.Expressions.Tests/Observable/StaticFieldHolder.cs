namespace Epiforge.Extensions.Expressions.Tests.Observable;

public static class StaticFieldHolder
{
    public static readonly SubscriptionLog Log = new();

    public static Recorded Held = new(Log) { Rank = 3, Score = 1, Tag = "static" };
}
