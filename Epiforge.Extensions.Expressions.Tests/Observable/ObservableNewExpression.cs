namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class ObservableNewExpression
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ArgumentFaultPropagation(bool useDirectSubscription)
    {
        var john = TestPerson.CreateJohn();
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expr = observer.Observe(() => new TestPerson(john.Name!.Length.ToString())))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            john.Name = null;
            Assert.IsNotNull(expr.Evaluation.Fault);
            john.Name = "John";
            Assert.IsNull(expr.Evaluation.Fault);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EvaluationFault(bool useDirectSubscription)
    {
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription);
        using (var expr = observer.Observe(() => new EquatableList<string>(null!)))
            Assert.IsNotNull(expr.Evaluation.Fault);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ValueAsyncDisposalAsync(bool useDirectSubscription)
    {
        var john = AsyncDisposableTestPerson.CreateJohn();
        AsyncDisposableTestPerson? first, second;
        var disposedTcs = new TaskCompletionSource<object?>();
        var options = new ExpressionObserverOptions();
        options.AddConstructedTypeDisposal(typeof(AsyncDisposableTestPerson));
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription, options);
        using (var expr = observer.Observe(() => new AsyncDisposableTestPerson(john.Name!.Length.ToString())))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            first = expr.Evaluation.Result;
            Assert.IsFalse(first!.IsDisposed);
            first.Disposed += (sender, e) => disposedTcs.SetResult(null);
            john.Name = string.Empty;
            await Task.WhenAny(disposedTcs.Task, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.IsNull(expr.Evaluation.Fault);
            second = expr.Evaluation.Result;
            Assert.IsFalse(second!.IsDisposed);
            Assert.IsTrue(first.IsDisposed);
            disposedTcs = new TaskCompletionSource<object?>();
            second.Disposed += (sender, e) => disposedTcs.SetResult(null);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        await Task.WhenAny(disposedTcs.Task, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.IsTrue(second.IsDisposed);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ValueDisposal(bool useDirectSubscription)
    {
        var john = SyncDisposableTestPerson.CreateJohn();
        SyncDisposableTestPerson? first, second;
        var options = new ExpressionObserverOptions();
        options.AddConstructedTypeDisposal(typeof(SyncDisposableTestPerson));
        var observer = ExpressionObserverHelpers.Create(useDirectSubscription, options);
        using (var expr = observer.Observe(() => new SyncDisposableTestPerson(john.Name!.Length.ToString())))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            first = expr.Evaluation.Result;
            Assert.IsFalse(first!.IsDisposed);
            john.Name = string.Empty;
            Assert.IsNull(expr.Evaluation.Fault);
            second = expr.Evaluation.Result;
            Assert.IsFalse(second!.IsDisposed);
            Assert.IsTrue(first.IsDisposed);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.IsTrue(second.IsDisposed);
    }
}
