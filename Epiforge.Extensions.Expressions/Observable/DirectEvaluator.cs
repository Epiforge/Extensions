namespace Epiforge.Extensions.Expressions.Observable;

sealed class DirectEvaluator
{
    internal static readonly DirectEvaluator Ineligible = new();

    DirectEvaluator()
    {
        DisposedHeldSlots = [];
        Evaluate = null!;
        FixedSubexpressions = [];
        LinkSites = [];
    }

    internal DirectEvaluator(Delegate evaluate, Expression[] fixedSubexpressions, DirectSubscriptionSite[] sites, int deferredGroupCount, int linkCount, IReadOnlyList<(Expression Expression, bool Disposed)> held)
    {
        DeferredGroupCount = deferredGroupCount;
        Evaluate = evaluate;
        FixedSubexpressions = fixedSubexpressions;
        HeldCount = held.Count;
        var disposedHeldSlots = new List<int>();
        for (var i = 0; i < held.Count; ++i)
            if (held[i].Disposed)
                disposedHeldSlots.Add(i);
        DisposedHeldSlots = [.. disposedHeldSlots];
        LinkCount = linkCount;
        var linkSites = new List<int>();
        for (var i = 0; i < sites.Length; ++i)
            if (sites[i].Link >= 0)
                linkSites.Add(i);
        LinkSites = [.. linkSites];
        Sites = sites;
    }

    internal readonly int DeferredGroupCount;
    internal readonly int HeldCount;
    internal readonly int[] DisposedHeldSlots;
    internal readonly int LinkCount;
    internal readonly int[] LinkSites;
    internal readonly Delegate Evaluate;
    internal readonly Expression[] FixedSubexpressions;
    internal readonly DirectSubscriptionSite[]? Sites;
}
