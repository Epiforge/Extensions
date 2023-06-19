namespace Epiforge.Extensions.Expressions.Tests;

[TestClass]
public class ExpressionExtensions
{
    #region Utilities

    [ExcludeFromCodeCoverage]
    public static int Add(int a, int b) =>
        a + b;

    [ExcludeFromCodeCoverage]
    public static object? Convert(int i) =>
        i;

    [ExcludeFromCodeCoverage]
    public static bool Equals(int value1, int value2) =>
        value1 == value2;

    class BinaryOperationBinder :
        DynamicMetaObjectBinder
    {
        readonly ExpressionType operationType;

        public BinaryOperationBinder(ExpressionType operationType) =>
            this.operationType = operationType;

        [ExcludeFromCodeCoverage]
        public override DynamicMetaObject Bind(DynamicMetaObject target, DynamicMetaObject[] args)
        {
            var expression = Expression.MakeBinary(operationType, target.Expression, args[0].Expression);
            return new DynamicMetaObject(expression, BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType));
        }
    }

    class Node
    {
        public NodeData Data { get; set; } = new();
        public IList<Node?>? Children { get; set; } = new List<Node?>() { null };
    }

    class NodeData
    {
        public string? Name { get; set; }
    }

    #endregion Utilities

    readonly Expressions.ExpressionEqualityComparer comparer = Expressions.ExpressionEqualityComparer.Default;

    [TestMethod]
    public void DuplicateBinary()
    {
        Expression<Func<int, int, int>> add = (a, b) => a + b;
        var duplicated = add.Duplicate();
        Assert.AreNotSame(add, duplicated);
        Assert.IsTrue(comparer.Equals(add, duplicated));
        Assert.AreEqual(comparer.GetHashCode(add), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateBlock()
    {
        var block = Expression.Block(Expression.Constant(1));
        var duplicated = block.Duplicate();
        Assert.AreNotSame(block, duplicated);
        Assert.IsTrue(comparer.Equals(block, duplicated));
        Assert.AreEqual(comparer.GetHashCode(block), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateConditional()
    {
        Expression<Func<bool, int>> conditional = b => b ? 1 : 0;
        var duplicated = conditional.Duplicate();
        Assert.AreNotSame(conditional, duplicated);
        Assert.IsTrue(comparer.Equals(conditional, duplicated));
        Assert.AreEqual(comparer.GetHashCode(conditional), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateDynamic()
    {
        var binder = new BinaryOperationBinder(ExpressionType.Add);
        var dynamic = Expression.Dynamic(binder, typeof(object), Expression.Constant(1));
        var duplicated = dynamic.Duplicate();
        Assert.AreNotSame(dynamic, duplicated);
        Assert.IsTrue(comparer.Equals(dynamic, duplicated));
        Assert.AreEqual(comparer.GetHashCode(dynamic), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateGoto()
    {
        var label = Expression.Label();
        var @goto = Expression.Goto(label);
        var duplicated = @goto.Duplicate();
        Assert.AreNotSame(@goto, duplicated);
        Assert.IsTrue(comparer.Equals(@goto, duplicated));
        Assert.AreEqual(comparer.GetHashCode(@goto), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateIndex()
    {
        var index = Expression.MakeIndex(Expression.Constant(new int[1]), typeof(int[]).GetProperty("Item", new Type[] { typeof(int) })!, new Expression[] { Expression.Constant(0) });
        var duplicated = index.Duplicate();
        Assert.AreNotSame(index, duplicated);
        Assert.IsTrue(comparer.Equals(index, duplicated));
        Assert.AreEqual(comparer.GetHashCode(index), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateInvocation()
    {
        var invocation = Expression.Invoke(Expression.Constant(new Func<int, int, int>(Add)), Expression.Constant(1), Expression.Constant(2));
        var duplicated = invocation.Duplicate();
        Assert.AreNotSame(invocation, duplicated);
        Assert.IsTrue(comparer.Equals(invocation, duplicated));
        Assert.AreEqual(comparer.GetHashCode(invocation), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateLabel()
    {
        var label = Expression.Label(Expression.Label());
        var duplicated = label.Duplicate();
        Assert.AreNotSame(label, duplicated);
        Assert.IsTrue(comparer.Equals(label, duplicated));
        Assert.AreEqual(comparer.GetHashCode(label), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateListInit()
    {
        var listInit = Expression.ListInit(Expression.New(typeof(List<int>)), Expression.Constant(1));
        var duplicated = listInit.Duplicate();
        Assert.AreNotSame(listInit, duplicated);
        Assert.IsTrue(comparer.Equals(listInit, duplicated));
        Assert.AreEqual(comparer.GetHashCode(listInit), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateLoop()
    {
        var label = Expression.Label();
        var loop = Expression.Loop(Expression.Constant(1), label);
        var duplicated = loop.Duplicate();
        Assert.AreNotSame(loop, duplicated);
        Assert.IsTrue(comparer.Equals(loop, duplicated));
        Assert.AreEqual(comparer.GetHashCode(loop), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateMember()
    {
        var member = Expression.MakeMemberAccess(Expression.Constant(string.Empty), typeof(string).GetProperty(nameof(string.Length))!);
        var duplicated = member.Duplicate();
        Assert.AreNotSame(member, duplicated);
        Assert.IsTrue(comparer.Equals(member, duplicated));
        Assert.AreEqual(comparer.GetHashCode(member), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateMemberInitWithAssignment()
    {
        var memberInit = Expression.MemberInit(Expression.New(typeof(List<int>)), Expression.Bind(typeof(List<int>).GetProperty(nameof(List<int>.Capacity))!, Expression.Constant(1)));
        var duplicated = memberInit.Duplicate();
        Assert.AreNotSame(memberInit, duplicated);
        Assert.IsTrue(comparer.Equals(memberInit, duplicated));
        Assert.AreEqual(comparer.GetHashCode(memberInit), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateMemberInitWithListBinding()
    {
#pragma warning disable CS8670 // Object or collection initializer implicitly dereferences possibly null member.
        Expression<Func<Node>> expression = () => new Node() { Children = { new Node(), new Node() } };
#pragma warning restore CS8670 // Object or collection initializer implicitly dereferences possibly null member.
        var duplicated = expression.Duplicate();
        Assert.AreNotSame(expression, duplicated);
        Assert.IsTrue(comparer.Equals(expression, duplicated));
        Assert.AreEqual(comparer.GetHashCode(expression), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateMemberInitWithMemberBinding()
    {
        Expression<Func<Node>> expression = () => new Node() { Data = { Name = "MemberMemberBinding" } };
        var duplicated = expression.Duplicate();
        Assert.AreNotSame(expression, duplicated);
        Assert.IsTrue(comparer.Equals(expression, duplicated));
        Assert.AreEqual(comparer.GetHashCode(expression), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateMethodCall()
    {
        var methodCall = Expression.Call(Expression.Constant(string.Empty), typeof(string).GetMethod(nameof(string.Substring), new Type[] { typeof(int), typeof(int) })!, Expression.Constant(0), Expression.Constant(1));
        var duplicated = methodCall.Duplicate();
        Assert.AreNotSame(methodCall, duplicated);
        Assert.IsTrue(comparer.Equals(methodCall, duplicated));
        Assert.AreEqual(comparer.GetHashCode(methodCall), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateNew()
    {
        var @new = Expression.New(typeof(string).GetConstructor(new Type[] { typeof(char[]) })!, Expression.Constant(new char[0]));
        var duplicated = @new.Duplicate();
        Assert.AreNotSame(@new, duplicated);
        Assert.IsTrue(comparer.Equals(@new, duplicated));
        Assert.AreEqual(comparer.GetHashCode(@new), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateNewArrayBounds()
    {
        var newArray = Expression.NewArrayBounds(typeof(int), Expression.Constant(1));
        var duplicated = newArray.Duplicate();
        Assert.AreNotSame(newArray, duplicated);
        Assert.IsTrue(comparer.Equals(newArray, duplicated));
        Assert.AreEqual(comparer.GetHashCode(newArray), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateNewArrayInit()
    {
        var newArray = Expression.NewArrayInit(typeof(int), Expression.Constant(1));
        var duplicated = newArray.Duplicate();
        Assert.AreNotSame(newArray, duplicated);
        Assert.IsTrue(comparer.Equals(newArray, duplicated));
        Assert.AreEqual(comparer.GetHashCode(newArray), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DuplicateNull() =>
        ((Expression<Func<int, int, int>>)null!).Duplicate();

    [TestMethod]
    public void DuplicateSwitch()
    {
        var @switch = Expression.Switch(Expression.Constant(1), Expression.Constant(1), Expression.SwitchCase(Expression.Constant(1), Expression.Constant(1)));
        var duplicated = @switch.Duplicate();
        Assert.AreNotSame(@switch, duplicated);
        Assert.IsTrue(comparer.Equals(@switch, duplicated));
        Assert.AreEqual(comparer.GetHashCode(@switch), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateTry()
    {
        var @try = Expression.TryCatch(Expression.Constant(1), Expression.Catch(typeof(Exception), Expression.Constant(1)));
        var duplicated = @try.Duplicate();
        Assert.AreNotSame(@try, duplicated);
        Assert.IsTrue(comparer.Equals(@try, duplicated));
        Assert.AreEqual(comparer.GetHashCode(@try), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateTypeBinary()
    {
        var typeBinary = Expression.TypeIs(Expression.Constant(string.Empty), typeof(string));
        var duplicated = typeBinary.Duplicate();
        Assert.AreNotSame(typeBinary, duplicated);
        Assert.IsTrue(comparer.Equals(typeBinary, duplicated));
        Assert.AreEqual(comparer.GetHashCode(typeBinary), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void DuplicateUnary()
    {
        var unary = Expression.Negate(Expression.Constant(1));
        var duplicated = unary.Duplicate();
        Assert.AreNotSame(unary, duplicated);
        Assert.IsTrue(comparer.Equals(unary, duplicated));
        Assert.AreEqual(comparer.GetHashCode(unary), comparer.GetHashCode(duplicated));
    }

    [TestMethod]
    public void EmptySubstitutions()
    {
        Expression<Func<int, int, int>> add = (a, b) => a + b;
        var substituted = add.SubstituteMethods();
        Assert.AreEqual(add.ToString(), substituted.ToString());
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void InvalidSubstitutions() =>
        ((Expression<Func<int, int, int>>)((a, b) => a + b)).SubstituteMethods((typeof(object).GetMethod(nameof(ToString), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!, typeof(int).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Single(method => method.ReturnType == typeof(string) && method.Name == nameof(int.ToString) && method.GetParameters() is { } parameters && parameters.Length == 1 && parameters[0].ParameterType == typeof(string))));

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullExpression() =>
        ((Expression<Func<int, int, int>>)null!).SubstituteMethods();

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullSubstitutions() =>
        ((Expression<Func<int, int, int>>)((a, b) => a + b)).SubstituteMethods(null!);

    void SubstituteMethods(Expression expression)
    {
        var toString = typeof(object).GetMethod(nameof(ToString), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!;
        var substituted = expression.SubstituteMethods((toString, toString));
        Assert.AreEqual(expression.ToString(), substituted.ToString());
    }

    [TestMethod]
    public void SubstituteMethodsBlock() =>
        SubstituteMethods(Expression.Block(new ParameterExpression[] { Expression.Parameter(typeof(int)) }, Expression.Constant(1), Expression.Constant(2)));

    [TestMethod]
    public void SubstituteMethodsCall() =>
        SubstituteMethods(Expression.Call(Expression.Constant(1), typeof(int).GetMethod(nameof(int.ToString), new Type[] { typeof(string) })!, Expression.Constant("f")));

    [TestMethod]
    public void SubstituteMethodsCallStatic() =>
        SubstituteMethods(Expression.Call(typeof(int).GetMethod(nameof(int.Parse), new Type[] { typeof(string) })!, Expression.Constant("1")));

    [TestMethod]
    public void SubstituteMethodsCondition() =>
        SubstituteMethods((Expression<Func<int, int, int>>)((a, b) => a < b ? -1 : 1));

    [TestMethod]
    public void SubstituteMethodsDynamic() =>
        SubstituteMethods(Expression.Dynamic(new BinaryOperationBinder(ExpressionType.Add), typeof(object), Expression.Constant(1), Expression.Constant(2)));

    [TestMethod]
    public void SubstituteMethodsGoto() =>
        SubstituteMethods(Expression.Goto(Expression.Label()));

    [TestMethod]
    public void SubstituteMethodsGotoValue() =>
        SubstituteMethods(Expression.Goto(Expression.Label(), Expression.Constant(1)));

    [TestMethod]
    public void SubstituteMethodsInvoke() =>
        SubstituteMethods(Expression.Invoke(Expression.Constant((Expression<Func<int, int, int>>)((a, b) => a + b)), Expression.Constant(1), Expression.Constant(2)));

    [TestMethod]
    public void SubstituteMethodsLabel() =>
        SubstituteMethods(Expression.Label(Expression.Label(), Expression.Constant(1)));

    [TestMethod]
    public void SubstituteMethodsListInit() =>
        SubstituteMethods(Expression.ListInit(Expression.New(typeof(List<int>)), Expression.Constant(1), Expression.Constant(2)));

    [TestMethod]
    public void SubstituteMethodsLoop() =>
        SubstituteMethods(Expression.Loop(Expression.Constant(1)));

    [TestMethod]
    public void SubstituteMethodsMakeBinary() =>
        SubstituteMethods(Expression.MakeBinary(ExpressionType.Add, Expression.Constant(1), Expression.Constant(2)));

    [TestMethod]
    public void SubstituteMethodsMakeBinaryFactoryMethod() =>
        SubstituteMethods(Expression.MakeBinary(ExpressionType.Add, Expression.Constant(1), Expression.Constant(2), true, GetType().GetMethods().Single(method => method.IsPublic && method.IsStatic && method.Name == nameof(Add))));

    [TestMethod]
    public void SubstituteMethodsMakeBinaryTypeConversion() =>
        SubstituteMethods(Expression.MakeBinary(ExpressionType.Coalesce, Expression.Constant("some text"), Expression.Constant("default text"), true, null, (Expression<Func<object?, string>>)(obj => obj == null ? string.Empty : obj.ToString()!)));

    [TestMethod]
    public void SubstituteMethodsMakeIndex() =>
        SubstituteMethods(Expression.MakeIndex(Expression.Constant(new List<int> { 1, 2, 3 }), typeof(List<int>).GetProperties().Single(property => property.GetIndexParameters().Length == 1), new Expression[] { Expression.Constant(0) }));

    [TestMethod]
    public void SubstituteMethodsMakeTry() =>
        SubstituteMethods(Expression.MakeTry(typeof(int), Expression.Constant(1), Expression.Constant(2), null, new CatchBlock[] { Expression.Catch(Expression.Parameter(typeof(Exception)), Expression.Constant(3)) }));

    [TestMethod]
    public void SubstituteMethodsMakeUnary() =>
        SubstituteMethods(Expression.MakeUnary(ExpressionType.Convert, Expression.Constant(1), typeof(object)));

    [TestMethod]
    public void SubstituteMethodsMakeUnaryTypeConversion() =>
        SubstituteMethods(Expression.MakeUnary(ExpressionType.Convert, Expression.Constant(1), typeof(object), GetType().GetMethods().Single(method => method.IsPublic && method.IsStatic && method.Name == nameof(Convert))));

    [TestMethod]
    public void SubstituteMethodsMemberInit() =>
        SubstituteMethods(Expression.MemberInit(Expression.New(typeof(List<int>)), Expression.Bind(typeof(List<int>).GetProperty(nameof(List<int>.Capacity))!, Expression.Constant(1))));

    [TestMethod]
    public void SubstituteMethodsNew() =>
        SubstituteMethods(Expression.New(typeof(List<int>).GetConstructor(new Type[] { typeof(int) })!, Expression.Constant(1)));

    [TestMethod]
    public void SubstituteMethodsNewArrayBounds() =>
        SubstituteMethods(Expression.NewArrayBounds(typeof(int), Expression.Constant(1)));

    [TestMethod]
    public void SubstituteMethodsNewArrayInit() =>
        SubstituteMethods(Expression.NewArrayInit(typeof(int), Expression.Constant(1), Expression.Constant(2)));

    [TestMethod]
    public void SubstituteMethodsNewWithMembers() =>
        SubstituteMethods(Expression.New(typeof(List<int>).GetConstructor(new Type[] { typeof(int) })!, new Expression[] { Expression.Constant(1) }, typeof(List<int>).GetProperty(nameof(List<int>.Capacity))!));

    [TestMethod]
    public void SubstituteMethodsSwitch() =>
        SubstituteMethods(Expression.Switch(Expression.Constant(2), Expression.SwitchCase(Expression.Block(Expression.Call(typeof(Console).GetMethod("WriteLine", new[] { typeof(string) })!, Expression.Constant("Case 2 executed."))), Expression.Constant(2))));

    [TestMethod]
    public void SubstituteMethodsSwitchWithComparison() =>
        SubstituteMethods(Expression.Switch(Expression.Constant(2), Expression.Call(typeof(Console).GetMethod("WriteLine", new[] { typeof(string) })!, Expression.Constant("Default case executed.")), GetType().GetMethod(nameof(Equals), BindingFlags.Public | BindingFlags.Static)!, new SwitchCase[] { Expression.SwitchCase(Expression.Call(typeof(Console).GetMethod("WriteLine", new[] { typeof(string) })!, Expression.Constant("Case 2 executed.")), Expression.Constant(2)) }));

    [TestMethod]
    public void SubstituteMethodsSwitchWithDefaultBody() =>
        SubstituteMethods(Expression.Switch(Expression.Constant(1), Expression.Constant(0), Expression.SwitchCase(Expression.Constant(1), Expression.Constant(1))));

    [TestMethod]
    public void SubstituteMethodsTypeIs() =>
        SubstituteMethods((Expression<Func<object, bool>>)(a => a is string));

    [TestMethod]
    public void SubstituteMethodsTypeEqual() =>
        SubstituteMethods(Expression.TypeEqual(Expression.Constant(1), typeof(int)));
}