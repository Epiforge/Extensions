namespace Epiforge.Extensions.Expressions.Observable;

sealed class DirectEvaluator
{
    internal static readonly DirectEvaluator Ineligible = new();

    DirectEvaluator()
    {
        Evaluate = null!;
        FixedSubexpressions = [];
        LinkSites = [];
    }

    internal DirectEvaluator(Delegate evaluate, Expression[] fixedSubexpressions, DirectSubscriptionSite[] sites, int deferredGroupCount, int linkCount)
    {
        DeferredGroupCount = deferredGroupCount;
        Evaluate = evaluate;
        FixedSubexpressions = fixedSubexpressions;
        LinkCount = linkCount;
        var linkSites = new List<int>();
        for (var i = 0; i < sites.Length; ++i)
            if (sites[i].Link >= 0)
                linkSites.Add(i);
        LinkSites = [.. linkSites];
        Sites = sites;
    }

    internal readonly int DeferredGroupCount;
    internal readonly int LinkCount;
    internal readonly int[] LinkSites;
    internal readonly Delegate Evaluate;
    internal readonly Expression[] FixedSubexpressions;
    internal readonly DirectSubscriptionSite[]? Sites;
}
