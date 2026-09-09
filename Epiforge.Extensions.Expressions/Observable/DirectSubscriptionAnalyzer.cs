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
/// A property read through a target which is not fixed contributes no subscription of its own when no value the target could hold raises a change notification, which is decided by its type being sealed and implementing none of the notification interfaces, exactly as the graph's node for it subscribes to nothing. A target which could notify becomes a link: the target's value is recorded by the evaluation which produces it, and the subscription naming that link attaches to whatever it holds and moves when it changes, which is what the graph's node does when the value it read last is not the value it reads now
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
        internal readonly List<Expression> Links = [];
        internal readonly List<DirectSubscription> Subscriptions = [];

        internal int CurrentGroup;

        internal void Add(Expression owner, DirectSubscription subscription)
        {
            owners.Add(owner);
            Subscriptions.Add(subscription);
        }

        internal void AddLinked(Expression owner, Expression target, string propertyName)
        {
            for (int i = 0, ii = Links.Count; i < ii; ++i)
                if (ReferenceEquals(Links[i], target))
                {
                    Add(owner, new(target, DirectSubscriptionKind.MemberPropertyChanged, propertyName, 0));
                    return;
                }
            Links.Add(target);
            Add(owner, new(target, DirectSubscriptionKind.MemberPropertyChanged, propertyName, 0));
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

        bool IsAncestorOrSelf(int candidate, int group) =>
            NearestCommonAncestor(candidate, group) == candidate;

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

        /// <summary>
        /// Discards a subscription which another names the same event of the same source for, from a group attached no later and never released, since the graph gives one node to an expression however many places name it and attaches that node once; sameness of source is expression equality and not node identity, because two occurrences of one captured or static value are two nodes naming one object; sibling groups keep theirs, neither being attached when the other is
        /// </summary>
        void DiscardRedundant()
        {
            for (var i = Subscriptions.Count - 1; i >= 0; --i)
            {
                var subscription = Subscriptions[i];
                for (var j = 0; j < Subscriptions.Count; ++j)
                {
                    if (j == i)
                        continue;
                    var other = Subscriptions[j];
                    if (!ExpressionEqualityComparer.Default.Equals(other.Source, subscription.Source) || other.Kind != subscription.Kind || other.PropertyName != subscription.PropertyName || !IsAncestorOrSelf(other.DeferredGroup, subscription.DeferredGroup) || other.DeferredGroup == subscription.DeferredGroup && j > i)
                        continue;
                    Subscriptions.RemoveAt(i);
                    owners.RemoveAt(i);
                    break;
                }
            }
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
            DiscardRedundant();
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
        this(Validated(options).ConstantExpressionsListenForCollectionChanged, options.ConstantExpressionsListenForDictionaryChanged, options.MemberExpressionsListenToGeneratedTypesFieldValuesForCollectionChanged, options.MemberExpressionsListenToGeneratedTypesFieldValuesForDictionaryChanged, options.IsIgnoredPropertyChangeNotification, options.IsMethodReturnValueDisposed, options.IsPropertyValueDisposed)
    {
    }

    internal DirectSubscriptionAnalyzer(ExpressionObserver observer) :
        this(observer.ConstantExpressionsListenForCollectionChanged, observer.ConstantExpressionsListenForDictionaryChanged, observer.MemberExpressionsListenToGeneratedTypesFieldValuesForCollectionChanged, observer.MemberExpressionsListenToGeneratedTypesFieldValuesForDictionaryChanged, observer.IsIgnoredPropertyChangeNotification, observer.IsMethodReturnValueDisposed, observer.IsPropertyValueDisposed)
    {
    }

    DirectSubscriptionAnalyzer(bool constantsListenForCollectionChanged, bool constantsListenForDictionaryChanged, bool generatedTypeFieldsListenForCollectionChanged, bool generatedTypeFieldsListenForDictionaryChanged, Func<PropertyInfo, bool> isIgnoredPropertyChangeNotification, Func<MethodInfo, bool> isMethodReturnValueDisposed, Func<PropertyInfo, bool> isPropertyValueDisposed)
    {
        this.constantsListenForCollectionChanged = constantsListenForCollectionChanged;
        this.constantsListenForDictionaryChanged = constantsListenForDictionaryChanged;
        this.generatedTypeFieldsListenForCollectionChanged = generatedTypeFieldsListenForCollectionChanged;
        this.generatedTypeFieldsListenForDictionaryChanged = generatedTypeFieldsListenForDictionaryChanged;
        this.isIgnoredPropertyChangeNotification = isIgnoredPropertyChangeNotification;
        this.isMethodReturnValueDisposed = isMethodReturnValueDisposed;
        this.isPropertyValueDisposed = isPropertyValueDisposed;
    }

    readonly bool constantsListenForCollectionChanged;
    readonly bool constantsListenForDictionaryChanged;
    readonly bool generatedTypeFieldsListenForCollectionChanged;
    readonly bool generatedTypeFieldsListenForDictionaryChanged;
    readonly Func<PropertyInfo, bool> isIgnoredPropertyChangeNotification;
    readonly Func<MethodInfo, bool> isMethodReturnValueDisposed;
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
        {
            if (target.Type.IsValueType && !CannotNotify(target.Type))
                return new(memberExpression, DirectSubscriptionIneligibility.ChangeableMemberTarget);
            var linkAnalysis = AnalyzeNode(target, planner);
            if (!linkAnalysis.IsEligible || planner is null || CannotNotify(target.Type))
                return linkAnalysis;
            if (memberExpression.Member is PropertyInfo linkedProperty)
                planner.AddLinked(memberExpression, target, linkedProperty.Name);
            return linkAnalysis;
        }
        var targetAnalysis = AnalyzeNode(target, planner);
        if (!targetAnalysis.IsEligible || planner is null)
            return targetAnalysis;
        if (memberExpression.Member is PropertyInfo property)
            AddPropertyChangedSubscription(planner, memberExpression, target, DirectSubscriptionKind.MemberPropertyChanged, property.Name);
        else if (memberExpression.Member is FieldInfo && IsCompilerGenerated(target))
            AddContentsSubscription(planner, memberExpression, memberExpression, generatedTypeFieldsListenForDictionaryChanged, generatedTypeFieldsListenForCollectionChanged);
        return targetAnalysis;
    }

    /// <summary>
    /// Determines whether an expression constructing an object and then assigning its members can be observed by subscribing directly to its change sources, and when it can, plans the subscriptions its arguments and assignments take
    /// </summary>
    /// <remarks>
    /// The limits here are the expression graph's limits, deliberately: <c>ObservableMemberInitExpression</c> supports assignment bindings over a reference type and throws for anything else, so admitting more here would make a shape which works one way and throws the other. Where the graph grows, this grows with it
    /// </remarks>
    DirectSubscriptionAnalysis AnalyzeMemberInit(MemberInitExpression memberInitExpression, Planner? planner)
    {
        if (memberInitExpression.Type.IsValueType)
            return new(memberInitExpression, DirectSubscriptionIneligibility.UnsupportedExpressionKind);
        var newAnalysis = AnalyzeNew(memberInitExpression.NewExpression, planner);
        if (!newAnalysis.IsEligible)
            return newAnalysis;
        var bindings = memberInitExpression.Bindings;
        for (int i = 0, ii = bindings.Count; i < ii; ++i)
        {
            if (bindings[i] is not MemberAssignment memberAssignment)
                return new(memberInitExpression, DirectSubscriptionIneligibility.UnsupportedExpressionKind);
            var bindingAnalysis = AnalyzeNode(memberAssignment.Expression, planner);
            if (!bindingAnalysis.IsEligible)
                return bindingAnalysis;
        }
        return DirectSubscriptionAnalysis.Eligible;
    }

    /// <summary>
    /// Determines whether an expression invoking a method can be observed by subscribing directly to its change sources, and when it can, plans the subscriptions its target and arguments take
    /// </summary>
    /// <remarks>
    /// A method is refused when the graph would dispose of what it returned, which is the same question asked of a property read: what only the graph does, only the graph may be asked to do. Both terms are needed. A return type sealed and implementing neither disposal interface can never be disposed by anyone, whatever the options say, and a return type which could be disposed is only ever disposed of when the observer has been told to dispose of that method's return values, whether by registration or by <c>DisposeWhenDiscardedAttribute</c> on the return parameter. Note that the observer disposes of every static method's return value unless told otherwise, so a static method returning an unsealed type stays refused under the default options
    /// </remarks>
    DirectSubscriptionAnalysis AnalyzeMethodCall(MethodCallExpression methodCallExpression, Planner? planner)
    {
        if (!ExpressionObserverOptions.CannotBeDisposed(methodCallExpression.Method.ReturnType) && isMethodReturnValueDisposed(methodCallExpression.Method))
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

    /// <summary>
    /// Determines whether an expression constructing an object can be observed by subscribing directly to its change sources, and when it can, plans the subscriptions its arguments take
    /// </summary>
    /// <remarks>
    /// A constructor is not a method: its value is of the constructed type exactly and never of a type derived from it, so the sealed test which a method's declared return type requires does not apply. What is left is whether the type implements either disposal interface, because the options can be told to dispose what an expression constructed and a value of a type implementing neither cannot be disposed by anyone, whatever the options say
    /// </remarks>
    DirectSubscriptionAnalysis AnalyzeNew(NewExpression newExpression, Planner? planner)
    {
        if (ExpressionObserverOptions.IsDisposable(newExpression.Type))
            return new(newExpression, DirectSubscriptionIneligibility.ValueRequiresDisposal);
        for (int i = 0, ii = newExpression.Arguments.Count; i < ii; ++i)
        {
            var argumentAnalysis = AnalyzeNode(newExpression.Arguments[i], planner);
            if (!argumentAnalysis.IsEligible)
                return argumentAnalysis;
        }
        return DirectSubscriptionAnalysis.Eligible;
    }

    /// <summary>
    /// Determines whether an expression building an array from its elements can be observed by subscribing directly to its change sources, and when it can, plans the subscriptions those elements take
    /// </summary>
    /// <remarks>
    /// An array built from bounds rather than from elements is left ineligible, because the graph names its node for initialization alone and a shape neither mechanism is known to share is not parity
    /// </remarks>
    DirectSubscriptionAnalysis AnalyzeNewArray(NewArrayExpression newArrayExpression, Planner? planner)
    {
        if (newArrayExpression.NodeType is not ExpressionType.NewArrayInit)
            return new(newArrayExpression, DirectSubscriptionIneligibility.UnsupportedExpressionKind);
        var expressions = newArrayExpression.Expressions;
        for (int i = 0, ii = expressions.Count; i < ii; ++i)
        {
            var elementAnalysis = AnalyzeNode(expressions[i], planner);
            if (!elementAnalysis.IsEligible)
                return elementAnalysis;
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
            MemberInitExpression memberInitExpression => AnalyzeMemberInit(memberInitExpression, planner),
            IndexExpression indexExpression => AnalyzeIndex(indexExpression, planner),
            MethodCallExpression methodCallExpressionForPropertyGet when ExpressionObserverOptions.PropertyGetMethodToProperty.GetOrAdd(methodCallExpressionForPropertyGet.Method, ExpressionObserverOptions.GetPropertyFromGetMethod) is { } property => AnalyzeNode(methodCallExpressionForPropertyGet.Arguments.Count > 0 ? Expression.MakeIndex(methodCallExpressionForPropertyGet.Object!, property, methodCallExpressionForPropertyGet.Arguments) : Expression.MakeMemberAccess(methodCallExpressionForPropertyGet.Object, property), planner),
            MethodCallExpression methodCallExpression => AnalyzeMethodCall(methodCallExpression, planner),
            NewExpression newExpression => AnalyzeNew(newExpression, planner),
            NewArrayExpression newArrayExpression => AnalyzeNewArray(newArrayExpression, planner),
            BinaryExpression binaryExpression when binaryExpression.Method is { } binaryOperator && !ExpressionObserverOptions.CannotBeDisposed(binaryOperator.ReturnType) && isMethodReturnValueDisposed(binaryOperator) => new(binaryExpression, DirectSubscriptionIneligibility.UserDefinedOperator),
            BinaryExpression binaryExpression when IsShortCircuiting(binaryExpression) => AnalyzeShortCircuiting(binaryExpression, planner),
            BinaryExpression binaryExpression when binaryExpression.Conversion is not null => new(binaryExpression, DirectSubscriptionIneligibility.UnsupportedExpressionKind),
            BinaryExpression binaryExpression => AnalyzeNode(binaryExpression.Left, planner) is { IsEligible: false } left ? left : AnalyzeNode(binaryExpression.Right, planner),
            ConditionalExpression conditionalExpression => AnalyzeConditional(conditionalExpression, planner),
            TypeBinaryExpression typeBinaryExpression when typeBinaryExpression.NodeType is not ExpressionType.TypeAs => AnalyzeNode(typeBinaryExpression.Expression, planner),
            UnaryExpression unaryExpression when unaryExpression.NodeType is ExpressionType.Quote => DirectSubscriptionAnalysis.Eligible,
            UnaryExpression unaryExpression when unaryExpression.Method is { } unaryOperator && !ExpressionObserverOptions.CannotBeDisposed(unaryOperator.ReturnType) && isMethodReturnValueDisposed(unaryOperator) => new(unaryExpression, DirectSubscriptionIneligibility.UserDefinedOperator),
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
        return analysis.IsEligible ? new(analysis, planner.Subscriptions.ToArray(), planner.DeferredGroups.ToArray(), planner.Links.ToArray()) : new(analysis, null, null, null);
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
