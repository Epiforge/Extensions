namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class DirectSubscriptionAnalyzer
{
    static Expression BodyOf<TResult>(Expression<Func<TestPerson, TResult>> expression) =>
        expression.Body;

    static Expression BodyOfPeople<TResult>(Expression<Func<ObservableCollection<TestPerson>, TResult>> expression) =>
        expression.Body;

    static Epiforge.Extensions.Expressions.Observable.DirectSubscriptionAnalyzer Analyzer() =>
        new();

    [TestMethod]
    public void AndAlsoOfMembersIsIneligible()
    {
        var analysis = Analyzer().Analyze(BodyOf<bool>(person => person.NameGets > 0 && person.NameGets < 100));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.DeferredBranch, analysis.Ineligibility);
    }

    [TestMethod]
    public void AndOfMembersIsEligible() =>
        Assert.IsTrue(Analyzer().Analyze(BodyOf<bool>(person => person.NameGets > 0 & person.NameGets < 100)).IsEligible);

    [TestMethod]
    public void ArrayAccessIsIneligible()
    {
        var analysis = Analyzer().Analyze(Expression.ArrayAccess(Expression.Constant(new[] { 3 }), Expression.Constant(0)));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.UnsupportedExpressionKind, analysis.Ineligibility);
    }

    [TestMethod]
    public void BinaryOfMembersIsEligible() =>
        Assert.IsTrue(Analyzer().Analyze(BodyOf<bool>(person => person.NameGets > 0)).IsEligible);

    [TestMethod]
    public void ChainedMemberIsIneligible()
    {
        var analysis = Analyzer().Analyze(BodyOf<int>(person => person.Name!.Length));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.ChangeableMemberTarget, analysis.Ineligibility);
        Assert.IsInstanceOfType<MemberExpression>(analysis.IneligibleExpression);
        Assert.AreEqual(nameof(string.Length), ((MemberExpression)analysis.IneligibleExpression!).Member.Name);
    }

    [TestMethod]
    public void ClosureFieldMemberIsEligible()
    {
        var other = TestPerson.CreateEmily();
        Assert.IsTrue(Analyzer().Analyze(BodyOf<bool>(person => person.NameGets > other.NameGets)).IsEligible);
    }

    [TestMethod]
    public void CoalesceOfMembersIsIneligible()
    {
        var analysis = Analyzer().Analyze(BodyOf<string?>(person => person.Name ?? person.Placeholder));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.DeferredBranch, analysis.Ineligibility);
    }

    [TestMethod]
    public void ConditionalOfMembersIsIneligible()
    {
        var analysis = Analyzer().Analyze(BodyOf<string?>(person => person.NameGets > 0 ? person.Name : person.Placeholder));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.DeferredBranch, analysis.Ineligibility);
    }

    [TestMethod]
    public void ConstantIsEligible() =>
        Assert.IsTrue(Analyzer().Analyze(Expression.Constant(3)).IsEligible);

    [TestMethod]
    public void DecimalComparisonIsEligible() =>
        Assert.IsTrue(Analyzer().Analyze(BodyOf<bool>(person => person.NameGets > 0m)).IsEligible);

    [TestMethod]
    public void DisposedPropertyValueIsIneligible()
    {
        var options = new ExpressionObserverOptions();
        var property = typeof(Options.TestObject).GetProperty(nameof(Options.TestObject.SyncDisposable))!;
        options.AddPropertyValueDisposal(property);
        var analysis = new Epiforge.Extensions.Expressions.Observable.DirectSubscriptionAnalyzer(options).Analyze(Expression.Property(Expression.Constant(new Options.TestObject()), property));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.ValueRequiresDisposal, analysis.Ineligibility);
    }

    [TestMethod]
    public void IndexerIsIneligible()
    {
        var analysis = Analyzer().Analyze(BodyOfPeople<TestPerson>(people => people[0]));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.UnsupportedExpressionKind, analysis.Ineligibility);
    }

    [TestMethod]
    public void IndexOnChangeableTargetIsIneligible()
    {
        var person = Expression.Parameter(typeof(TestPerson));
        var analysis = Analyzer().Analyze(Expression.MakeIndex(Expression.Property(person, nameof(TestPerson.Name)), typeof(string).GetProperty("Chars")!, [Expression.Constant(0)]));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.ChangeableIndexTarget, analysis.Ineligibility);
    }

    [TestMethod]
    public void IndexOnConstantIsEligible()
    {
        var people = new ObservableCollection<TestPerson>(TestPerson.MakePeople());
        Assert.IsTrue(Analyzer().Analyze(Expression.MakeIndex(Expression.Constant(people), typeof(ObservableCollection<TestPerson>).GetProperty("Item")!, [Expression.Constant(0)])).IsEligible);
    }

    [TestMethod]
    public void InvocationIsIneligible()
    {
        var analysis = Analyzer().Analyze(Expression.Invoke(Expression.Constant((Func<int>)(() => 3))));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.UnsupportedExpressionKind, analysis.Ineligibility);
    }

    [TestMethod]
    public void MemberInitIsIneligible()
    {
        var analysis = Analyzer().Analyze(BodyOf<TestPerson>(person => new TestPerson { Name = "Emily" }));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.UnsupportedExpressionKind, analysis.Ineligibility);
    }

    [TestMethod]
    public void MemberOnParameterIsEligible() =>
        Assert.IsTrue(Analyzer().Analyze(BodyOf<string?>(person => person.Name)).IsEligible);

    [TestMethod]
    public void MethodCallIsIneligible()
    {
        var analysis = Analyzer().Analyze(BodyOf<string>(person => person.ToString()));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.UnsupportedExpressionKind, analysis.Ineligibility);
    }

    [TestMethod]
    public void MethodCallOnAChangeableTargetIsEligible() =>
        Assert.IsTrue(Analyzer().Analyze(BodyOf<string>(person => person.Placeholder!.ToString())).IsEligible);

    [TestMethod]
    public void MethodCallReturningSealedTypeIsEligible()
    {
        var body = (MethodCallExpression)BodyOf<string>(person => person.ToString());
        Assert.IsTrue(body.Method.ReturnType.IsSealed);
        Assert.IsTrue(Analyzer().Analyze(body).IsEligible);
    }

    [TestMethod]
    public void MethodCallReturningUnsealedTypeIsIneligible()
    {
        var body = (MethodCallExpression)BodyOf<object>(person => person.Name!.Clone());
        Assert.IsFalse(body.Method.ReturnType.IsSealed);
        var analysis = Analyzer().Analyze(body);
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.ValueRequiresDisposal, analysis.Ineligibility);
    }

    [TestMethod]
    public void MethodCallReturningValueTypeIsEligible()
    {
        var body = (MethodCallExpression)BodyOf<bool>(person => string.IsNullOrEmpty(person.Name));
        Assert.IsTrue(body.Method.IsStatic);
        Assert.IsTrue(Analyzer().Analyze(body).IsEligible);
    }

    [TestMethod]
    public void MethodCallWithALambdaArgumentIsIneligible()
    {
        var analysis = Analyzer().Analyze(BodyOf<bool>(person => person.Name!.ToCharArray().Any(character => character == 'x')));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.UnsupportedExpressionKind, analysis.Ineligibility);
    }

    [TestMethod]
    public void NewArrayInitIsIneligible()
    {
        var analysis = Analyzer().Analyze(BodyOf<long[]>(person => new[] { person.NameGets }));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.UnsupportedExpressionKind, analysis.Ineligibility);
    }

    [TestMethod]
    public void NewIsIneligible()
    {
        var analysis = Analyzer().Analyze(BodyOf<TestPerson>(person => new TestPerson()));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.UnsupportedExpressionKind, analysis.Ineligibility);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullExpression() =>
        Analyzer().Analyze(null!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullOptions() =>
        new Epiforge.Extensions.Expressions.Observable.DirectSubscriptionAnalyzer(null!);

    [TestMethod]
    public void OrElseOfMembersIsIneligible()
    {
        var analysis = Analyzer().Analyze(BodyOf<bool>(person => person.NameGets > 0 || person.NameGets < 100));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.DeferredBranch, analysis.Ineligibility);
    }

    [TestMethod]
    public void OrOfMembersIsEligible() =>
        Assert.IsTrue(Analyzer().Analyze(BodyOf<bool>(person => person.NameGets > 0 | person.NameGets < 100)).IsEligible);

    [TestMethod]
    public void QuotedLambdaIsEligible() =>
        Assert.IsTrue(Analyzer().Analyze(Expression.Quote(Expression.Lambda<Func<int>>(Expression.Constant(3)))).IsEligible);

    [TestMethod]
    public void StaticFieldChainTargetIsEligible() =>
        Assert.IsTrue(Analyzer().Analyze(BodyOf<int>(person => StaticFieldHolder.Held.Linked!.Rank)).IsEligible);

    [TestMethod]
    public void StaticFieldTargetIsEligible() =>
        Assert.IsTrue(Analyzer().Analyze(BodyOf<int>(person => StaticFieldHolder.Held.Rank)).IsEligible);

    [TestMethod]
    public void StaticPropertyIsEligibleWhenStaticDisposalIsExcluded()
    {
        var options = new ExpressionObserverOptions { DisposeStaticMethodReturnValues = false };
        Assert.IsTrue(new Epiforge.Extensions.Expressions.Observable.DirectSubscriptionAnalyzer(options).Analyze(Expression.Property(null, typeof(Console).GetProperty(nameof(Console.Out))!)).IsEligible);
    }

    [TestMethod]
    public void StaticPropertyChainTargetIsEligible() =>
        Assert.IsTrue(Analyzer().Analyze(BodyOf<int>(person => StaticPropertyHolder.Label.Length)).IsEligible);

    [TestMethod]
    public void StaticPropertyOfDisposableTypeIsIneligible()
    {
        var analysis = Analyzer().Analyze(Expression.Property(null, typeof(Console).GetProperty(nameof(Console.Out))!));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.ValueRequiresDisposal, analysis.Ineligibility);
    }

    [TestMethod]
    public void StaticPropertyOfValueTypeIsEligible() =>
        Assert.IsTrue(Analyzer().Analyze(Expression.Property(null, typeof(DateTime).GetProperty(nameof(DateTime.Now))!)).IsEligible);

    [TestMethod]
    public void StringComparisonIsEligible()
    {
        var body = (BinaryExpression)BodyOf<bool>(person => person.Name == "Emily");
        Assert.IsNotNull(body.Method);
        Assert.IsTrue(Analyzer().Analyze(body).IsEligible);
    }

    [TestMethod]
    public void TypeTestOfMemberIsEligible() =>
        Assert.IsTrue(Analyzer().Analyze(BodyOf<bool>(person => person.Name is string)).IsEligible);

    [TestMethod]
    public void UserDefinedBinaryOperatorReturningSealedDisposableIsIneligible()
    {
        var a = new SealedDisposableTestPerson("Emily");
        var b = new SealedDisposableTestPerson("John");
        var analysis = Analyzer().Analyze(BodyOf<SealedDisposableTestPerson>(person => a + b));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.UserDefinedOperator, analysis.Ineligibility);
    }

    [TestMethod]
    public void UserDefinedBinaryOperatorReturningUnsealedTypeIsIneligible()
    {
        var other = TestPerson.CreateEmily();
        var analysis = Analyzer().Analyze(BodyOf<TestPerson>(person => person + other));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.UserDefinedOperator, analysis.Ineligibility);
    }

    [TestMethod]
    public void UserDefinedUnaryOperatorReturningUnsealedTypeIsIneligible()
    {
        var analysis = Analyzer().Analyze(BodyOf<TestPerson>(person => -person));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.UserDefinedOperator, analysis.Ineligibility);
    }
}
