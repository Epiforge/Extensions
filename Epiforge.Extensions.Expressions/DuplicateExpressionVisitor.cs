namespace Epiforge.Extensions.Expressions;

sealed class DuplicateExpressionVisitor :
    ExpressionVisitor
{
    [return: NotNullIfNotNull("node")]
    public override Expression? Visit(Expression? node)
    {
        if (node is DynamicExpression dynamic)
            return VisitDynamic(dynamic);
        return base.Visit(node);
    }

    protected override Expression VisitBinary(BinaryExpression node) =>
        Expression.MakeBinary(node.NodeType, Visit(node.Left), Visit(node.Right), node.IsLiftedToNull, node.Method, node.Conversion);

    protected override Expression VisitBlock(BlockExpression node) =>
        Expression.Block(node.Type, node.Variables, Visit(node.Expressions));

    protected override CatchBlock VisitCatchBlock(CatchBlock node) =>
        Expression.MakeCatchBlock(node.Test, node.Variable, Visit(node.Body), Visit(node.Filter));

    protected override Expression VisitConditional(ConditionalExpression node) =>
        Expression.Condition(Visit(node.Test), Visit(node.IfTrue), Visit(node.IfFalse));

    protected override Expression VisitDynamic(DynamicExpression node) =>
        Expression.Dynamic(node.Binder, node.Type, node.Arguments);

    protected override ElementInit VisitElementInit(ElementInit node) =>
        Expression.ElementInit(node.AddMethod, Visit(node.Arguments));

    protected override Expression VisitGoto(GotoExpression node) =>
        Expression.MakeGoto(node.Kind, node.Target, Visit(node.Value), node.Type);

    protected override Expression VisitIndex(IndexExpression node) =>
        Expression.MakeIndex(Visit(node.Object)!, node.Indexer, Visit(node.Arguments));

    protected override Expression VisitInvocation(InvocationExpression node) =>
        Expression.Invoke(Visit(node.Expression), Visit(node.Arguments));

    protected override Expression VisitLabel(LabelExpression node) =>
        Expression.Label(node.Target, Visit(node.DefaultValue));

    protected override Expression VisitLambda<T>(Expression<T> node) =>
        Expression.Lambda<T>(Visit(node.Body), node.Name, node.TailCall, node.Parameters);

    protected override Expression VisitListInit(ListInitExpression node) =>
        Expression.ListInit((NewExpression)Visit(node.NewExpression), node.Initializers.Select(VisitElementInit));

    protected override Expression VisitLoop(LoopExpression node) =>
        Expression.Loop(Visit(node.Body), node.BreakLabel, node.ContinueLabel);

    protected override Expression VisitMember(MemberExpression node) =>
        Expression.MakeMemberAccess(Visit(node.Expression), node.Member);

    protected override MemberAssignment VisitMemberAssignment(MemberAssignment node) =>
        Expression.Bind(node.Member, Visit(node.Expression));

    protected override MemberBinding VisitMemberBinding(MemberBinding node) =>
        node.BindingType switch
        {
            MemberBindingType.Assignment => VisitMemberAssignment((MemberAssignment)node),
            MemberBindingType.ListBinding => VisitMemberListBinding((MemberListBinding)node),
            MemberBindingType.MemberBinding => VisitMemberMemberBinding((MemberMemberBinding)node),
            _ => throw new NotSupportedException($"Unhandled binding type: {node.BindingType}"),
        };

    protected override Expression VisitMemberInit(MemberInitExpression node) =>
        Expression.MemberInit((NewExpression)Visit(node.NewExpression), node.Bindings.Select(VisitMemberBinding));

    protected override MemberListBinding VisitMemberListBinding(MemberListBinding node) =>
        Expression.ListBind(node.Member, node.Initializers.Select(VisitElementInit));

    protected override MemberMemberBinding VisitMemberMemberBinding(MemberMemberBinding node) =>
        Expression.MemberBind(node.Member, node.Bindings.Select(VisitMemberBinding));

    protected override Expression VisitMethodCall(MethodCallExpression node) =>
        Expression.Call(Visit(node.Object), node.Method, Visit(node.Arguments));

    protected override Expression VisitNew(NewExpression node) =>
        node.Constructor is null ? Expression.New(node.Type) : node.Members is null ? Expression.New(node.Constructor, Visit(node.Arguments)) : Expression.New(node.Constructor, Visit(node.Arguments), node.Members);

    protected override Expression VisitNewArray(NewArrayExpression node) =>
        node.NodeType switch
        {
            ExpressionType.NewArrayBounds => Expression.NewArrayBounds(node.Type.GetElementType()!, node.Expressions.Select(Visit)!),
            ExpressionType.NewArrayInit => Expression.NewArrayInit(node.Type.GetElementType()!, node.Expressions.Select(Visit)!),
            _ => throw new ArgumentException($"Unhandled expression type: {node.NodeType}"),
        };

    protected override Expression VisitSwitch(SwitchExpression node) =>
        Expression.Switch(Visit(node.SwitchValue), Visit(node.DefaultBody), node.Comparison, node.Cases.Select(VisitSwitchCase));

    protected override SwitchCase VisitSwitchCase(SwitchCase node) =>
        Expression.SwitchCase(Visit(node.Body), node.TestValues.Select(Visit)!);

    protected override Expression VisitTry(TryExpression node) =>
        Expression.MakeTry(node.Type, Visit(node.Body), Visit(node.Finally), Visit(node.Fault), node.Handlers.Select(VisitCatchBlock));

    protected override Expression VisitTypeBinary(TypeBinaryExpression node) =>
        Expression.TypeIs(Visit(node.Expression), node.TypeOperand);

    protected override Expression VisitUnary(UnaryExpression node) =>
        Expression.MakeUnary(node.NodeType, Visit(node.Operand), node.Type, node.Method);
}
