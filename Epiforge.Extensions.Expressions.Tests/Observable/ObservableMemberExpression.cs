namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class ObservableMemberExpression
{
    #region TestMethod Classes

    public class TestObject :
        PropertyChangeNotifier
    {
        AsyncDisposableTestPerson? asyncDisposable;
        SyncDisposableTestPerson? syncDisposable;
        bool unheard;

        public AsyncDisposableTestPerson? AsyncDisposable
        {
            get => asyncDisposable;
            set => SetBackedProperty(ref asyncDisposable, in value);
        }

        public SyncDisposableTestPerson? SyncDisposable
        {
            get => syncDisposable;
            set => SetBackedProperty(ref syncDisposable, in value);
        }

        public bool Unheard
        {
            get => unheard;
            set => SetBackedProperty(ref unheard, in value);
        }
    }

    #endregion TestMethod Classes

    [TestMethod]
    public void Closure()
    {
        var x = 3;
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(p1 => p1.Name == null ? x : emily.Name!.Length, john))
        {
            Assert.AreEqual(5, expr.Evaluation.Result);
            john.Name = null;
            Assert.AreEqual(3, expr.Evaluation.Result);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void DoNotListen()
    {
        var test = new TestObject { Unheard = true };
        var options = new ExpressionObserverOptions();
        options.AddIgnoredPropertyChangeNotification(typeof(TestObject).GetProperty(nameof(TestObject.Unheard))!);
        var observer = ExpressionObserverHelpers.Create(options);
        using (var expr = observer.Observe(p1 => p1.Unheard, test))
        {
            Assert.IsTrue(expr.Evaluation.Result);
            test.Unheard = false;
            Assert.IsTrue(expr.Evaluation.Result);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void FieldValue()
    {
        var team = (developer: TestPerson.CreateJohn(), artist: TestPerson.CreateEmily());
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(p1 => p1.artist.Name, team))
            Assert.AreEqual("Emily", expr.Evaluation.Result);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void ObjectFaultPropagation()
    {
        var john = TestPerson.CreateJohn();
        var emily = TestPerson.CreateEmily();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe((p1, p2) => (p1.Name!.Length > 0 ? p1 : p2).Name, john, emily))
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
    public void StaticPropertyValue()
    {
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(() => Environment.UserName))
            Assert.AreEqual(Environment.UserName, expr.Evaluation.Result);
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public async Task ValueAsyncDisposalAsync()
    {
        var john = AsyncDisposableTestPerson.CreateJohn();
        var emily = AsyncDisposableTestPerson.CreateEmily();
        var testObject = new TestObject { AsyncDisposable = john };
        var disposedTcs = new TaskCompletionSource<object?>();
        var options = new ExpressionObserverOptions();
        options.AddExpressionValueDisposal(() => new TestObject().AsyncDisposable);
        var observer = ExpressionObserverHelpers.Create(options);
        using (var expr = observer.Observe(p1 => p1.AsyncDisposable, testObject))
        {
            Assert.AreSame(john, expr.Evaluation.Result);
            Assert.IsFalse(john.IsDisposed);
            john.Disposed += (sender, e) => disposedTcs.SetResult(null);
            testObject.AsyncDisposable = emily;
            await Task.WhenAny(disposedTcs.Task, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.AreSame(emily, expr.Evaluation.Result);
            Assert.IsFalse(emily.IsDisposed);
            Assert.IsTrue(john.IsDisposed);
            disposedTcs = new TaskCompletionSource<object?>();
            emily.Disposed += (sender, e) => disposedTcs.SetResult(null);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        await Task.WhenAny(disposedTcs.Task, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.IsTrue(emily.IsDisposed);
    }

    [TestMethod]
    public void ValueDisposal()
    {
        var john = SyncDisposableTestPerson.CreateJohn();
        var emily = SyncDisposableTestPerson.CreateEmily();
        var testObject = new TestObject { SyncDisposable = john };
        var options = new ExpressionObserverOptions();
        options.AddExpressionValueDisposal(() => new TestObject().SyncDisposable);
        var observer = ExpressionObserverHelpers.Create(options);
        using (var expr = observer.Observe(p1 => p1.SyncDisposable, testObject))
        {
            Assert.AreSame(john, expr.Evaluation.Result);
            Assert.IsFalse(john.IsDisposed);
            testObject.SyncDisposable = emily;
            Assert.AreSame(emily, expr.Evaluation.Result);
            Assert.IsFalse(emily.IsDisposed);
            Assert.IsTrue(john.IsDisposed);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.IsTrue(emily.IsDisposed);
    }
}
