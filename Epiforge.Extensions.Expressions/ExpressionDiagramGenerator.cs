namespace Epiforge.Extensions.Expressions;

static class ExpressionDiagramGenerator
{
    class IterationState
    {
        public Dictionary<ParameterExpression, (int set, int index)> Parameters { get; } = new();
        public int ParameterSet { get; set; } = -1;
    }

    public static IEnumerable<object?> GenerateDiagram(Expression? node) =>
        GenerateDiagram(node, new IterationState());

    [SuppressMessage("Maintainability", "CA1502: Avoid excessive complexity")]
    [SuppressMessage("Code Analysis", "CA1506: Avoid excessive class coupling")]
    static IEnumerable<object?> GenerateDiagram(Expression? node, IterationState iterationState)
    {
        if (node is null)
        {
            yield return null;
            yield break;
        }
        yield return node.CanReduce;
        yield return node.NodeType;
        yield return node.Type;
        if (node is BinaryExpression binary)
        {
            yield return binary.IsLifted;
            yield return binary.IsLiftedToNull;
            yield return binary.Method;
            foreach (var element in GenerateDiagram(binary.Left, iterationState))
                yield return element;
            foreach (var element in GenerateDiagram(binary.Right, iterationState))
                yield return element;
            yield break;
        }
        if (node is BlockExpression block)
        {
            ++iterationState.ParameterSet;
            var blockVariables = block.Variables;
            for (int i = 0, ii = blockVariables.Count; i < ii; ++i)
                iterationState.Parameters.Add(blockVariables[i], (iterationState.ParameterSet, i));
            foreach (var expression in block.Expressions)
                foreach (var element in GenerateDiagram(expression, iterationState))
                    yield return element;
            foreach (var variable in block.Variables)
                foreach (var element in GenerateDiagram(variable, iterationState))
                    yield return element;
        }
        if (node is ConditionalExpression conditional)
        {
            foreach (var element in GenerateDiagram(conditional.Test, iterationState))
                yield return element;
            foreach (var element in GenerateDiagram(conditional.IfTrue, iterationState))
                yield return element;
            foreach (var element in GenerateDiagram(conditional.IfFalse, iterationState))
                yield return element;
            yield break;
        }
        if (node is ConstantExpression constant)
        {
            yield return constant.Value;
            yield break;
        }
        if (node is DebugInfoExpression debugInfo)
        {
            yield return debugInfo.Document.DocumentType;
            yield return debugInfo.Document.FileName;
            yield return debugInfo.Document.Language;
            yield return debugInfo.Document.LanguageVendor;
            yield return debugInfo.EndColumn;
            yield return debugInfo.EndLine;
            yield return debugInfo.IsClear;
            yield return debugInfo.StartColumn;
            yield return debugInfo.StartLine;
            yield break;
        }
        if (node is DynamicExpression dynamic)
        {
            yield return dynamic.Binder;
            yield return dynamic.DelegateType;
            foreach (var argument in dynamic.Arguments)
                foreach (var element in GenerateDiagram(argument, iterationState))
                    yield return element;
            yield break;
        }
        if (node is GotoExpression @goto)
        {
            yield return @goto.Kind;
            yield return @goto.Target.Name;
            yield return @goto.Target.Type;
            foreach (var element in GenerateDiagram(@goto.Value, iterationState))
                yield return element;
            yield break;
        }
        if (node is IndexExpression index)
        {
            foreach (var element in GenerateDiagram(index.Object, iterationState))
                yield return element;
            yield return index.Indexer;
            foreach (var argument in index.Arguments)
                foreach (var element in GenerateDiagram(argument, iterationState))
                    yield return element;
            yield break;
        }
        if (node is InvocationExpression invocation)
        {
            foreach (var element in GenerateDiagram(invocation.Expression, iterationState))
                yield return element;
            foreach (var argument in invocation.Arguments)
                foreach (var element in GenerateDiagram(argument, iterationState))
                    yield return element;
            yield break;
        }
        if (node is LabelExpression label)
        {
            yield return label.Target.Name;
            yield return label.Target.Type;
            foreach (var element in GenerateDiagram(label.DefaultValue, iterationState))
                yield return element;
            yield break;
        }
        if (node is LambdaExpression lambda)
        {
            ++iterationState.ParameterSet;
            var lambdaParameters = lambda.Parameters;
            for (int i = 0, ii = lambdaParameters.Count; i < ii; ++i)
                iterationState.Parameters.Add(lambdaParameters[i], (iterationState.ParameterSet, i));
            yield return lambda.Name;
            yield return lambda.ReturnType;
            yield return lambda.TailCall;
            foreach (var element in GenerateDiagram(lambda.Body, iterationState))
                yield return element;
            foreach (var lambdaParameter in lambdaParameters)
                foreach (var element in GenerateDiagram(lambdaParameter, iterationState))
                    yield return element;
            yield break;
        }
        if (node is ListInitExpression listInit)
        {
            foreach (var element in GenerateDiagram(listInit.NewExpression, iterationState))
                yield return element;
            foreach (var initializer in listInit.Initializers)
            {
                yield return initializer.AddMethod;
                foreach (var argument in initializer.Arguments)
                    foreach (var element in GenerateDiagram(argument, iterationState))
                        yield return element;
            }
            yield break;
        }
        if (node is LoopExpression loop)
        {
            yield return loop.BreakLabel?.Name;
            yield return loop.BreakLabel?.Type;
            yield return loop.ContinueLabel?.Name;
            yield return loop.ContinueLabel?.Type;
            foreach (var element in GenerateDiagram(loop.Body, iterationState))
                yield return element;
            yield break;
        }
        if (node is MemberExpression member)
        {
            foreach (var element in GenerateDiagram(member.Expression, iterationState))
                yield return element;
            yield return member.Member;
            yield break;
        }
        if (node is MemberInitExpression memberInit)
        {
            foreach (var element in GenerateDiagram(memberInit.NewExpression, iterationState))
                yield return element;
            foreach (var binding in memberInit.Bindings)
            {
                yield return binding.BindingType;
                yield return binding.Member;
            }
            yield break;
        }
        if (node is MethodCallExpression methodCall)
        {
            foreach (var element in GenerateDiagram(methodCall.Object, iterationState))
                yield return element;
            yield return methodCall.Method;
            foreach (var argument in methodCall.Arguments)
                foreach (var element in GenerateDiagram(argument, iterationState))
                    yield return element;
            yield break;
        }
        if (node is NewArrayExpression newArray)
        {
            foreach (var expression in newArray.Expressions)
                foreach (var element in GenerateDiagram(expression, iterationState))
                    yield return element;
            yield break;
        }
        if (node is NewExpression @new)
        {
            yield return @new.Constructor;
            foreach (var argument in @new.Arguments)
                foreach (var element in GenerateDiagram(argument, iterationState))
                    yield return element;
            if (@new.Members is { } members)
                foreach (var newMember in members) // this line is not covered by tests
                    yield return newMember; // this line is not covered by tests
            yield break;
        }
        if (node is ParameterExpression parameter)
        {
            var (set, parameterIndex) = iterationState.Parameters[parameter];
            yield return set;
            yield return parameterIndex;
            yield break;
        }
        if (node is RuntimeVariablesExpression runtimeVariables)
        {
            ++iterationState.ParameterSet;
            var runtimeVariablesVariables = runtimeVariables.Variables;
            for (int i = 0, ii = runtimeVariablesVariables.Count; i < ii; ++i)
                iterationState.Parameters.Add(runtimeVariablesVariables[i], (iterationState.ParameterSet, i));
            foreach (var variable in runtimeVariables.Variables)
                foreach (var element in GenerateDiagram(variable, iterationState))
                    yield return element;
            yield break;
        }
        if (node is SwitchExpression @switch)
        {
            yield return @switch.Comparison;
            foreach (var switchCase in @switch.Cases)
            {
                foreach (var testValue in switchCase.TestValues)
                    foreach (var element in GenerateDiagram(testValue, iterationState))
                        yield return element;
                foreach (var element in GenerateDiagram(switchCase.Body, iterationState))
                    yield return element;
            }
            foreach (var element in GenerateDiagram(@switch.DefaultBody, iterationState))
                yield return element;
            yield break;
        }
        if (node is TryExpression @try)
        {
            foreach (var element in GenerateDiagram(@try.Body, iterationState))
                yield return element;
            foreach (var handler in @try.Handlers)
            {
                yield return handler.Test;
                foreach (var element in GenerateDiagram(handler.Filter, iterationState))
                    yield return element;
                foreach (var element in GenerateDiagram(handler.Variable, iterationState))
                    yield return element;
                foreach (var element in GenerateDiagram(handler.Body, iterationState))
                    yield return element;
            }
            foreach (var element in GenerateDiagram(@try.Fault, iterationState))
                yield return element;
            foreach (var element in GenerateDiagram(@try.Finally, iterationState))
                yield return element;
            yield break;
        }
        if (node is TypeBinaryExpression typeBinary)
        {
            foreach (var element in GenerateDiagram(typeBinary.Expression, iterationState))
                yield return element;
            yield return typeBinary.TypeOperand;
            yield break;
        }
        if (node is UnaryExpression unary)
        {
            yield return unary.IsLifted;
            yield return unary.IsLiftedToNull;
            yield return unary.Method;
            foreach (var element in GenerateDiagram(unary.Operand, iterationState))
                yield return element;
            yield break;
        }
    }
}
