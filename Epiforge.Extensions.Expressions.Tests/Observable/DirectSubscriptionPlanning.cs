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

    static List<(DirectSubscriptionKind Kind, string? PropertyName, object Source)> Attachments(DirectSubscriptionPlan plan, ParameterExpression parameter, object? argument)
    {
        var attachments = new List<(DirectSubscriptionKind, string?, object)>();
        for (int i = 0, ii = plan.Subscriptions.Count; i < ii; ++i)
        {
            var subscription = plan.Subscriptions[i];
            var source = subscription.Source!;
            var value = ReferenceEquals(source, parameter) ? argument : source is ConstantExpression constantExpression ? constantExpression.Value : Expression.Lambda(source).Compile().DynamicInvoke();
            if (value is not null && subscription.ResolveKind(value) is var kind && kind is not DirectSubscriptionKind.None)
                attachments.Add((kind, subscription.PropertyName, value));
        }
        return attachments;
    }

    static void AssertPlansAgree<TResult>(Expression<Func<TestPerson, TResult>> lambda, TestPerson person)
    {
        var analyzer = Analyzer();
        var parameter = lambda.Parameters[0];
        var fromLambda = analyzer.Plan(lambda.Body);
        var fromNormalized = analyzer.Plan(new ParameterReplacer(parameter, Expression.Constant(person, parameter.Type)).Visit(lambda.Body));
        Assert.AreEqual(fromNormalized.IsEligible, fromLambda.IsEligible, $"normalized: {fromNormalized}; lambda: {fromLambda}");
        var normalized = Attachments(fromNormalized, parameter, person);
        var lambdas = Attachments(fromLambda, parameter, person);
        Assert.AreEqual(normalized.Count, lambdas.Count, $"{lambda}; normalized: [{string.Join(", ", normalized)}]; lambda: [{string.Join(", ", lambdas)}]");
        for (var i = 0; i < normalized.Count; ++i)
        {
            Assert.AreEqual(normalized[i].Kind, lambdas[i].Kind, $"{lambda}, attachment {i}");
            Assert.AreEqual(normalized[i].PropertyName, lambdas[i].PropertyName, $"{lambda}, attachment {i}");
            Assert.AreSame(normalized[i].Source, lambdas[i].Source, $"{lambda}, attachment {i}");
        }
    }

    [TestMethod]
    public void APlanFromALambdaBodyAttachesWhatOneFromTheExpressionItNormalizesToAttaches()
    {
        var person = TestPerson.CreateJohn();
        var other = TestPerson.CreateEmily();
        var people = new ObservableCollection<TestPerson>(TestPerson.MakePeople());
        AssertPlansAgree(subject => subject.NameGets, person);
        AssertPlansAgree(subject => subject.Name, person);
        AssertPlansAgree(subject => subject.NameGets + subject.NameGets, person);
        AssertPlansAgree(subject => subject.NameGets > other.NameGets, person);
        AssertPlansAgree(subject => subject.NameGets + people.Count, person);
        AssertPlansAgree(subject => subject, person);
        AssertPlansAgree(subject => -subject, person);
    }

    public sealed class Holder
    {
        public ObservableCollection<TestPerson>? People;
        public TestPerson? Person;
    }

    [TestMethod]
    public void AFieldOfAnOrdinaryObjectIsAFixedTargetAndItsPropertyIsPlanned()
    {
        var holder = new Holder { Person = TestPerson.CreateJohn() };
        var field = Expression.Field(Expression.Constant(holder), nameof(Holder.Person));
        var plan = Analyzer().Plan(Expression.MakeMemberAccess(field, typeof(TestPerson).GetProperty(nameof(TestPerson.NameGets))!));
        Assert.IsTrue(plan.IsEligible, plan.ToString());
        Assert.AreEqual(1, plan.Subscriptions.Count, $"[{string.Join(", ", plan.Subscriptions)}]");
        Assert.AreEqual(DirectSubscriptionKind.MemberPropertyChanged, plan.Subscriptions[0].Kind);
        Assert.AreEqual(nameof(TestPerson.NameGets), plan.Subscriptions[0].PropertyName);
        Assert.AreSame(field, plan.Subscriptions[0].Source);
    }

    [TestMethod]
    public void AFieldOfAnOrdinaryObjectHoldingACollectionPlansNoContentsSubscription()
    {
        var holder = new Holder { People = new ObservableCollection<TestPerson>(TestPerson.MakePeople()) };
        var field = Expression.Field(Expression.Constant(holder), nameof(Holder.People));
        var plan = Analyzer().Plan(Expression.MakeMemberAccess(field, typeof(ObservableCollection<TestPerson>).GetProperty(nameof(ObservableCollection<TestPerson>.Count))!));
        Assert.IsTrue(plan.IsEligible, plan.ToString());
        foreach (var subscription in plan.Subscriptions)
            Assert.AreNotEqual(DirectSubscriptionKind.DictionaryOrCollectionChanged, subscription.Kind, "the graph watches the contents of a compiler-generated type's field, and of nothing else");
    }

    [TestMethod]
    public void APlanFromALambdaBodyListsAContentsSubscriptionTheNormalizedOneCanRuleOut()
    {
        var person = TestPerson.CreateJohn();
        var lambda = (Expression<Func<TestPerson, TestPerson>>)(subject => subject);
        var parameter = lambda.Parameters[0];
        var fromLambda = Analyzer().Plan(lambda.Body);
        var fromNormalized = Analyzer().Plan(new ParameterReplacer(parameter, Expression.Constant(person, parameter.Type)).Visit(lambda.Body));
        Assert.AreEqual(1, fromLambda.Subscriptions.Count);
        Assert.AreEqual(0, fromNormalized.Subscriptions.Count);
        Assert.AreEqual(DirectSubscriptionKind.None, fromLambda.Subscriptions[0].ResolveKind(person));
    }

    [TestMethod]
    public void AParameterPlansTheContentsSubscriptionItsArgumentWould()
    {
        var people = new ObservableCollection<TestPerson>(TestPerson.MakePeople());
        var parameter = Expression.Parameter(typeof(ObservableCollection<TestPerson>), "subject");
        var plan = Analyzer().Plan(parameter);
        Assert.IsTrue(plan.IsEligible);
        Assert.AreEqual(1, plan.Subscriptions.Count);
        Assert.AreEqual(DirectSubscriptionKind.DictionaryOrCollectionChanged, plan.Subscriptions[0].Kind);
        Assert.AreSame(parameter, plan.Subscriptions[0].Source);
        Assert.AreEqual(DirectSubscriptionKind.CollectionChanged, plan.Subscriptions[0].ResolveKind(people));
    }

    [TestMethod]
    public void AParameterPlansNoContentsSubscriptionWhenConstantsListenForNeither()
    {
        var options = new ExpressionObserverOptions { ConstantExpressionsListenForCollectionChanged = false, ConstantExpressionsListenForDictionaryChanged = false };
        var parameter = Expression.Parameter(typeof(ObservableCollection<TestPerson>), "subject");
        var plan = Analyzer(options).Plan(parameter);
        Assert.IsTrue(plan.IsEligible);
        Assert.AreEqual(0, plan.Subscriptions.Count);
    }
}
