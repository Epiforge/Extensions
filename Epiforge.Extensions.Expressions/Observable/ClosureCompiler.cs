#if IS_NET_8_0_OR_GREATER
namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Turns a lambda expression into a tree of closures which evaluate it on boxed values, for runtimes which cannot generate code and would otherwise interpret the lambda
/// </summary>
/// <remarks>
/// The interpreter of <see cref="System.Linq.Expressions"/> allocates a frame on every call and reaches every member through reflection that cannot be generated either; a closure tree allocates nothing of its own beyond boxing a value-type result, and reaches members through invokers resolved once. It covers the nodes the libraries' own lambdas are built from, and refuses anything else by returning <see langword="null"/>, in which case the caller compiles the lambda as before. Every primitive conversion and operator is the C# cast or operator that compiles to the instruction the expression compiler emits, so wrapping, overflow and division by zero agree with a compiled lambda
/// </remarks>
static class ClosureCompiler
{
    /// <summary>
    /// Compiles the lambda into closures, or returns <see langword="null"/> when it contains a node the closures do not cover
    /// </summary>
    internal static ClosureLambda? TryCompile(LambdaExpression lambda) =>
        TryCompile(lambda.Body, lambda.Parameters);

    /// <summary>
    /// Compiles a body over the specified parameters into closures, or returns <see langword="null"/> when it contains a node the closures do not cover
    /// </summary>
    internal static ClosureLambda? TryCompile(Expression body, IReadOnlyList<ParameterExpression> parameters)
    {
        var scope = new Scope();
        foreach (var parameter in parameters)
            if (parameter.IsByRef || !scope.Declare(parameter))
                return null;
        return Compile(body, scope) is { } compiled ? new ClosureLambda(compiled, scope.SlotCount) : null;
    }

    static bool CannotBeAnObject(Type type) =>
        type.IsByRef || type.IsPointer || type.IsByRefLike;

    static ClosureNode? Compile(Expression? expression, Scope scope)
    {
        if (expression is null || CannotBeAnObject(expression.Type))
            return null;
        return expression switch
        {
            ConstantExpression constant => new ConstantNode(constant.Value),
            ParameterExpression parameter => scope.SlotOf(parameter) is var slot && slot >= 0 ? new SlotNode(slot) : null,
            DefaultExpression @default => @default.Type == typeof(void) ? new ConstantNode(null) : DefaultOf(@default.Type),
            MemberExpression member => CompileMember(member, scope),
            MethodCallExpression call => CompileCall(call, scope),
            IndexExpression index => CompileIndex(index, scope),
            NewExpression @new => CompileNew(@new, scope),
            MemberInitExpression memberInit => CompileMemberInit(memberInit, scope),
            NewArrayExpression newArray when newArray.NodeType is ExpressionType.NewArrayInit => CompileNewArray(newArray, scope),
            ConditionalExpression conditional => Compile(conditional.Test, scope) is { } test && Compile(conditional.IfTrue, scope) is { } ifTrue && Compile(conditional.IfFalse, scope) is { } ifFalse ? new ConditionalNode(test, ifTrue, ifFalse) : null,
            BlockExpression block => CompileBlock(block, scope),
            TypeBinaryExpression typeBinary => Compile(typeBinary.Expression, scope) is { } operand ? new TypeTestNode(operand, typeBinary.TypeOperand, typeBinary.NodeType is ExpressionType.TypeEqual) : null,
            TryExpression @try => CompileTry(@try, scope),
            InvocationExpression { Expression: LambdaExpression invoked } invocation => CompileInvocation(invoked, invocation.Arguments, scope),
            UnaryExpression unary => CompileUnary(unary, scope),
            BinaryExpression binary => CompileBinary(binary, scope),
            _ => null
        };
    }

    static ClosureNode[]? CompileAll(IReadOnlyList<Expression> expressions, Scope scope)
    {
        var nodes = new ClosureNode[expressions.Count];
        for (var i = 0; i < nodes.Length; ++i)
            if (Compile(expressions[i], scope) is { } node)
                nodes[i] = node;
            else
                return null;
        return nodes;
    }

    static ClosureNode? CompileBinary(BinaryExpression binary, Scope scope)
    {
        switch (binary.NodeType)
        {
            case ExpressionType.Assign:
                return CompileAssign(binary, scope);
            case ExpressionType.AndAlso or ExpressionType.OrElse:
                if (binary.Method is not null || binary.Type != typeof(bool))
                    return null;
                return Compile(binary.Left, scope) is { } shortLeft && Compile(binary.Right, scope) is { } shortRight ? new ShortCircuitNode(shortLeft, shortRight, binary.NodeType is ExpressionType.AndAlso) : null;
            case ExpressionType.Coalesce:
                if (binary.Conversion is not null)
                    return null;
                return Compile(binary.Left, scope) is { } coalesceLeft && Compile(binary.Right, scope) is { } coalesceRight ? new CoalesceNode(coalesceLeft, coalesceRight) : null;
            case ExpressionType.ArrayIndex:
                return Compile(binary.Left, scope) is { } array && Compile(binary.Right, scope) is { } arrayIndex ? new ArrayIndexNode(array, arrayIndex) : null;
        }
        if (Compile(binary.Left, scope) is not { } left || Compile(binary.Right, scope) is not { } right)
            return null;
        var lifted = binary.IsLifted;
        var comparison = binary.NodeType is ExpressionType.Equal or ExpressionType.NotEqual or ExpressionType.LessThan or ExpressionType.LessThanOrEqual or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual;
        if (binary.Method is { } method)
        {
            if (method.IsGenericMethodDefinition || Array.Exists(method.GetParameters(), parameter => CannotBeAnObject(parameter.ParameterType)) || CannotBeAnObject(method.ReturnType))
                return null;
            return new LiftedBinaryNode(left, right, new MethodBinaryOperation(MethodInvoker.Create(method)).Apply, lifted, comparison, binary.NodeType, binary.IsLiftedToNull, false);
        }
        var operandType = Nullable.GetUnderlyingType(binary.Left.Type) ?? binary.Left.Type;
        Func<object?, object?, object?>? operation = null;
        if (operandType.IsPrimitive)
            operation = ClosureOperations.Binary(binary.NodeType, Type.GetTypeCode(operandType));
        else if (binary.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
        {
            var equal = binary.NodeType is ExpressionType.Equal;
            if (!operandType.IsValueType)
                operation = equal ? static (l, r) => BooleanBoxes.Box(ReferenceEquals(l, r)) : static (l, r) => BooleanBoxes.Box(!ReferenceEquals(l, r));
            else if (operandType.IsEnum)
                operation = equal ? static (l, r) => BooleanBoxes.Box(l!.Equals(r)) : static (l, r) => BooleanBoxes.Box(!l!.Equals(r));
        }
        if (operation is null)
            return null;
        if (!operandType.IsValueType)
            return new ReferenceComparisonNode(left, right, operation);
        return new LiftedBinaryNode(left, right, operation, lifted, comparison, binary.NodeType, binary.IsLiftedToNull, operandType == typeof(bool) && binary.NodeType is ExpressionType.And or ExpressionType.Or);
    }

    static ClosureNode? CompileAssign(BinaryExpression assign, Scope scope)
    {
        if (Compile(assign.Right, scope) is not { } value)
            return null;
        switch (assign.Left)
        {
            case ParameterExpression variable when scope.SlotOf(variable) is var slot && slot >= 0:
                return new AssignSlotNode(slot, value);
            case IndexExpression { Indexer: null, Arguments.Count: 1 } element:
                return Compile(element.Object, scope) is { } array && Compile(element.Arguments[0], scope) is { } index ? new AssignElementNode(array, index, value) : null;
            default:
                return null;
        }
    }

    static ClosureNode? CompileBlock(BlockExpression block, Scope scope)
    {
        var variableSlots = new int[block.Variables.Count];
        var variableDefaults = new object?[variableSlots.Length];
        for (var i = 0; i < variableSlots.Length; ++i)
        {
            var variable = block.Variables[i];
            if (variable.IsByRef || CannotBeAnObject(variable.Type) || !scope.Declare(variable))
                return null;
            variableSlots[i] = scope.SlotOf(variable);
            variableDefaults[i] = variable.Type.IsValueType && Nullable.GetUnderlyingType(variable.Type) is null ? variable.Type.FastDefault() : null;
        }
        return CompileAll(block.Expressions, scope) is { } expressions ? new BlockNode(variableSlots, variableDefaults, expressions, block.Type == typeof(void)) : null;
    }

    static ClosureNode? CompileCall(MethodCallExpression call, Scope scope)
    {
        var method = call.Method;
        if (method.ContainsGenericParameters || CannotBeAnObject(method.ReturnType) || Array.Exists(method.GetParameters(), parameter => CannotBeAnObject(parameter.ParameterType)))
            return null;
        ClosureNode? instance = null;
        if (!method.IsStatic)
        {
            if (call.Object is not { } target || Compile(target, scope) is not { } compiledTarget)
                return null;
            if (Nullable.GetUnderlyingType(target.Type) is not null)
                return method.Name == nameof(Nullable<int>.GetValueOrDefault) && call.Arguments.Count == 0 ? new NullableValueNode(compiledTarget, DefaultOf(Nullable.GetUnderlyingType(target.Type)!)) : null;
            instance = compiledTarget;
        }
        return CompileAll(call.Arguments, scope) is { } arguments ? new InvocationNode(instance, MethodInvoker.Create(method), arguments) : null;
    }

    static ClosureNode? CompileIndex(IndexExpression index, Scope scope)
    {
        if (Compile(index.Object, scope) is not { } target || CompileAll(index.Arguments, scope) is not { } arguments)
            return null;
        if (index.Indexer is { } indexer)
            return indexer.GetMethod is { } getter ? new InvocationNode(target, MethodInvoker.Create(getter), arguments) : null;
        return arguments.Length == 1 ? new ArrayIndexNode(target, arguments[0]) : null;
    }

    static ClosureNode? CompileInvocation(LambdaExpression lambda, IReadOnlyList<Expression> arguments, Scope scope)
    {
        if (CompileAll(arguments, scope) is not { } values)
            return null;
        var slots = new int[lambda.Parameters.Count];
        for (var i = 0; i < slots.Length; ++i)
        {
            var parameter = lambda.Parameters[i];
            if (parameter.IsByRef || !scope.Declare(parameter))
                return null;
            slots[i] = scope.SlotOf(parameter);
        }
        return Compile(lambda.Body, scope) is { } body ? new InlinedInvocationNode(slots, values, body) : null;
    }

    static ClosureNode? CompileMember(MemberExpression member, Scope scope)
    {
        ClosureNode? instance = null;
        if (member.Expression is { } target)
        {
            if (Compile(target, scope) is not { } compiledTarget)
                return null;
            if (Nullable.GetUnderlyingType(target.Type) is { } underlying)
                return member.Member.Name switch
                {
                    nameof(Nullable<int>.HasValue) => new HasValueNode(compiledTarget),
                    nameof(Nullable<int>.Value) => new NullableValueNode(compiledTarget, null),
                    _ => null
                };
            instance = compiledTarget;
        }
        return member.Member switch
        {
            FieldInfo { IsStatic: true, IsInitOnly: true } field => new ConstantNode(field.GetValue(null)),
            FieldInfo field => new FieldNode(instance, field),
            PropertyInfo { GetMethod: not null } property => new PropertyNode(instance, property),
            _ => null
        };
    }

    static ClosureNode? CompileMemberInit(MemberInitExpression memberInit, Scope scope)
    {
        if (CompileNew(memberInit.NewExpression, scope) is not { } @new)
            return null;
        var bindings = new (FieldInfo? Field, MethodInvoker? Setter, ClosureNode Value)[memberInit.Bindings.Count];
        for (var i = 0; i < bindings.Length; ++i)
        {
            if (memberInit.Bindings[i] is not MemberAssignment assignment || Compile(assignment.Expression, scope) is not { } value)
                return null;
            switch (assignment.Member)
            {
                case FieldInfo field:
                    bindings[i] = (field, null, value);
                    break;
                case PropertyInfo { SetMethod: { } setter }:
                    bindings[i] = (null, MethodInvoker.Create(setter), value);
                    break;
                default:
                    return null;
            }
        }
        return new MemberInitNode(@new, bindings);
    }

    static ClosureNode? CompileNew(NewExpression @new, Scope scope)
    {
        if (@new.Constructor is not { } constructor)
            return DefaultOf(@new.Type);
        if (constructor.DeclaringType is not { IsAbstract: false } || constructor.ContainsGenericParameters || Array.Exists(constructor.GetParameters(), parameter => CannotBeAnObject(parameter.ParameterType)))
            return null;
        return CompileAll(@new.Arguments, scope) is { } arguments ? new ConstructionNode(ConstructorInvoker.Create(constructor), arguments) : null;
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The array type is the type of a node of the caller's expression, so the caller's code already needed it")]
    static ClosureNode? CompileNewArray(NewArrayExpression newArray, Scope scope)
    {
        var elementType = newArray.Type.GetElementType()!;
        if (CannotBeAnObject(elementType))
            return null;
        return CompileAll(newArray.Expressions, scope) is { } elements ? new NewArrayNode(elementType, elements) : null;
    }

    static ClosureNode? CompileTry(TryExpression @try, Scope scope)
    {
        if (@try.Fault is not null || Compile(@try.Body, scope) is not { } body)
            return null;
        var handlers = new (Type Test, int Slot, ClosureNode Body)[@try.Handlers.Count];
        for (var i = 0; i < handlers.Length; ++i)
        {
            var handler = @try.Handlers[i];
            if (handler.Filter is not null)
                return null;
            var slot = -1;
            if (handler.Variable is { } variable)
            {
                if (!scope.Declare(variable))
                    return null;
                slot = scope.SlotOf(variable);
            }
            if (Compile(handler.Body, scope) is not { } handlerBody)
                return null;
            handlers[i] = (handler.Test, slot, handlerBody);
        }
        ClosureNode? @finally = null;
        if (@try.Finally is { } finallyExpression && (@finally = Compile(finallyExpression, scope)) is null)
            return null;
        return new TryNode(body, handlers, @finally, @try.Type == typeof(void));
    }

    static ClosureNode? CompileUnary(UnaryExpression unary, Scope scope)
    {
        if (unary.NodeType is ExpressionType.Quote)
            return unary.Operand is LambdaExpression quoted && !ReferencesParameters(quoted) ? new ConstantNode(quoted) : null;
        if (unary.NodeType is ExpressionType.Throw)
        {
            if (unary.Operand is null)
                return new RethrowNode();
            return Compile(unary.Operand, scope) is { } thrown ? new ThrowNode(thrown) : null;
        }
        if (Compile(unary.Operand, scope) is not { } operand)
            return null;
        var from = unary.Operand.Type;
        var to = unary.Type;
        if (unary.Method is { } method)
        {
            if (method.ContainsGenericParameters || CannotBeAnObject(method.ReturnType) || Array.Exists(method.GetParameters(), parameter => CannotBeAnObject(parameter.ParameterType)))
                return null;
            return new LiftedUnaryNode(operand, new MethodUnaryOperation(MethodInvoker.Create(method)).Apply, unary.IsLifted, Nullable.GetUnderlyingType(to) is not null || !to.IsValueType);
        }
        switch (unary.NodeType)
        {
            case ExpressionType.Convert or ExpressionType.ConvertChecked:
                return CompileConversion(operand, from, to, unary.NodeType is ExpressionType.ConvertChecked);
            case ExpressionType.Unbox:
                return new CastNode(operand, new BoxCast(to));
            case ExpressionType.TypeAs:
                return new TypeAsNode(operand, to);
            case ExpressionType.UnaryPlus:
                return operand;
            case ExpressionType.ArrayLength:
                return new ArrayLengthNode(operand);
            case ExpressionType.IsTrue or ExpressionType.IsFalse when (Nullable.GetUnderlyingType(from) ?? from) == typeof(bool):
                return new LiftedUnaryNode(operand, unary.NodeType is ExpressionType.IsTrue ? static value => value : static value => BooleanBoxes.Box(!(bool)value!), Nullable.GetUnderlyingType(from) is not null, true);
            case ExpressionType.Negate or ExpressionType.NegateChecked or ExpressionType.Not or ExpressionType.OnesComplement:
                var operandType = Nullable.GetUnderlyingType(from) ?? from;
                if (!operandType.IsPrimitive || ClosureOperations.Unary(unary.NodeType, Type.GetTypeCode(operandType)) is not { } operation)
                    return null;
                return new LiftedUnaryNode(operand, operation, Nullable.GetUnderlyingType(from) is not null, true);
            default:
                return null;
        }
    }

    static ClosureNode? CompileConversion(ClosureNode operand, Type from, Type to, bool @checked)
    {
        if (from == to)
            return operand;
        if (!to.IsValueType)
            return from.IsValueType || to.IsAssignableFrom(from) ? operand : new CastNode(operand, new BoxCast(to));
        if (!from.IsValueType)
            return new CastNode(operand, new BoxCast(to));
        var fromUnderlying = Nullable.GetUnderlyingType(from);
        var toUnderlying = Nullable.GetUnderlyingType(to);
        var fromValue = fromUnderlying ?? from;
        var toValue = toUnderlying ?? to;
        Func<object?, object?>? conversion;
        if (fromValue == toValue)
            conversion = null;
        else
        {
            var fromCode = Type.GetTypeCode(fromValue);
            var toCode = Type.GetTypeCode(toValue);
            if (!IsNumeric(fromCode) || !IsNumeric(toCode) || ClosureOperations.Conversion(fromCode, toCode, @checked) is not { } numeric)
                return null;
            conversion = toValue.IsEnum ? value => Enum.ToObject(toValue, numeric(value)!) : numeric;
        }
        return new ValueConversionNode(operand, conversion, fromUnderlying is not null, toUnderlying is not null);
    }

    static ClosureNode DefaultOf(Type type) =>
        new ConstantNode(type.IsValueType && Nullable.GetUnderlyingType(type) is null ? type.FastDefault() : null);

    static bool IsNumeric(TypeCode typeCode) =>
        typeCode is >= TypeCode.Char and <= TypeCode.Double;

    static bool ReferencesParameters(LambdaExpression lambda)
    {
        var finder = new FreeParameterFinder(lambda);
        finder.Visit(lambda.Body);
        return finder.Found;
    }

    sealed class FreeParameterFinder(LambdaExpression lambda) :
        ExpressionVisitor
    {
        readonly HashSet<ParameterExpression> declared = [.. lambda.Parameters];

        internal bool Found { get; private set; }

        protected override Expression VisitBlock(BlockExpression node)
        {
            declared.UnionWith(node.Variables);
            return base.VisitBlock(node);
        }

        protected override CatchBlock VisitCatchBlock(CatchBlock node)
        {
            if (node.Variable is { } variable)
                declared.Add(variable);
            return base.VisitCatchBlock(node);
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            declared.UnionWith(node.Parameters);
            return base.VisitLambda(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (!declared.Contains(node))
                Found = true;
            return node;
        }
    }

    sealed class Scope
    {
        readonly Dictionary<ParameterExpression, int> slots = [];

        internal int SlotCount =>
            slots.Count;

        internal bool Declare(ParameterExpression parameter) =>
            slots.TryAdd(parameter, slots.Count);

        internal int SlotOf(ParameterExpression parameter) =>
            slots.TryGetValue(parameter, out var slot) ? slot : -1;
    }

    sealed class MethodBinaryOperation(MethodInvoker invoker)
    {
        internal object? Apply(object? left, object? right) =>
            invoker.Invoke(null, left, right);
    }

    sealed class MethodUnaryOperation(MethodInvoker invoker)
    {
        internal object? Apply(object? operand) =>
            invoker.Invoke(null, operand);
    }
}
#endif
