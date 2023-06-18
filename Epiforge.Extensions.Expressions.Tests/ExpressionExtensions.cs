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

    #endregion Utilities

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