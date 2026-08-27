namespace Epiforge.Extensions.Expressions;

static class ExpressionDiagramGenerator
{
    sealed class ObservedArgument
    {
        internal ObservedArgument(object? value) =>
            this.value = value;

        readonly object? value;

        public override bool Equals(object? obj) =>
            obj is ObservedArgument other && ReferenceEquals(other.value, value);

        public override int GetHashCode() =>
            value is null ? 0 : RuntimeHelpers.GetHashCode(value);
    }

    class IterationState
    {
        Dictionary<ParameterExpression, (int set, int index)>? parameters;

        public bool HasParameters =>
            parameters is not null;

        public Dictionary<ParameterExpression, (int set, int index)> Parameters =>
            parameters ??= [];

        public int ParameterSet { get; set; } = -1;
    }

    static readonly object boxedFalse = false;
    static readonly object boxedTrue = true;
    static readonly object[] boxedNodeTypes = CreateBoxedNodeTypes();

    static object[] CreateBoxedNodeTypes()
    {
        var nodeTypes = (ExpressionType[])Enum.GetValues(typeof(ExpressionType));
        var largest = 0;
        for (int i = 0, ii = nodeTypes.Length; i < ii; ++i)
            largest = Math.Max(largest, (int)nodeTypes[i]);
        var boxes = new object[largest + 1];
        for (int i = 0, ii = nodeTypes.Length; i < ii; ++i)
            boxes[(int)nodeTypes[i]] = nodeTypes[i];
        return boxes;
    }

    static object Box(bool value) =>
        value ? boxedTrue : boxedFalse;

    static object Box(ExpressionType nodeType) =>
        boxedNodeTypes[(int)nodeType];

    static readonly ConditionalWeakTable<ConstantExpression, object> constantsSubstitutedForArguments = [];
    static readonly object constantSubstitutedForArgument = new();

    internal static void NoteConstantSubstitutedForArgument(ConstantExpression constantExpression) =>
        constantsSubstitutedForArguments.AddOrUpdate(constantExpression, constantSubstitutedForArgument);

    public static IReadOnlyList<object?> GenerateDiagram(Expression? node)
    {
        var diagram = new List<object?>();
        GenerateDiagram(node, new IterationState(), diagram);
        return diagram;
    }

    static void GenerateDiagram(MemberBinding binding, IterationState iterationState, List<object?> diagram)
    {
        diagram.Add(binding.BindingType);
        diagram.Add(binding.Member);
        if (binding is MemberAssignment memberAssignment)
            GenerateDiagram(memberAssignment.Expression, iterationState, diagram);
        else if (binding is MemberListBinding memberListBinding)
            foreach (var initializer in memberListBinding.Initializers)
            {
                diagram.Add(initializer.AddMethod);
                foreach (var argument in initializer.Arguments)
                    GenerateDiagram(argument, iterationState, diagram);
            }
        else if (binding is MemberMemberBinding memberMemberBinding)
            foreach (var nestedBinding in memberMemberBinding.Bindings)
                GenerateDiagram(nestedBinding, iterationState, diagram);
    }

    [SuppressMessage("Maintainability", "CA1502: Avoid excessive complexity")]
    [SuppressMessage("Code Analysis", "CA1506: Avoid excessive class coupling")]
    static void GenerateDiagram(Expression? node, IterationState iterationState, List<object?> diagram)
    {
        if (node is null)
        {
            diagram.Add(null);
            return;
        }
        diagram.Add(Box(node.CanReduce));
        diagram.Add(Box(node.NodeType));
        diagram.Add(node.Type);
        if (node is BinaryExpression binary)
        {
            diagram.Add(Box(binary.IsLifted));
            diagram.Add(Box(binary.IsLiftedToNull));
            diagram.Add(binary.Method);
            GenerateDiagram(binary.Left, iterationState, diagram);
            GenerateDiagram(binary.Right, iterationState, diagram);
            return;
        }
        if (node is BlockExpression block)
        {
            ++iterationState.ParameterSet;
            var blockVariables = block.Variables;
            for (int i = 0, ii = blockVariables.Count; i < ii; ++i)
                iterationState.Parameters[blockVariables[i]] = (iterationState.ParameterSet, i);
            foreach (var expression in block.Expressions)
                GenerateDiagram(expression, iterationState, diagram);
            foreach (var variable in block.Variables)
                GenerateDiagram(variable, iterationState, diagram);
        }
        if (node is ConditionalExpression conditional)
        {
            GenerateDiagram(conditional.Test, iterationState, diagram);
            GenerateDiagram(conditional.IfTrue, iterationState, diagram);
            GenerateDiagram(conditional.IfFalse, iterationState, diagram);
            return;
        }
        if (node is ConstantExpression constant)
        {
            if (constantsSubstitutedForArguments.TryGetValue(constant, out _))
                diagram.Add(new ObservedArgument(constant.Value));
            else
                diagram.Add(constant.Value);
            return;
        }
        if (node is DebugInfoExpression debugInfo)
        {
            diagram.Add(debugInfo.Document.DocumentType);
            diagram.Add(debugInfo.Document.FileName);
            diagram.Add(debugInfo.Document.Language);
            diagram.Add(debugInfo.Document.LanguageVendor);
            diagram.Add(debugInfo.EndColumn);
            diagram.Add(debugInfo.EndLine);
            diagram.Add(Box(debugInfo.IsClear));
            diagram.Add(debugInfo.StartColumn);
            diagram.Add(debugInfo.StartLine);
            return;
        }
        if (node is DynamicExpression dynamic)
        {
            diagram.Add(dynamic.Binder);
            diagram.Add(dynamic.DelegateType);
            foreach (var argument in dynamic.Arguments)
                GenerateDiagram(argument, iterationState, diagram);
            return;
        }
        if (node is GotoExpression @goto)
        {
            diagram.Add(@goto.Kind);
            diagram.Add(@goto.Target.Name);
            diagram.Add(@goto.Target.Type);
            GenerateDiagram(@goto.Value, iterationState, diagram);
            return;
        }
        if (node is IndexExpression index)
        {
            GenerateDiagram(index.Object, iterationState, diagram);
            diagram.Add(index.Indexer);
            foreach (var argument in index.Arguments)
                GenerateDiagram(argument, iterationState, diagram);
            return;
        }
        if (node is InvocationExpression invocation)
        {
            GenerateDiagram(invocation.Expression, iterationState, diagram);
            foreach (var argument in invocation.Arguments)
                GenerateDiagram(argument, iterationState, diagram);
            return;
        }
        if (node is LabelExpression label)
        {
            diagram.Add(label.Target.Name);
            diagram.Add(label.Target.Type);
            GenerateDiagram(label.DefaultValue, iterationState, diagram);
            return;
        }
        if (node is LambdaExpression lambda)
        {
            ++iterationState.ParameterSet;
            var lambdaParameters = lambda.Parameters;
            for (int i = 0, ii = lambdaParameters.Count; i < ii; ++i)
                iterationState.Parameters[lambdaParameters[i]] = (iterationState.ParameterSet, i);
            diagram.Add(lambda.Name);
            diagram.Add(lambda.ReturnType);
            diagram.Add(Box(lambda.TailCall));
            GenerateDiagram(lambda.Body, iterationState, diagram);
            foreach (var lambdaParameter in lambdaParameters)
                GenerateDiagram(lambdaParameter, iterationState, diagram);
            return;
        }
        if (node is ListInitExpression listInit)
        {
            GenerateDiagram(listInit.NewExpression, iterationState, diagram);
            foreach (var initializer in listInit.Initializers)
            {
                diagram.Add(initializer.AddMethod);
                foreach (var argument in initializer.Arguments)
                    GenerateDiagram(argument, iterationState, diagram);
            }
            return;
        }
        if (node is LoopExpression loop)
        {
            diagram.Add(loop.BreakLabel?.Name);
            diagram.Add(loop.BreakLabel?.Type);
            diagram.Add(loop.ContinueLabel?.Name);
            diagram.Add(loop.ContinueLabel?.Type);
            GenerateDiagram(loop.Body, iterationState, diagram);
            return;
        }
        if (node is MemberExpression member)
        {
            GenerateDiagram(member.Expression, iterationState, diagram);
            diagram.Add(member.Member);
            return;
        }
        if (node is MemberInitExpression memberInit)
        {
            GenerateDiagram(memberInit.NewExpression, iterationState, diagram);
            foreach (var binding in memberInit.Bindings)
                GenerateDiagram(binding, iterationState, diagram);
            return;
        }
        if (node is MethodCallExpression methodCall)
        {
            GenerateDiagram(methodCall.Object, iterationState, diagram);
            diagram.Add(methodCall.Method);
            foreach (var argument in methodCall.Arguments)
                GenerateDiagram(argument, iterationState, diagram);
            return;
        }
        if (node is NewArrayExpression newArray)
        {
            foreach (var expression in newArray.Expressions)
                GenerateDiagram(expression, iterationState, diagram);
            return;
        }
        if (node is NewExpression @new)
        {
            diagram.Add(@new.Constructor);
            foreach (var argument in @new.Arguments)
                GenerateDiagram(argument, iterationState, diagram);
            if (@new.Members is { } members)
                foreach (var newMember in members) // this line is not covered by tests
                    diagram.Add(newMember); // this line is not covered by tests
            return;
        }
        if (node is ParameterExpression parameter)
        {
            if (iterationState.HasParameters && iterationState.Parameters.TryGetValue(parameter, out var parameterPosition))
            {
                diagram.Add(parameterPosition.set);
                diagram.Add(parameterPosition.index);
            }
            else
                diagram.Add(parameter);
            return;
        }
        if (node is RuntimeVariablesExpression runtimeVariables)
        {
            ++iterationState.ParameterSet;
            var runtimeVariablesVariables = runtimeVariables.Variables;
            for (int i = 0, ii = runtimeVariablesVariables.Count; i < ii; ++i)
                iterationState.Parameters[runtimeVariablesVariables[i]] = (iterationState.ParameterSet, i);
            foreach (var variable in runtimeVariables.Variables)
                GenerateDiagram(variable, iterationState, diagram);
            return;
        }
        if (node is SwitchExpression @switch)
        {
            diagram.Add(@switch.Comparison);
            foreach (var switchCase in @switch.Cases)
            {
                foreach (var testValue in switchCase.TestValues)
                    GenerateDiagram(testValue, iterationState, diagram);
                GenerateDiagram(switchCase.Body, iterationState, diagram);
            }
            GenerateDiagram(@switch.DefaultBody, iterationState, diagram);
            return;
        }
        if (node is TryExpression @try)
        {
            GenerateDiagram(@try.Body, iterationState, diagram);
            foreach (var handler in @try.Handlers)
            {
                diagram.Add(handler.Test);
                GenerateDiagram(handler.Filter, iterationState, diagram);
                GenerateDiagram(handler.Variable, iterationState, diagram);
                GenerateDiagram(handler.Body, iterationState, diagram);
            }
            GenerateDiagram(@try.Fault, iterationState, diagram);
            GenerateDiagram(@try.Finally, iterationState, diagram);
            return;
        }
        if (node is TypeBinaryExpression typeBinary)
        {
            GenerateDiagram(typeBinary.Expression, iterationState, diagram);
            diagram.Add(typeBinary.TypeOperand);
            return;
        }
        if (node is UnaryExpression unary)
        {
            diagram.Add(Box(unary.IsLifted));
            diagram.Add(Box(unary.IsLiftedToNull));
            diagram.Add(unary.Method);
            GenerateDiagram(unary.Operand, iterationState, diagram);
            return;
        }
    }
}
