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
    public void ConditionalOfMembersIsEligible() =>
        Assert.IsTrue(Analyzer().Analyze(BodyOf<string?>(person => person.NameGets > 0 ? person.Name : person.Placeholder)).IsEligible);

    [TestMethod]
    public void ConstantIsEligible() =>
        Assert.IsTrue(Analyzer().Analyze(Expression.Constant(3)).IsEligible);

    [TestMethod]
    public void DisposedPropertyValueIsIneligible()
    {
        var options = new ExpressionObserverOptions();
        options.AddPropertyValueDisposal(typeof(TestPerson).GetProperty(nameof(TestPerson.Name))!);
        var analysis = new Epiforge.Extensions.Expressions.Observable.DirectSubscriptionAnalyzer(options).Analyze(BodyOf<string?>(person => person.Name));
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
    public void TypeTestOfMemberIsEligible() =>
        Assert.IsTrue(Analyzer().Analyze(BodyOf<bool>(person => person.Name is string)).IsEligible);

    [TestMethod]
    public void UserDefinedBinaryOperatorIsIneligible()
    {
        var other = TestPerson.CreateEmily();
        var analysis = Analyzer().Analyze(BodyOf<TestPerson>(person => person + other));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.UserDefinedOperator, analysis.Ineligibility);
    }

    [TestMethod]
    public void UserDefinedUnaryOperatorIsIneligible()
    {
        var analysis = Analyzer().Analyze(BodyOf<TestPerson>(person => -person));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.UserDefinedOperator, analysis.Ineligibility);
    }
}
