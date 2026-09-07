namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Determines whether an expression can be observed by subscribing directly to its change sources instead of by building a graph of observable expressions
/// </summary>
/// <remarks>
/// The answer is a property of the options as well as of the expression, since they decide which change sources are subscribed to at all; an expression eligible under one configuration may not be under another
/// </remarks>
/// <remarks>
/// A parameter is analyzed as the argument which will replace it, so that a lambda body and the parameter-replaced expression derived from it yield the same answer and the same subscriptions
/// </remarks>
/// <remarks>
/// A field is a fixed target whatever declares it and whether it is static or an instance field, because a field raises no change notification and is therefore read once and held by either mechanism; only the contents of a field of a compiler-generated type are watched, which is what the graph does
/// </remarks>
/// <remarks>
/// An operator, property or indexer backed by a method is admitted when its return type is sealed and implements neither disposal interface, because the graph's disposal of such a value is a runtime type test which cannot succeed; a registration of such a member for disposal is refused by the options, so only the blanket disposal of static method return values reaches this rule
/// </remarks>
/// <remarks>
/// A static property is a fixed target, because the graph gives it a node with no dependency which is therefore evaluated once and held; reading it afresh on every evaluation would make the two mechanisms disagree the moment it changed
/// </remarks>
/// <remarks>
/// A method call is admitted when its return type is sealed and implements neither disposal interface, decided by the return type alone rather than by whether the call is registered for disposal, so that the rule holds whatever the options say; it contributes only the subscriptions its object and its arguments contribute, the graph's node for a call subscribing to nothing itself and re-evaluating when one of those changes
/// </remarks>
/// <remarks>
/// A call to the get method of a property or an indexer is rewritten into the member or index access it stands for and analyzed as that, exactly as the graph rewrites it, so that an indexer written in C# — which reaches this analysis as a call — plans the subscriptions the rewritten form plans rather than none of them
/// </remarks>
/// <remarks>
/// A short-circuiting operator and a conditional expression are admitted whatever their deferred operands reach, the subscriptions of each deferred operand being attached the first time an evaluation reaches that operand rather than when the observation is constructed, which is where the graph attaches the nodes of that operand and after which it never detaches them
/// </remarks>
/// <remarks>
/// A subscription belongs to the nearest operand enclosing every use of the node which plans it, because the graph gives one node to an expression however many operands name it and attaches that node the first time any of them is evaluated; the contents of a constant or of the argument are the exception, the graph attaching those when the node is constructed whether or not its evaluation is deferred
/// </remarks>
/// <remarks>
/// A property read through a target which is not fixed is admitted only when no value the target could hold raises a change notification, which is decided by its type being sealed and implementing none of the notification interfaces; such a member contributes no subscription of its own, exactly as the graph's node for it subscribes to nothing, and the chain is watched by whatever its target contributes. A target which could notify is refused, because what would have to be subscribed to changes as that target's value changes, while the plan is decided once when the observation is constructed
/// </remarks>
public sealed class DirectSubscriptionAnalyzer
{
    sealed class ExpressionIdentityComparer :
        IEqualityComparer<Expression>
    {
        internal static readonly ExpressionIdentityComparer Default = new();

        public bool Equals(Expression? x, Expression? y) =>
            ReferenceEquals(x, y);

        public int GetHashCode(Expression obj) =>
            RuntimeHelpers.GetHashCode(obj);
    }

    sealed class Planner
    {
        sealed class GroupLowering(Planner planner, int group) :
            ExpressionVisitor
        {
            public override Expression? Visit(Expression? node)
            {
                if (node is UnaryExpression { NodeType: ExpressionType.Quote })
                    return node;
                if (node is not null)
                    planner.Lower(node, group);
                return base.Visit(node);
            }
        }

        readonly Dictionary<Expression, int> groups = new(ExpressionIdentityComparer.Default);
        readonly List<Expression> owners = [];
        readonly List<int> parents = [];

        internal readonly List<Expression> DeferredGroups = [];
        internal readonly List<DirectSubscription> Subscriptions = [];

        internal int CurrentGroup;

        internal void Add(Expression owner, DirectSubscription subscription)
        {
            owners.Add(owner);
            Subscriptions.Add(subscription);
        }

        internal void BeginDeferredGroup(Expression operand)
        {
            DeferredGroups.Add(operand);
            parents.Add(CurrentGroup);
            CurrentGroup = DeferredGroups.Count;
        }

        int DepthOf(int group)
        {
            var depth = 0;
            while (group != 0)
            {
                group = parents[group - 1];
                ++depth;
            }
            return depth;
        }

        internal void EndDeferredGroup(int enclosing) =>
            CurrentGroup = enclosing;

        void Lower(Expression expression, int group)
        {
            if (groups.TryGetValue(expression, out var reached) && NearestCommonAncestor(reached, group) is var lowered && lowered != reached)
                groups[expression] = lowered;
        }

        int NearestCommonAncestor(int first, int second)
        {
            if (first == 0 || second == 0)
                return 0;
            while (DepthOf(first) > DepthOf(second))
                first = parents[first - 1];
            while (DepthOf(second) > DepthOf(first))
                second = parents[second - 1];
            while (first != second)
            {
                first = parents[first - 1];
                second = parents[second - 1];
            }
            return first;
        }

        internal bool Reached(Expression expression)
        {
            var group = expression is ConstantExpression or ParameterExpression ? 0 : CurrentGroup;
            if (!groups.TryGetValue(expression, out var reached))
            {
                groups.Add(expression, group);
                return false;
            }
            if (NearestCommonAncestor(reached, group) is var lowered && lowered != reached)
                new GroupLowering(this, lowered).Visit(expression);
            return true;
        }

        internal void Resolve()
        {
            for (int i = 0, ii = Subscriptions.Count; i < ii; ++i)
                if (Subscriptions[i] is var subscription && groups[owners[i]] is var owned && owned != subscription.DeferredGroup)
                    Subscriptions[i] = new(subscription.Source!, subscription.Kind, subscription.PropertyName, owned);
            var used = new bool[DeferredGroups.Count];
            for (int i = 0, ii = Subscriptions.Count; i < ii; ++i)
                if (Subscriptions[i].DeferredGroup is var group && group > 0)
                    used[group - 1] = true;
            var renumbered = new int[DeferredGroups.Count + 1];
            var kept = new List<Expression>();
            for (var group = 0; group < used.Length; ++group)
                if (used[group])
                {
                    kept.Add(DeferredGroups[group]);
                    renumbered[group + 1] = kept.Count;
                }
            if (kept.Count == DeferredGroups.Count)
                return;
            for (int i = 0, ii = Subscriptions.Count; i < ii; ++i)
                if (Subscriptions[i] is var subscription && renumbered[subscription.DeferredGroup] is var group && group != subscription.DeferredGroup)
                    Subscriptions[i] = new(subscription.Source!, subscription.Kind, subscription.PropertyName, group);
            DeferredGroups.Clear();
            DeferredGroups.AddRange(kept);
        }
    }

    static void AddContentsSubscription(Planner planner, Expression owner, Expression source, bool dictionaryPermitted, bool collectionPermitted)
    {
        if (source is ConstantExpression constantExpression)
        {
            var value = constantExpression.Value;
            if (dictionaryPermitted && value is INotifyDictionaryChanged)
                planner.Add(owner, new(source, DirectSubscriptionKind.DictionaryChanged, null, 0));
            else if (collectionPermitted && value is INotifyCollectionChanged)
                planner.Add(owner, new(source, DirectSubscriptionKind.CollectionChanged, null, 0));
            return;
        }
        if (dictionaryPermitted && collectionPermitted)
            planner.Add(owner, new(source, DirectSubscriptionKind.DictionaryOrCollectionChanged, null, 0));
        else if (dictionaryPermitted)
            planner.Add(owner, new(source, DirectSubscriptionKind.DictionaryChanged, null, 0));
        else if (collectionPermitted)
            planner.Add(owner, new(source, DirectSubscriptionKind.CollectionChanged, null, 0));
    }

    static void AddPropertyChangedSubscription(Planner planner, Expression owner, Expression source, DirectSubscriptionKind kind, string propertyName)
    {
        if (source is ConstantExpression constantExpression && constantExpression.Value is not INotifyPropertyChanged)
            return;
        planner.Add(owner, new(source, kind, propertyName, 0));
    }

    internal static bool IsFixed(Expression expression) =>
        expression switch
        {
            ConstantExpression => true,
            ParameterExpression => true,
            MemberExpression { Member: FieldInfo } memberExpression => memberExpression.Expression is not { } target || IsFixed(target),
            MemberExpression { Member: PropertyInfo, Expression: null } => true,
            UnaryExpression unaryExpression when unaryExpression.NodeType is ExpressionType.Quote => true,
            _ => false
        };

    static bool CannotNotify(Type type) =>
        type.IsSealed && !typeof(INotifyPropertyChanged).IsAssignableFrom(type) && !typeof(INotifyCollectionChanged).IsAssignableFrom(type) && !typeof(INotifyDictionaryChanged).IsAssignableFrom(type);

    static bool IsShortCircuiting(BinaryExpression binaryExpression) =>
        binaryExpression.NodeType is ExpressionType.Coalesce || binaryExpression.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse && binaryExpression.Type == typeof(bool);

    static bool IsCompilerGenerated(Expression? expression) =>
        expression?.Type.Name.StartsWith('<') ?? false;

    static ExpressionObserverOptions Validated(ExpressionObserverOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options;
    }

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
    public DirectSubscriptionAnalyzer(ExpressionObserverOptions options) :
        this(Validated(options).ConstantExpressionsListenForCollectionChanged, options.ConstantExpressionsListenForDictionaryChanged, options.MemberExpressionsListenToGeneratedTypesFieldValuesForCollectionChanged, options.MemberExpressionsListenToGeneratedTypesFieldValuesForDictionaryChanged, options.IsIgnoredPropertyChangeNotification, options.IsPropertyValueDisposed)
    {
    }

    internal DirectSubscriptionAnalyzer(ExpressionObserver observer) :
        this(observer.ConstantExpressionsListenForCollectionChanged, observer.ConstantExpressionsListenForDictionaryChanged, observer.MemberExpressionsListenToGeneratedTypesFieldValuesForCollectionChanged, observer.MemberExpressionsListenToGeneratedTypesFieldValuesForDictionaryChanged, observer.IsIgnoredPropertyChangeNotification, observer.IsPropertyValueDisposed)
    {
    }

    DirectSubscriptionAnalyzer(bool constantsListenForCollectionChanged, bool constantsListenForDictionaryChanged, bool generatedTypeFieldsListenForCollectionChanged, bool generatedTypeFieldsListenForDictionaryChanged, Func<PropertyInfo, bool> isIgnoredPropertyChangeNotification, Func<PropertyInfo, bool> isPropertyValueDisposed)
    {
        this.constantsListenForCollectionChanged = constantsListenForCollectionChanged;
        this.constantsListenForDictionaryChanged = constantsListenForDictionaryChanged;
        this.generatedTypeFieldsListenForCollectionChanged = generatedTypeFieldsListenForCollectionChanged;
        this.generatedTypeFieldsListenForDictionaryChanged = generatedTypeFieldsListenForDictionaryChanged;
        this.isIgnoredPropertyChangeNotification = isIgnoredPropertyChangeNotification;
        this.isPropertyValueDisposed = isPropertyValueDisposed;
    }

    readonly bool constantsListenForCollectionChanged;
    readonly bool constantsListenForDictionaryChanged;
    readonly bool generatedTypeFieldsListenForCollectionChanged;
    readonly bool generatedTypeFieldsListenForDictionaryChanged;
    readonly Func<PropertyInfo, bool> isIgnoredPropertyChangeNotification;
    readonly Func<PropertyInfo, bool> isPropertyValueDisposed;

    /// <summary>
    /// Determines whether the specified expression can be observed by subscribing directly to its change sources
    /// </summary>
    /// <param name="expression">The expression, which may be a lambda body or the parameter-replaced expression derived from one</param>
    public DirectSubscriptionAnalysis Analyze(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return Resolved(expression, new Planner());
    }

    DirectSubscriptionAnalysis AnalyzeConditional(ConditionalExpression conditionalExpression, Planner? planner)
    {
        if (planner is null)
            return new(conditionalExpression, DirectSubscriptionIneligibility.DeferredBranch);
        var testAnalysis = AnalyzeNode(conditionalExpression.Test, planner);
        if (!testAnalysis.IsEligible)
            return testAnalysis;
        var ifTrueAnalysis = AnalyzeDeferredOperand(conditionalExpression.IfTrue, planner);
        return ifTrueAnalysis.IsEligible ? AnalyzeDeferredOperand(conditionalExpression.IfFalse, planner) : ifTrueAnalysis;
    }

    DirectSubscriptionAnalysis AnalyzeConstant(ConstantExpression constantExpression, Planner? planner)
    {
        if (planner is not null)
            AddContentsSubscription(planner, constantExpression, constantExpression, constantsListenForDictionaryChanged, constantsListenForCollectionChanged);
        return DirectSubscriptionAnalysis.Eligible;
    }

    DirectSubscriptionAnalysis AnalyzeDeferredOperand(Expression operand, Planner planner)
    {
        var enclosing = planner.CurrentGroup;
        planner.BeginDeferredGroup(operand);
        var analysis = AnalyzeNode(operand, planner);
        planner.EndDeferredGroup(enclosing);
        return analysis;
    }

    DirectSubscriptionAnalysis AnalyzeIndex(IndexExpression indexExpression, Planner? planner)
    {
        if (indexExpression.Indexer is not { } indexer)
            return new(indexExpression, DirectSubscriptionIneligibility.UnsupportedExpressionKind);
        if (isPropertyValueDisposed(indexer) && !ExpressionObserverOptions.CannotBeDisposed(indexer.PropertyType))
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
            AddContentsSubscription(planner, indexExpression, target, true, true);
            AddPropertyChangedSubscription(planner, indexExpression, target, DirectSubscriptionKind.IndexerPropertyChanged, indexer.Name);
        }
        return DirectSubscriptionAnalysis.Eligible;
    }

    DirectSubscriptionAnalysis AnalyzeMember(MemberExpression memberExpression, Planner? planner)
    {
        if (memberExpression.Member is PropertyInfo disposedProperty && isPropertyValueDisposed(disposedProperty) && !ExpressionObserverOptions.CannotBeDisposed(disposedProperty.PropertyType))
            return new(memberExpression, DirectSubscriptionIneligibility.ValueRequiresDisposal);
        if (memberExpression.Member is PropertyInfo ignoredProperty && isIgnoredPropertyChangeNotification(ignoredProperty))
            return new(memberExpression, DirectSubscriptionIneligibility.IgnoredChangeNotification);
        if (memberExpression.Expression is not { } target)
            return DirectSubscriptionAnalysis.Eligible;
        if (!IsFixed(target))
            return CannotNotify(target.Type) ? AnalyzeNode(target, planner) : new(memberExpression, DirectSubscriptionIneligibility.ChangeableMemberTarget);
        var targetAnalysis = AnalyzeNode(target, planner);
        if (!targetAnalysis.IsEligible || planner is null)
            return targetAnalysis;
        if (memberExpression.Member is PropertyInfo property)
            AddPropertyChangedSubscription(planner, memberExpression, target, DirectSubscriptionKind.MemberPropertyChanged, property.Name);
        else if (memberExpression.Member is FieldInfo && IsCompilerGenerated(target))
            AddContentsSubscription(planner, memberExpression, memberExpression, generatedTypeFieldsListenForDictionaryChanged, generatedTypeFieldsListenForCollectionChanged);
        return targetAnalysis;
    }

    DirectSubscriptionAnalysis AnalyzeMethodCall(MethodCallExpression methodCallExpression, Planner? planner)
    {
        if (!ExpressionObserverOptions.CannotBeDisposed(methodCallExpression.Method.ReturnType))
            return new(methodCallExpression, DirectSubscriptionIneligibility.ValueRequiresDisposal);
        if (methodCallExpression.Object is { } target)
        {
            var targetAnalysis = AnalyzeNode(target, planner);
            if (!targetAnalysis.IsEligible)
                return targetAnalysis;
        }
        for (int i = 0, ii = methodCallExpression.Arguments.Count; i < ii; ++i)
        {
            var argumentAnalysis = AnalyzeNode(methodCallExpression.Arguments[i], planner);
            if (!argumentAnalysis.IsEligible)
                return argumentAnalysis;
        }
        return DirectSubscriptionAnalysis.Eligible;
    }

    DirectSubscriptionAnalysis AnalyzeParameter(ParameterExpression parameterExpression, Planner? planner)
    {
        if (planner is not null)
            AddContentsSubscription(planner, parameterExpression, parameterExpression, constantsListenForDictionaryChanged, constantsListenForCollectionChanged);
        return DirectSubscriptionAnalysis.Eligible;
    }

    DirectSubscriptionAnalysis AnalyzeShortCircuiting(BinaryExpression binaryExpression, Planner? planner)
    {
        if (binaryExpression.Conversion is not null)
            return new(binaryExpression, DirectSubscriptionIneligibility.UnsupportedExpressionKind);
        if (planner is null)
            return new(binaryExpression, DirectSubscriptionIneligibility.DeferredBranch);
        var leftAnalysis = AnalyzeNode(binaryExpression.Left, planner);
        return leftAnalysis.IsEligible ? AnalyzeDeferredOperand(binaryExpression.Right, planner) : leftAnalysis;
    }

    DirectSubscriptionAnalysis AnalyzeNode(Expression expression, Planner? planner) =>
        planner is not null && planner.Reached(expression) ? DirectSubscriptionAnalysis.Eligible : expression switch
        {
            ConstantExpression constantExpression => AnalyzeConstant(constantExpression, planner),
            ParameterExpression parameterExpression => AnalyzeParameter(parameterExpression, planner),
            MemberExpression memberExpression => AnalyzeMember(memberExpression, planner),
            IndexExpression indexExpression => AnalyzeIndex(indexExpression, planner),
            MethodCallExpression methodCallExpressionForPropertyGet when ExpressionObserverOptions.PropertyGetMethodToProperty.GetOrAdd(methodCallExpressionForPropertyGet.Method, ExpressionObserverOptions.GetPropertyFromGetMethod) is { } property => AnalyzeNode(methodCallExpressionForPropertyGet.Arguments.Count > 0 ? Expression.MakeIndex(methodCallExpressionForPropertyGet.Object!, property, methodCallExpressionForPropertyGet.Arguments) : Expression.MakeMemberAccess(methodCallExpressionForPropertyGet.Object, property), planner),
            MethodCallExpression methodCallExpression => AnalyzeMethodCall(methodCallExpression, planner),
            BinaryExpression binaryExpression when binaryExpression.Method is { } binaryOperator && !ExpressionObserverOptions.CannotBeDisposed(binaryOperator.ReturnType) => new(binaryExpression, DirectSubscriptionIneligibility.UserDefinedOperator),
            BinaryExpression binaryExpression when IsShortCircuiting(binaryExpression) => AnalyzeShortCircuiting(binaryExpression, planner),
            BinaryExpression binaryExpression when binaryExpression.Conversion is not null => new(binaryExpression, DirectSubscriptionIneligibility.UnsupportedExpressionKind),
            BinaryExpression binaryExpression => AnalyzeNode(binaryExpression.Left, planner) is { IsEligible: false } left ? left : AnalyzeNode(binaryExpression.Right, planner),
            ConditionalExpression conditionalExpression => AnalyzeConditional(conditionalExpression, planner),
            TypeBinaryExpression typeBinaryExpression when typeBinaryExpression.NodeType is not ExpressionType.TypeAs => AnalyzeNode(typeBinaryExpression.Expression, planner),
            UnaryExpression unaryExpression when unaryExpression.NodeType is ExpressionType.Quote => DirectSubscriptionAnalysis.Eligible,
            UnaryExpression unaryExpression when unaryExpression.Method is { } unaryOperator && !ExpressionObserverOptions.CannotBeDisposed(unaryOperator.ReturnType) => new(unaryExpression, DirectSubscriptionIneligibility.UserDefinedOperator),
            UnaryExpression unaryExpression => AnalyzeNode(unaryExpression.Operand, planner),
            _ => new(expression, DirectSubscriptionIneligibility.UnsupportedExpressionKind)
        };

    /// <summary>
    /// Determines whether the specified expression can be observed by subscribing directly to its change sources and, when it can, which subscriptions that would take
    /// </summary>
    /// <param name="expression">The expression, which may be a lambda body or the parameter-replaced expression derived from one</param>
    public DirectSubscriptionPlan Plan(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var planner = new Planner();
        var analysis = Resolved(expression, planner);
        return analysis.IsEligible ? new(analysis, planner.Subscriptions.ToArray(), planner.DeferredGroups.ToArray()) : new(analysis, null, null);
    }

    DirectSubscriptionAnalysis Resolved(Expression expression, Planner planner)
    {
        var analysis = AnalyzeNode(expression, planner);
        if (!analysis.IsEligible)
            return analysis;
        planner.Resolve();
        return planner.DeferredGroups.Count > 64 ? new(expression, DirectSubscriptionIneligibility.DeferredBranch) : analysis;
    }
}
