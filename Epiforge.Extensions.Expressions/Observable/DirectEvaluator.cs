namespace Epiforge.Extensions.Expressions.Observable;

sealed class DirectEvaluator
{
    internal DirectEvaluator(Delegate evaluate, Expression[] fixedSubexpressions)
    {
        Evaluate = evaluate;
        FixedSubexpressions = fixedSubexpressions;
    }

    internal readonly Delegate Evaluate;
    internal readonly Expression[] FixedSubexpressions;
}
