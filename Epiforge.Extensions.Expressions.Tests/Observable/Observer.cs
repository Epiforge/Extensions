namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class Observer
{
    [TestMethod]
    public void AlreadyCancelled()
    {
        var john = TestPerson.CreateJohn();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var observer = ExpressionObserverHelpers.Create();
        var johnsNameIsSixCharacters = observer.ConditionAsync(() => john.Name!.Length == 6, cancellationTokenSource.Token);
        Assert.IsTrue(johnsNameIsSixCharacters.IsCompleted);
        Assert.IsTrue(johnsNameIsSixCharacters.IsCanceled);
    }

    [TestMethod]
    public void AlreadyFaulted()
    {
        var john = TestPerson.CreateJohn();
        john.Name = null;
        var observer = ExpressionObserverHelpers.Create();
        var johnsNameIsSixCharacters = observer.ConditionAsync(() => john.Name!.Length == 6);
        Assert.IsTrue(johnsNameIsSixCharacters.IsCompleted);
        Assert.IsInstanceOfType(johnsNameIsSixCharacters.Exception, typeof(AggregateException));
#if IS_NET_7_0_OR_GREATER
        Assert.IsInstanceOfType(johnsNameIsSixCharacters.Exception!.InnerExceptions[0], typeof(TargetException));
#else
        Assert.IsInstanceOfType(johnsNameIsSixCharacters.Exception!.InnerExceptions[0], typeof(NullReferenceException));
#endif
    }

    [TestMethod]
    public void AlreadySucceeded()
    {
        var john = TestPerson.CreateJohn();
        john.Name = "Jan";
        var observer = ExpressionObserverHelpers.Create();
        var johnsSexChange = observer.ConditionAsync(() => john.Name == "Jan");
#if IS_NET_STANDARD_2_1_OR_GREATER
        Assert.IsTrue(johnsSexChange.IsCompletedSuccessfully);
#else
        Assert.IsTrue(johnsSexChange.IsCompleted);
        Assert.IsFalse(johnsSexChange.IsFaulted);
        Assert.IsFalse(johnsSexChange.IsCanceled);
#endif
    }

    [TestMethod]
    public void CancelledLater()
    {
        var john = TestPerson.CreateJohn();
        using var cancellationTokenSource = new CancellationTokenSource();
        var observer = ExpressionObserverHelpers.Create();
        var johnsNameIsSixCharacters = observer.ConditionAsync(() => john.Name!.Length == 6, cancellationTokenSource.Token);
        cancellationTokenSource.Cancel();
        Assert.IsTrue(johnsNameIsSixCharacters.IsCompleted);
        Assert.IsTrue(johnsNameIsSixCharacters.IsCanceled);
    }

    [TestMethod]
    public void Fault()
    {
        var john = TestPerson.CreateJohn();
        var observer = ExpressionObserverHelpers.Create();
        var johnsNameIsSixCharacters = observer.ConditionAsync(() => john.Name!.Length == 6);
        Assert.IsFalse(johnsNameIsSixCharacters.IsCompleted);
        john.Name = null;
        Assert.IsTrue(johnsNameIsSixCharacters.IsCompleted);
        Assert.IsInstanceOfType(johnsNameIsSixCharacters.Exception, typeof(AggregateException));
#if IS_NET_7_0_OR_GREATER
        Assert.IsInstanceOfType(johnsNameIsSixCharacters.Exception!.InnerExceptions[0], typeof(TargetException));
#else
        Assert.IsInstanceOfType(johnsNameIsSixCharacters.Exception!.InnerExceptions[0], typeof(NullReferenceException));
#endif
    }

    [TestMethod]
    public void Success()
    {
        var john = TestPerson.CreateJohn();
        var observer = ExpressionObserverHelpers.Create();
        var johnsSexChange = observer.ConditionAsync(() => john.Name == "Jan");
        Assert.IsFalse(johnsSexChange.IsCompleted);
        john.Name = "Jon";
        Assert.IsFalse(johnsSexChange.IsCompleted);
        john.Name = "Jan";
#if IS_NET_STANDARD_2_1_OR_GREATER
        Assert.IsTrue(johnsSexChange.IsCompletedSuccessfully);
#else
        Assert.IsTrue(johnsSexChange.IsCompleted);
        Assert.IsFalse(johnsSexChange.IsFaulted);
        Assert.IsFalse(johnsSexChange.IsCanceled);
#endif
    }

    [TestMethod]
    public void UnsupportedExpressionType()
    {
        var expr = Expression.Lambda<Func<int>>(Expression.Block(Expression.Constant(3)));
        Assert.AreEqual(3, expr.Compile()());
        var notSupportedThrown = false;
        var observer = ExpressionObserverHelpers.Create();
        try
        {
            using var observableExpr = observer.Observe(expr);
        }
        catch (NotSupportedException)
        {
            notSupportedThrown = true;
        }
        Assert.IsTrue(notSupportedThrown);
    }
}
