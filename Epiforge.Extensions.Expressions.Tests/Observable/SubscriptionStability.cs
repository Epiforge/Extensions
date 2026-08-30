namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class SubscriptionStability
{
    static Expression BodyOf<TResult>(Expression<Func<Watched, TResult>> expression) =>
        expression.Body;

    static Epiforge.Extensions.Expressions.Observable.DirectSubscriptionAnalyzer Analyzer() =>
        new();

    [TestMethod]
    public void EligibleExpressionKeepsOneFixedSubscription()
    {
        Assert.IsTrue(Analyzer().Analyze(BodyOf<int>(watched => watched.Rank * 2)).IsEligible);
        var subject = new Watched();
        var observer = new ExpressionObserver();
        using (var expr = observer.Observe(watched => watched.Rank * 2, subject))
        {
            Assert.AreEqual(1, subject.Handlers);
            subject.Rank = 1;
            subject.Rank = 2;
            subject.Rank = 3;
            Assert.AreEqual(6, expr.Evaluation.Result);
            Assert.AreEqual(1, subject.Handlers);
        }
        Assert.AreEqual(0, subject.Handlers);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void IneligibleExpressionMovesItsSubscriptionToTheNewTarget()
    {
        var analysis = Analyzer().Analyze(BodyOf<int>(watched => watched.Next!.Rank));
        Assert.IsFalse(analysis.IsEligible);
        Assert.AreEqual(DirectSubscriptionIneligibility.ChangeableMemberTarget, analysis.Ineligibility);
        var first = new Watched { Rank = 1 };
        var second = new Watched { Rank = 2 };
        var subject = new Watched { Next = first };
        var observer = new ExpressionObserver();
        using (var expr = observer.Observe(watched => watched.Next!.Rank, subject))
        {
            Assert.AreEqual(1, subject.Handlers);
            Assert.AreEqual(1, first.Handlers);
            Assert.AreEqual(0, second.Handlers);
            Assert.AreEqual(1, expr.Evaluation.Result);
            subject.Next = second;
            Assert.AreEqual(1, subject.Handlers);
            Assert.AreEqual(0, first.Handlers);
            Assert.AreEqual(1, second.Handlers);
            Assert.AreEqual(2, expr.Evaluation.Result);
        }
        Assert.AreEqual(0, subject.Handlers);
        Assert.AreEqual(0, second.Handlers);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }
}

public class Watched :
    INotifyPropertyChanged
{
    Watched? next;
    PropertyChangedEventHandler? propertyChanged;
    int rank;

    public int Handlers { get; private set; }

    public Watched? Next
    {
        get => next;
        set
        {
            next = value;
            propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Next)));
        }
    }

    public int Rank
    {
        get => rank;
        set
        {
            rank = value;
            propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Rank)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add
        {
            ++Handlers;
            propertyChanged += value;
        }
        remove
        {
            --Handlers;
            propertyChanged -= value;
        }
    }
}
