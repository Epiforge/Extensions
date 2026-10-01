namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Carries a direct observation's values, reached groups, links and held slots into an interpreted evaluation of two parameters, which is the most a runtime without dynamic code can give a delegate made from an interpreted lambda without emitting a thunk
/// </summary>
/// <remarks>
/// One carrier serves each thread. The evaluation copies what it carries into locals before anything else, so an evaluation started inside it on the same thread can refill the carrier without disturbing it, and the carrier is cleared after each evaluation so that it keeps nothing of an observation alive
/// </remarks>
sealed class DirectEvaluationState
{
    [ThreadStatic]
    static DirectEvaluationState? current;

    static readonly FieldInfo heldField = typeof(DirectEvaluationState).GetField(nameof(Held), BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly FieldInfo linksField = typeof(DirectEvaluationState).GetField(nameof(Links), BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly FieldInfo reachedField = typeof(DirectEvaluationState).GetField(nameof(Reached), BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly FieldInfo valuesField = typeof(DirectEvaluationState).GetField(nameof(Values), BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal object?[]? Held;
    internal object?[]? Links;
    internal bool[]? Reached;
    internal object?[]? Values;

    /// <summary>
    /// Wraps the body of an evaluation so that it reads the values, the reached groups, the links and the held slots from the carrier it is given
    /// </summary>
    internal static Expression Bind(Expression body, ParameterExpression state, ParameterExpression values, ParameterExpression reached, ParameterExpression links, ParameterExpression held) =>
        Expression.Block(body.Type, [values, reached, links, held],
            Expression.Assign(values, Expression.Field(state, valuesField)),
            Expression.Assign(reached, Expression.Field(state, reachedField)),
            Expression.Assign(links, Expression.Field(state, linksField)),
            Expression.Assign(held, Expression.Field(state, heldField)),
            body);

    /// <summary>
    /// Makes a delegate taking the observation's state as parameters from one which takes it through this thread's carrier
    /// </summary>
    internal static Func<TArgument, object?[], bool[], object?[], object?[], TResult> Spread<TArgument, TResult>(Func<TArgument, DirectEvaluationState, TResult> evaluate) =>
        (argument, values, reached, links, held) =>
        {
            var state = current ??= new();
            state.Held = held;
            state.Links = links;
            state.Reached = reached;
            state.Values = values;
            try
            {
                return evaluate(argument, state);
            }
            finally
            {
                state.Held = null;
                state.Links = null;
                state.Reached = null;
                state.Values = null;
            }
        };
}
