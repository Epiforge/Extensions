namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class ObservableUnaryExpression
{
    [TestMethod]
    public void Cast()
    {
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(p1 => (double)p1, 3))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            Assert.AreEqual(3D, expr.Evaluation.Result);
            Assert.IsInstanceOfType(expr.Evaluation.Result, typeof(double));
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void EvaluationFault()
    {
        TestPerson? noOne = null;
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(() => -noOne!))
            Assert.IsNotNull(expr.Evaluation.Fault);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void FaultPropagation()
    {
        var john = TestPerson.CreateJohn();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(p1 => -p1.Name!.Length, john))
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
    public void NullableConversion()
    {
        var john = TestPerson.CreateJohn();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(p1 => (p1 == null || p1.Name == null ? (int?)null : p1.Name.Length) + 3, john))
        {
            Assert.IsTrue(expr.Evaluation.Result == 7);
            Assert.IsNull(expr.Evaluation.Fault);
            john.Name = null;
            Assert.IsNull(expr.Evaluation.Result);
            Assert.IsNull(expr.Evaluation.Fault);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void PropertyChanges()
    {
        var john = TestPerson.CreateJohn();
        var values = new BlockingCollection<int>();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(p1 => -p1.Name!.Length, john))
        {
            void propertyChanged(object? sender, PropertyChangedEventArgs e) =>
                values.Add(expr.Evaluation.Result);
            expr.PropertyChanged += propertyChanged;
            values.Add(expr.Evaluation.Result);
            john.Name = "J";
            john.Name = "John";
            john.Name = "Jon";
            john.Name = "Jhn";
            john.Name = string.Empty;
            expr.PropertyChanged -= propertyChanged;
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.IsTrue(new int[] { -4, -1, -4, -3, 0 }.SequenceEqual(values));
    }

    [TestMethod]
    public async Task ValueAsyncDisposalAsync()
    {
        var people = new ObservableCollection<AsyncDisposableTestPerson>
        {
            AsyncDisposableTestPerson.CreateJohn(),
        };
        AsyncDisposableTestPerson? newPerson;
        var disposedTcs = new TaskCompletionSource<object?>();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(p => -p[0], people))
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
            SyncDisposableTestPerson.CreateJohn()
        };
        SyncDisposableTestPerson? newPerson;
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(p => -p[0], people))
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
