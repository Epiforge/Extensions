namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class ObservableMethodCallExpression
{
    #region TestMethod Methods

    public static AsyncDisposableTestPerson CombineAsyncDisposablePeople(AsyncDisposableTestPerson a, AsyncDisposableTestPerson b) => new() { Name = $"{a.Name} {b.Name}" };

    [SuppressMessage("Performance", "CA1822:Mark members as static")]
    public TestPerson CombinePeople(TestPerson a, TestPerson b) => new() { Name = $"{a.Name} {b.Name}" };

    public static SyncDisposableTestPerson CombineSyncDisposablePeople(SyncDisposableTestPerson a, SyncDisposableTestPerson b) => new() { Name = $"{a.Name} {b.Name}" };

    #endregion TestMethod Methods

    [TestMethod]
    public void ActuallyAProperty()
    {
        var emily = TestPerson.CreateEmily();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(Expression.Lambda<Func<string>>(Expression.Call(Expression.Constant(emily), typeof(TestPerson).GetProperty(nameof(TestPerson.Name))!.GetMethod!))))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            Assert.AreEqual("Emily", expr.Evaluation.Result);
            emily.Name = "E";
            Assert.IsNull(expr.Evaluation.Fault);
            Assert.AreEqual("E", expr.Evaluation.Result);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void ArgumentFaultPropagation()
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(() => CombinePeople(john.Name!.Length > 3 ? john : null!, emily)))
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
    public void ObjectFaultPropagation()
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(() => (john.Name!.Length > 3 ? this : null!).CombinePeople(john, emily)))
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
    public async Task ValueAsyncDisposalAsync()
    {
        var john = AsyncDisposableTestPerson.CreateJohn();
        var emily = AsyncDisposableTestPerson.CreateEmily();
        AsyncDisposableTestPerson? first, second;
        var disposedTcs = new TaskCompletionSource<object?>();
        var options = new ExpressionObserverOptions();
        options.AddExpressionValueDisposal(() => CombineAsyncDisposablePeople(null!, null!));
        var observer = ExpressionObserverHelpers.Create(options);
        using (var expr = observer.Observe(() => CombineAsyncDisposablePeople(john.Name!.Length > 3 ? john : emily, emily)))
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
        await Task.WhenAny(disposedTcs.Task, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.IsTrue(second.IsDisposed);
    }

    [TestMethod]
    public void ValueDisposal()
    {
        var john = SyncDisposableTestPerson.CreateJohn();
        var emily = SyncDisposableTestPerson.CreateEmily();
        SyncDisposableTestPerson? first, second;
        var options = new ExpressionObserverOptions();
        options.AddExpressionValueDisposal(() => CombineSyncDisposablePeople(null!, null!));
        var observer = ExpressionObserverHelpers.Create(options);
        using (var expr = observer.Observe(() => CombineSyncDisposablePeople(john.Name!.Length > 3 ? john : emily, emily)))
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
        Assert.IsTrue(second.IsDisposed);
    }
}
