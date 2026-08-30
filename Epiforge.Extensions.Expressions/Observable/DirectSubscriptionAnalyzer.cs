namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Determines whether an expression can be observed by subscribing directly to its change sources instead of by building a graph of observable expressions
/// </summary>
/// <remarks>
/// The answer is a property of the options as well as of the expression, since they decide which change sources are subscribed to at all; an expression eligible under one configuration may not be under another
/// </remarks>
/// <remarks>
/// The expression analyzed is the one which will be observed, after its parameters have been replaced and any optimization applied; producing it is the observer's business rather than the analyzer's, so that the two cannot disagree about what was examined
/// </remarks>
public sealed class DirectSubscriptionAnalyzer
{
    static bool IsFixed(Expression expression) =>
        expression switch
        {
            ConstantExpression => true,
            ParameterExpression => true,
            MemberExpression memberExpression => IsClosureField(memberExpression) && memberExpression.Expression is { } target && IsFixed(target),
            UnaryExpression unaryExpression when unaryExpression.NodeType is ExpressionType.Quote => true,
            _ => false
        };

    static bool IsClosureField(MemberExpression memberExpression) =>
        memberExpression.Member is FieldInfo && (memberExpression.Expression?.Type.Name.StartsWith('<') ?? false);

    /// <summary>
    /// Instantiates a direct subscription analyzer with the default options
    /// </summary>
    public DirectSubscriptionAnalyzer() :
        this(new ExpressionObserverOptions())
    {
    }

    /// <summary>
    /// Instantiates a direct subscription analyzer with the specified options
    /// </summary>
    /// <param name="options">The options which decide which change sources are subscribed to</param>
    public DirectSubscriptionAnalyzer(ExpressionObserverOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options;
    }

    readonly ExpressionObserverOptions options;

    /// <summary>
    /// Determines whether the specified expression can be observed by subscribing directly to its change sources
    /// </summary>
    /// <param name="expression">The expression, as it will be observed, with its parameters already replaced and any optimization already applied</param>
    public DirectSubscriptionAnalysis Analyze(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return AnalyzeNode(expression);
    }

    DirectSubscriptionAnalysis AnalyzeIndex(IndexExpression indexExpression)
    {
        if (indexExpression.Indexer is { } indexer && options.IsPropertyValueDisposed(indexer))
            return new(indexExpression, DirectSubscriptionIneligibility.ValueRequiresDisposal);
        if (indexExpression.Object is not { } target)
            return new(indexExpression, DirectSubscriptionIneligibility.UnsupportedExpressionKind);
        if (!IsFixed(target))
            return new(indexExpression, DirectSubscriptionIneligibility.ChangeableIndexTarget);
        var targetAnalysis = AnalyzeNode(target);
        if (!targetAnalysis.IsEligible)
            return targetAnalysis;
        for (int i = 0, ii = indexExpression.Arguments.Count; i < ii; ++i)
        {
            var argumentAnalysis = AnalyzeNode(indexExpression.Arguments[i]);
            if (!argumentAnalysis.IsEligible)
                return argumentAnalysis;
        }
        return DirectSubscriptionAnalysis.Eligible;
    }

    DirectSubscriptionAnalysis AnalyzeMember(MemberExpression memberExpression)
    {
        if (memberExpression.Member is PropertyInfo property && options.IsPropertyValueDisposed(property))
            return new(memberExpression, DirectSubscriptionIneligibility.ValueRequiresDisposal);
        if (memberExpression.Expression is not { } target)
            return DirectSubscriptionAnalysis.Eligible;
        if (!IsFixed(target))
            return new(memberExpression, DirectSubscriptionIneligibility.ChangeableMemberTarget);
        return AnalyzeNode(target);
    }

    DirectSubscriptionAnalysis AnalyzeNode(Expression expression) =>
        expression switch
        {
            ConstantExpression => DirectSubscriptionAnalysis.Eligible,
            ParameterExpression => DirectSubscriptionAnalysis.Eligible,
            MemberExpression memberExpression => AnalyzeMember(memberExpression),
            IndexExpression indexExpression => AnalyzeIndex(indexExpression),
            BinaryExpression binaryExpression when binaryExpression.Method is not null => new(binaryExpression, DirectSubscriptionIneligibility.UserDefinedOperator),
            BinaryExpression binaryExpression when binaryExpression.Conversion is not null => new(binaryExpression, DirectSubscriptionIneligibility.UnsupportedExpressionKind),
            BinaryExpression binaryExpression => AnalyzeNode(binaryExpression.Left) is { IsEligible: false } left ? left : AnalyzeNode(binaryExpression.Right),
            ConditionalExpression conditionalExpression => AnalyzeNode(conditionalExpression.Test) is { IsEligible: false } test ? test : AnalyzeNode(conditionalExpression.IfTrue) is { IsEligible: false } ifTrue ? ifTrue : AnalyzeNode(conditionalExpression.IfFalse),
            TypeBinaryExpression typeBinaryExpression when typeBinaryExpression.NodeType is not ExpressionType.TypeAs => AnalyzeNode(typeBinaryExpression.Expression),
            UnaryExpression unaryExpression when unaryExpression.NodeType is ExpressionType.Quote => DirectSubscriptionAnalysis.Eligible,
            UnaryExpression unaryExpression when unaryExpression.Method is not null => new(unaryExpression, DirectSubscriptionIneligibility.UserDefinedOperator),
            UnaryExpression unaryExpression => AnalyzeNode(unaryExpression.Operand),
            _ => new(expression, DirectSubscriptionIneligibility.UnsupportedExpressionKind)
        };
}
