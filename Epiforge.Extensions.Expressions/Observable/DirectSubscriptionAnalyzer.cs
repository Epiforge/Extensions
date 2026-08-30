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
    sealed class Planner
    {
        readonly HashSet<Expression> planned = new(ExpressionEqualityComparer.Default);

        internal readonly List<DirectSubscription> Subscriptions = [];

        internal bool Reached(Expression expression) =>
            !planned.Add(expression);
    }

    static void AddContentsSubscription(List<DirectSubscription> subscriptions, Expression source, bool dictionaryPermitted, bool collectionPermitted)
    {
        if (source is ConstantExpression constantExpression)
        {
            var value = constantExpression.Value;
            if (dictionaryPermitted && value is INotifyDictionaryChanged)
                subscriptions.Add(new(source, DirectSubscriptionKind.DictionaryChanged, null));
            else if (collectionPermitted && value is INotifyCollectionChanged)
                subscriptions.Add(new(source, DirectSubscriptionKind.CollectionChanged, null));
            return;
        }
        if (dictionaryPermitted && collectionPermitted)
            subscriptions.Add(new(source, DirectSubscriptionKind.DictionaryOrCollectionChanged, null));
        else if (dictionaryPermitted)
            subscriptions.Add(new(source, DirectSubscriptionKind.DictionaryChanged, null));
        else if (collectionPermitted)
            subscriptions.Add(new(source, DirectSubscriptionKind.CollectionChanged, null));
    }

    static void AddPropertyChangedSubscription(List<DirectSubscription> subscriptions, Expression source, DirectSubscriptionKind kind, string propertyName)
    {
        if (source is ConstantExpression constantExpression && constantExpression.Value is not INotifyPropertyChanged)
            return;
        subscriptions.Add(new(source, kind, propertyName));
    }

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
        memberExpression.Member is FieldInfo && IsCompilerGenerated(memberExpression.Expression);

    static bool IsCompilerGenerated(Expression? expression) =>
        expression?.Type.Name.StartsWith('<') ?? false;

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
        return AnalyzeNode(expression, null);
    }

    DirectSubscriptionAnalysis AnalyzeConstant(ConstantExpression constantExpression, Planner? planner)
    {
        if (planner is not null)
            AddContentsSubscription(planner.Subscriptions, constantExpression, options.ConstantExpressionsListenForDictionaryChanged, options.ConstantExpressionsListenForCollectionChanged);
        return DirectSubscriptionAnalysis.Eligible;
    }

    DirectSubscriptionAnalysis AnalyzeIndex(IndexExpression indexExpression, Planner? planner)
    {
        if (indexExpression.Indexer is not { } indexer)
            return new(indexExpression, DirectSubscriptionIneligibility.UnsupportedExpressionKind);
        if (options.IsPropertyValueDisposed(indexer))
            return new(indexExpression, DirectSubscriptionIneligibility.ValueRequiresDisposal);
        if (indexExpression.Object is not { } target)
            return new(indexExpression, DirectSubscriptionIneligibility.UnsupportedExpressionKind);
        if (!IsFixed(target))
            return new(indexExpression, DirectSubscriptionIneligibility.ChangeableIndexTarget);
        var targetAnalysis = AnalyzeNode(target, planner);
        if (!targetAnalysis.IsEligible)
            return targetAnalysis;
        for (int i = 0, ii = indexExpression.Arguments.Count; i < ii; ++i)
        {
            var argumentAnalysis = AnalyzeNode(indexExpression.Arguments[i], planner);
            if (!argumentAnalysis.IsEligible)
                return argumentAnalysis;
        }
        if (planner is not null)
        {
            AddContentsSubscription(planner.Subscriptions, target, true, true);
            AddPropertyChangedSubscription(planner.Subscriptions, target, DirectSubscriptionKind.IndexerPropertyChanged, indexer.Name);
        }
        return DirectSubscriptionAnalysis.Eligible;
    }

    DirectSubscriptionAnalysis AnalyzeMember(MemberExpression memberExpression, Planner? planner)
    {
        if (memberExpression.Member is PropertyInfo disposedProperty && options.IsPropertyValueDisposed(disposedProperty))
            return new(memberExpression, DirectSubscriptionIneligibility.ValueRequiresDisposal);
        if (memberExpression.Expression is not { } target)
            return DirectSubscriptionAnalysis.Eligible;
        if (!IsFixed(target))
            return new(memberExpression, DirectSubscriptionIneligibility.ChangeableMemberTarget);
        var targetAnalysis = AnalyzeNode(target, planner);
        if (!targetAnalysis.IsEligible || planner is null)
            return targetAnalysis;
        if (memberExpression.Member is PropertyInfo property)
        {
            if (!options.IsIgnoredPropertyChangeNotification(property))
                AddPropertyChangedSubscription(planner.Subscriptions, target, DirectSubscriptionKind.MemberPropertyChanged, property.Name);
        }
        else if (memberExpression.Member is FieldInfo && IsCompilerGenerated(target))
            AddContentsSubscription(planner.Subscriptions, memberExpression, options.MemberExpressionsListenToGeneratedTypesFieldValuesForDictionaryChanged, options.MemberExpressionsListenToGeneratedTypesFieldValuesForCollectionChanged);
        return targetAnalysis;
    }

    DirectSubscriptionAnalysis AnalyzeNode(Expression expression, Planner? planner) =>
        planner is not null && planner.Reached(expression) ? DirectSubscriptionAnalysis.Eligible : expression switch
        {
            ConstantExpression constantExpression => AnalyzeConstant(constantExpression, planner),
            ParameterExpression => DirectSubscriptionAnalysis.Eligible,
            MemberExpression memberExpression => AnalyzeMember(memberExpression, planner),
            IndexExpression indexExpression => AnalyzeIndex(indexExpression, planner),
            BinaryExpression binaryExpression when binaryExpression.Method is not null => new(binaryExpression, DirectSubscriptionIneligibility.UserDefinedOperator),
            BinaryExpression binaryExpression when binaryExpression.Conversion is not null => new(binaryExpression, DirectSubscriptionIneligibility.UnsupportedExpressionKind),
            BinaryExpression binaryExpression => AnalyzeNode(binaryExpression.Left, planner) is { IsEligible: false } left ? left : AnalyzeNode(binaryExpression.Right, planner),
            ConditionalExpression conditionalExpression => AnalyzeNode(conditionalExpression.Test, planner) is { IsEligible: false } test ? test : AnalyzeNode(conditionalExpression.IfTrue, planner) is { IsEligible: false } ifTrue ? ifTrue : AnalyzeNode(conditionalExpression.IfFalse, planner),
            TypeBinaryExpression typeBinaryExpression when typeBinaryExpression.NodeType is not ExpressionType.TypeAs => AnalyzeNode(typeBinaryExpression.Expression, planner),
            UnaryExpression unaryExpression when unaryExpression.NodeType is ExpressionType.Quote => DirectSubscriptionAnalysis.Eligible,
            UnaryExpression unaryExpression when unaryExpression.Method is not null => new(unaryExpression, DirectSubscriptionIneligibility.UserDefinedOperator),
            UnaryExpression unaryExpression => AnalyzeNode(unaryExpression.Operand, planner),
            _ => new(expression, DirectSubscriptionIneligibility.UnsupportedExpressionKind)
        };

    /// <summary>
    /// Determines whether the specified expression can be observed by subscribing directly to its change sources and, when it can, which subscriptions that would take
    /// </summary>
    /// <param name="expression">The expression, as it will be observed, with its parameters already replaced and any optimization already applied</param>
    public DirectSubscriptionPlan Plan(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var planner = new Planner();
        var analysis = AnalyzeNode(expression, planner);
        return new(analysis, analysis.IsEligible ? planner.Subscriptions.ToArray() : null);
    }
}
