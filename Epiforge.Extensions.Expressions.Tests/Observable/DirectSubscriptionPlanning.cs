namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class DirectSubscriptionPlanning
{
    sealed class ParameterReplacer(ParameterExpression parameter, Expression replacement) :
        ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == parameter ? replacement : base.VisitParameter(node);
    }

    static Epiforge.Extensions.Expressions.Observable.DirectSubscriptionAnalyzer Analyzer() =>
        new();

    static Epiforge.Extensions.Expressions.Observable.DirectSubscriptionAnalyzer Analyzer(ExpressionObserverOptions options) =>
        new(options);

    static Expression Bound<TResult>(Expression<Func<TestPerson, TResult>> expression, TestPerson person) =>
        new ParameterReplacer(expression.Parameters[0], Expression.Constant(person)).Visit(expression.Body);

    static object? ValueOf(Expression? expression) =>
        ((ConstantExpression)expression!).Value;

    [TestMethod]
    public void ClosureFieldPlansItsContentsAndTheMemberTakenFromIt()
    {
        var person = TestPerson.CreateJohn();
        var other = TestPerson.CreateEmily();
        var plan = Analyzer().Plan(Bound<bool>(subject => subject.NameGets > other.NameGets, person));
        Assert.IsTrue(plan.IsEligible);
        Assert.AreEqual(3, plan.Subscriptions.Count);
        Assert.AreEqual(DirectSubscriptionKind.MemberPropertyChanged, plan.Subscriptions[0].Kind);
        Assert.AreSame(person, ValueOf(plan.Subscriptions[0].Source));
        Assert.AreEqual(DirectSubscriptionKind.DictionaryOrCollectionChanged, plan.Subscriptions[1].Kind);
        Assert.IsInstanceOfType<MemberExpression>(plan.Subscriptions[1].Source);
        Assert.AreEqual(DirectSubscriptionKind.MemberPropertyChanged, plan.Subscriptions[2].Kind);
        Assert.AreEqual(nameof(TestPerson.NameGets), plan.Subscriptions[2].PropertyName);
        Assert.AreSame(plan.Subscriptions[1].Source, plan.Subscriptions[2].Source);
    }

    [TestMethod]
    public void ConstantCollectionPlansCollectionChanged()
    {
        var people = TestPerson.CreatePeopleCollection();
        var plan = Analyzer().Plan(Expression.Constant(people));
        Assert.AreEqual(1, plan.Subscriptions.Count);
        Assert.AreEqual(DirectSubscriptionKind.CollectionChanged, plan.Subscriptions[0].Kind);
        Assert.IsNull(plan.Subscriptions[0].PropertyName);
        Assert.AreSame(people, ValueOf(plan.Subscriptions[0].Source));
    }

    [TestMethod]
    public void ConstantDictionaryPlansCollectionChangedWhenDictionaryChangedIsExcluded()
    {
        var options = new ExpressionObserverOptions { ConstantExpressionsListenForDictionaryChanged = false };
        var plan = Analyzer(options).Plan(Expression.Constant(TestPerson.CreatePeopleDictionary()));
        Assert.AreEqual(1, plan.Subscriptions.Count);
        Assert.AreEqual(DirectSubscriptionKind.CollectionChanged, plan.Subscriptions[0].Kind);
    }

    [TestMethod]
    public void ConstantDictionaryPlansDictionaryChanged()
    {
        var plan = Analyzer().Plan(Expression.Constant(TestPerson.CreatePeopleDictionary()));
        Assert.AreEqual(1, plan.Subscriptions.Count);
        Assert.AreEqual(DirectSubscriptionKind.DictionaryChanged, plan.Subscriptions[0].Kind);
    }

    [TestMethod]
    public void ConstantPlansNothingWhenBothContentsOptionsAreExcluded()
    {
        var options = new ExpressionObserverOptions { ConstantExpressionsListenForCollectionChanged = false, ConstantExpressionsListenForDictionaryChanged = false };
        var plan = Analyzer(options).Plan(Expression.Constant(TestPerson.CreatePeopleCollection()));
        Assert.IsTrue(plan.IsEligible);
        Assert.AreEqual(0, plan.Subscriptions.Count);
    }

    [TestMethod]
    public void ConstantWhichNotifiesOfNothingPlansNothing()
    {
        var plan = Analyzer().Plan(Expression.Constant(3));
        Assert.IsTrue(plan.IsEligible);
        Assert.AreEqual(0, plan.Subscriptions.Count);
    }

    [TestMethod]
    public void DefaultPlanIsIneligibleAndEmpty()
    {
        var plan = default(DirectSubscriptionPlan);
        Assert.IsFalse(plan.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.Unanalyzed, plan.Analysis.Ineligibility);
        Assert.AreEqual(0, plan.Subscriptions.Count);
    }

    [TestMethod]
    public void IgnoredPropertyChangeNotificationIsIneligible()
    {
        var options = new ExpressionObserverOptions();
        options.AddIgnoredPropertyChangeNotification(typeof(TestPerson).GetProperty(nameof(TestPerson.Name))!);
        var plan = Analyzer(options).Plan(Bound<string?>(person => person.Name, TestPerson.CreateEmily()));
        Assert.IsFalse(plan.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.IgnoredChangeNotification, plan.Analysis.Ineligibility);
        Assert.AreEqual(0, plan.Subscriptions.Count);
    }

    [TestMethod]
    public void IndexOnConstantPlansBothContentsSubscriptionsAndTheIndexer()
    {
        var people = new ObservableCollection<TestPerson>(TestPerson.MakePeople());
        var plan = Analyzer().Plan(Expression.MakeIndex(Expression.Constant(people), typeof(ObservableCollection<TestPerson>).GetProperty("Item")!, [Expression.Constant(0)]));
        Assert.IsTrue(plan.IsEligible);
        Assert.AreEqual(3, plan.Subscriptions.Count);
        Assert.AreEqual(DirectSubscriptionKind.CollectionChanged, plan.Subscriptions[0].Kind);
        Assert.AreSame(people, ValueOf(plan.Subscriptions[0].Source));
        Assert.AreEqual(plan.Subscriptions[0], plan.Subscriptions[1]);
        Assert.AreEqual(DirectSubscriptionKind.IndexerPropertyChanged, plan.Subscriptions[2].Kind);
        Assert.AreEqual("Item", plan.Subscriptions[2].PropertyName);
        Assert.AreSame(people, ValueOf(plan.Subscriptions[2].Source));
    }

    [TestMethod]
    public void IneligibleExpressionPlansNothing()
    {
        var plan = Analyzer().Plan(Bound<int>(person => person.Name!.Length, TestPerson.CreateEmily()));
        Assert.IsFalse(plan.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.ChangeableMemberTarget, plan.Analysis.Ineligibility);
        Assert.AreEqual(0, plan.Subscriptions.Count);
    }

    [TestMethod]
    public void MemberOnConstantPlansPropertyChangedOnTheConstant()
    {
        var person = TestPerson.CreateEmily();
        var plan = Analyzer().Plan(Bound<string?>(subject => subject.Name, person));
        Assert.IsTrue(plan.IsEligible);
        Assert.AreEqual(1, plan.Subscriptions.Count);
        Assert.AreEqual(DirectSubscriptionKind.MemberPropertyChanged, plan.Subscriptions[0].Kind);
        Assert.AreEqual(nameof(TestPerson.Name), plan.Subscriptions[0].PropertyName);
        Assert.AreSame(person, ValueOf(plan.Subscriptions[0].Source));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void NullExpression() =>
        Analyzer().Plan(null!);

    [TestMethod]
    public void RepeatedMemberPlansTheSameSiteTwice()
    {
        var person = TestPerson.CreateEmily();
        var plan = Analyzer().Plan(Bound<long>(subject => subject.NameGets + subject.NameGets, person));
        Assert.IsTrue(plan.IsEligible);
        Assert.AreEqual(2, plan.Subscriptions.Count);
        Assert.AreEqual(plan.Subscriptions[0], plan.Subscriptions[1]);
        Assert.AreEqual(DirectSubscriptionKind.MemberPropertyChanged, plan.Subscriptions[0].Kind);
        Assert.AreEqual(nameof(TestPerson.NameGets), plan.Subscriptions[0].PropertyName);
        Assert.AreSame(person, ValueOf(plan.Subscriptions[0].Source));
    }

    [TestMethod]
    public void RepeatedSubexpressionsOfDifferentKindsEachPlanTheirOwn()
    {
        var people = new ObservableCollection<TestPerson>(TestPerson.MakePeople());
        var index = Expression.MakeIndex(Expression.Constant(people), typeof(ObservableCollection<TestPerson>).GetProperty("Item")!, [Expression.Constant(0)]);
        var plan = Analyzer().Plan(Expression.MakeBinary(ExpressionType.Equal, index, index));
        Assert.IsTrue(plan.IsEligible);
        Assert.AreEqual(3, plan.Subscriptions.Count);
    }

    [TestMethod]
    public void StaticMemberPlansNothing()
    {
        var options = new ExpressionObserverOptions { DisposeStaticMethodReturnValues = false };
        var plan = Analyzer(options).Plan(Expression.Property(null, typeof(DateTime).GetProperty(nameof(DateTime.Now))!));
        Assert.IsTrue(plan.IsEligible);
        Assert.AreEqual(0, plan.Subscriptions.Count);
    }
}
