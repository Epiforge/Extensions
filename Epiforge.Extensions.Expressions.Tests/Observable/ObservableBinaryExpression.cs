namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class ObservableBinaryExpression
{
    [TestMethod]
    public void EvaluationFault()
    {
        var john = TestPerson.CreateJohn();
        TestPerson? noOne = null;
        var observer = ExpressionObserverHelpers.Create();
#pragma warning disable CS8604 // Possible null reference argument.
        using (var expr = observer.Observe(() => john + noOne))
#pragma warning restore CS8604 // Possible null reference argument.
            Assert.IsNotNull(expr.Evaluation.Fault);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void FaultPropagation()
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe((p1, p2) => p1.Name!.Length + p2.Name!.Length, john, emily))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            john.Name = null;
            Assert.IsNotNull(expr.Evaluation.Fault);
            emily.Name = null;
            john.Name = string.Empty;
            Assert.IsNotNull(expr.Evaluation.Fault);
            emily.Name = "Emily";
            Assert.IsNull(expr.Evaluation.Fault);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void PropertyChanges()
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var values = new BlockingCollection<int>();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe((p1, p2) => p1.Name!.Length + p2.Name!.Length, john, emily))
        {
            void propertyChanged(object? sender, PropertyChangedEventArgs e) => values.Add(expr.Evaluation.Result);
            expr.PropertyChanged += propertyChanged;
            values.Add(expr.Evaluation.Result);
            john.Name = "J";
            emily.Name = "E";
            john.Name = "John";
            john.Name = "J";
            emily.Name = "Emily";
            emily.Name = "E";
            expr.PropertyChanged -= propertyChanged;
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.IsTrue(new int[] { 9, 6, 2, 5, 2, 6, 2 }.SequenceEqual(values));
    }

    [TestMethod]
    public async Task ValueAsyncDisposalAsync()
    {
        var people = new ObservableCollection<AsyncDisposableTestPerson>
        {
            AsyncDisposableTestPerson.CreateJohn(),
            AsyncDisposableTestPerson.CreateEmily()
        };
        var disposedTcs = new TaskCompletionSource<object?>();
        AsyncDisposableTestPerson? newPerson;
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(p => p[0] + p[1], people))
        {
            newPerson = expr.Evaluation.Result;
            Assert.IsFalse(newPerson!.IsDisposed);
            newPerson.Disposed += (sender, e) => disposedTcs.SetResult(null);
            people[0] = AsyncDisposableTestPerson.CreateJohn();
            await Task.WhenAny(disposedTcs.Task, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.IsTrue(newPerson.IsDisposed);
            newPerson = expr.Evaluation.Result;
            Assert.IsFalse(newPerson!.IsDisposed);
            disposedTcs = new TaskCompletionSource<object?>();
            newPerson.Disposed += (sender, e) => disposedTcs.SetResult(null);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        await Task.WhenAny(disposedTcs.Task, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.IsTrue(newPerson.IsDisposed);
    }

    [TestMethod]
    public void ValueDisposal()
    {
        var people = new ObservableCollection<SyncDisposableTestPerson>
        {
            SyncDisposableTestPerson.CreateJohn(),
            SyncDisposableTestPerson.CreateEmily()
        };
        SyncDisposableTestPerson? newPerson;
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(p => p[0] + p[1], people))
        {
            newPerson = expr.Evaluation.Result;
            Assert.IsFalse(newPerson!.IsDisposed);
            people[0] = SyncDisposableTestPerson.CreateJohn();
            Assert.IsTrue(newPerson.IsDisposed);
            newPerson = expr.Evaluation.Result;
            Assert.IsFalse(newPerson!.IsDisposed);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.IsTrue(newPerson.IsDisposed);
    }
}
