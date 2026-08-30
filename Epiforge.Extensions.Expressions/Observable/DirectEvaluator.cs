namespace Epiforge.Extensions.Expressions.Observable;

sealed class DirectEvaluator
{
    internal static readonly DirectEvaluator Ineligible = new();

    DirectEvaluator()
    {
        Evaluate = null!;
        FixedSubexpressions = [];
    }

    internal DirectEvaluator(Delegate evaluate, Expression[] fixedSubexpressions, DirectSubscriptionSite[] sites)
    {
        Evaluate = evaluate;
        FixedSubexpressions = fixedSubexpressions;
        Sites = sites;
    }

    internal readonly Delegate Evaluate;
    internal readonly Expression[] FixedSubexpressions;
    internal readonly DirectSubscriptionSite[]? Sites;
}
