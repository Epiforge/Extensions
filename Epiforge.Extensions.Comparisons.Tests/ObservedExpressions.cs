namespace Epiforge.Extensions.Comparisons.Tests;

/// <summary>
/// What an observed expression announces, in the three libraries which observe one, for shapes where they differ: a value read twice, a value which changes without the expression's changing, an element read by index, a query written inside the expression, and a try
/// </summary>
/// <remarks>
/// Each test records the values announced after the observation is built, then asserts the value it started with, what it announced and where it ended. DynamicData observes no expression, so it has no tests here
/// </remarks>
[TestClass]
public class ObservedExpressions
{
    sealed record Record<T>(T Start, string Announced, T Final);

    static void ChangeCount(ObservableCollection<Person> people)
    {
        people[2].Rank = 3;
        people.Add(new Person("P3", 1));
    }

    static void ChangeFirst(ObservableCollection<Person> people)
    {
        people[0] = new Person("X", 9);
        people[0].Rank = 4;
        people.Insert(0, new Person("Y", 5));
    }

    static Record<T> Expressions<T>(Expression<Func<T>> expression, Action change)
    {
        using var observation = new ExpressionObserver().Observe(expression);
        var announced = new List<string>();
        observation.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(IObservableExpression<T>.Evaluation))
                announced.Add($"{observation.Evaluation.Result}");
        };
        var start = observation.Evaluation.Result!;
        change();
        return new(start, string.Join(",", announced), observation.Evaluation.Result!);
    }

    [TestMethod]
    public void ExpressionsAnnouncesAValueReadTwiceOnce()
    {
        var person = new Person("A", 1);
        Assert.AreEqual(new Record<int>(3, "6", 6), Expressions(() => person.Rank * 2 + person.Rank, () => person.Rank = 2));
    }

    [TestMethod]
    public void ExpressionsAnnouncesNothingWhenTheValueDoesNotChange()
    {
        var person = new Person("A", 1);
        Assert.AreEqual(new Record<bool>(true, string.Empty, true), Expressions(() => person.Rank > 0, () => { person.Rank = 2; person.Rank = 3; }));
    }

    [TestMethod]
    public void ExpressionsCountsWithAQueryRatherThanInsideAnExpression()
    {
        var people = Person.Many(1, 2, -3);
        using var observation = new CollectionObserver().ObserveReadOnlyList(people);
        using var count = observation.ObserveCount(person => person.Rank > 0);
        var announced = new List<int>();
        count.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(IObservableScalarQuery<int>.Evaluation))
                announced.Add(count.Evaluation.Result);
        };
        ChangeCount(people);
        CollectionAssert.AreEqual(new[] { 3, 4 }, announced);
    }

    [TestMethod]
    public void ExpressionsFollowsAnElementReadByIndex()
    {
        var people = Person.Many(1, 2);
        Assert.AreEqual(new Record<int>(1, "9,4,5", 5), Expressions(() => people[0].Rank, () => ChangeFirst(people)));
    }

    [TestMethod]
    public void ExpressionsFollowsATry()
    {
        var person = new Person("A", 1) { Score = 5 };
        Assert.AreEqual(new Record<int>(2, "-1,5", 5), Expressions(Guarded(person), () => Unguard(person)));
    }

    [TestMethod]
    public void ExpressionsRefusesAQueryWrittenInsideAnExpression()
    {
        var people = Person.Many(1, 2, -3);
        Assert.ThrowsException<NotSupportedException>(() => Expressions(() => people.Count(person => person.Rank > 0), () => ChangeCount(people)));
    }

    static Expression<Func<int>> Guarded(Person person) =>
        Expression.Lambda<Func<int>>(Expression.TryCatch(Expression.Divide(Expression.Constant(10), Expression.Property(Expression.Constant(person), nameof(Person.Score))), Expression.Catch(typeof(DivideByZeroException), Expression.Constant(-1))));

    static Record<T> Nmf<T>(Expression<Func<T>> expression, Action change)
    {
        using var observation = NMF.Expressions.Observable.Expression(expression);
        observation.Successors.SetDummy();
        var announced = new List<string>();
        observation.ValueChanged += (sender, e) => announced.Add($"{observation.Value}");
        var start = observation.Value;
        change();
        return new(start, string.Join(",", announced), observation.Value);
    }

    [TestMethod]
    public void NmfAnnouncesAValueReadTwiceOncePerRead()
    {
        var person = new Person("A", 1);
        Assert.AreEqual(new Record<int>(3, "5,6", 6), Nmf(() => person.Rank * 2 + person.Rank, () => person.Rank = 2));
    }

    [TestMethod]
    public void NmfAnnouncesNothingWhenTheValueDoesNotChange()
    {
        var person = new Person("A", 1);
        Assert.AreEqual(new Record<bool>(true, string.Empty, true), Nmf(() => person.Rank > 0, () => { person.Rank = 2; person.Rank = 3; }));
    }

    [TestMethod]
    public void NmfFollowsAnElementReadByIndex()
    {
        var people = Person.Many(1, 2);
        Assert.AreEqual(new Record<int>(1, "9,4,5", 5), Nmf(() => people[0].Rank, () => ChangeFirst(people)));
    }

    [TestMethod]
    public void NmfFollowsAQueryWrittenInsideAnExpressionOverASequenceWithUpdates()
    {
        var people = Person.Many(1, 2, -3);
        var withUpdates = ((IEnumerable<Person>)people).WithUpdates();
        Assert.AreEqual(new Record<int>(2, "3,4", 4), Nmf(() => withUpdates.Count(person => person.Rank > 0), () => ChangeCount(people)));
    }

    [TestMethod]
    public void NmfRefusesAQueryWrittenInsideAnExpressionOverAPlainSequence()
    {
        var people = Person.Many(1, 2, -3);
        var thrown = Assert.ThrowsException<TargetInvocationException>(() => Nmf(() => people.Count(person => person.Rank > 0), () => ChangeCount(people)));
        Assert.IsInstanceOfType<InvalidOperationException>(thrown.InnerException);
    }

    [TestMethod]
    public void NmfRefusesATry()
    {
        var person = new Person("A", 1) { Score = 5 };
        Assert.ThrowsException<NotSupportedException>(() => Nmf(Guarded(person), () => Unguard(person)));
    }

    static Record<T> ObservableComputations<T>(Expression<Func<T>> expression, Action change)
    {
        using var consumer = new OcConsumer();
        var observation = new Computing<T>(expression).For(consumer);
        var announced = new List<string>();
        observation.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(Computing<T>.Value))
                announced.Add($"{observation.Value}");
        };
        var start = observation.Value;
        change();
        return new(start, string.Join(",", announced), observation.Value);
    }

    [TestMethod]
    public void ObservableComputationsAnnouncesAValueReadTwiceOnce()
    {
        var person = new Person("A", 1);
        Assert.AreEqual(new Record<int>(3, "6", 6), ObservableComputations(() => person.Rank * 2 + person.Rank, () => person.Rank = 2));
    }

    [TestMethod]
    public void ObservableComputationsAnnouncesWhenTheValueDoesNotChange()
    {
        var person = new Person("A", 1);
        Assert.AreEqual(new Record<bool>(true, "True,True", true), ObservableComputations(() => person.Rank > 0, () => { person.Rank = 2; person.Rank = 3; }));
    }

    [TestMethod]
    public void ObservableComputationsDoesNotFollowALinqQueryWrittenInsideAnExpression()
    {
        var people = Person.Many(1, 2, -3);
        Assert.AreEqual(new Record<int>(2, string.Empty, 2), ObservableComputations(() => people.Count(person => person.Rank > 0), () => ChangeCount(people)));
    }

    [TestMethod]
    public void ObservableComputationsDoesNotFollowAnElementReadByIndex()
    {
        var people = Person.Many(1, 2);
        Assert.AreEqual(new Record<int>(1, string.Empty, 1), ObservableComputations(() => people[0].Rank, () => ChangeFirst(people)));
    }

    [TestMethod]
    public void ObservableComputationsFollowsAQueryOfItsOwnWrittenInsideAnExpression()
    {
        var people = Person.Many(1, 2, -3);
        Assert.AreEqual(new Record<int>(2, "3,4", 4), ObservableComputations(() => people.Filtering(person => person.Rank > 0).Count, () => ChangeCount(people)));
    }

    [TestMethod]
    public void ObservableComputationsFollowsATry()
    {
        var person = new Person("A", 1) { Score = 5 };
        Assert.AreEqual(new Record<int>(2, "-1,5", 5), ObservableComputations(Guarded(person), () => Unguard(person)));
    }

    static void Unguard(Person person)
    {
        person.Score = 0;
        person.Score = 2;
    }
}
