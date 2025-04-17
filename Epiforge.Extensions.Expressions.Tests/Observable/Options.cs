namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class Options
{
    #region Helper Classes

    public class TestObject : PropertyChangeNotifier
    {
        AsyncDisposableTestPerson? asyncDisposable;
        SyncDisposableTestPerson? syncDisposable;

        public SyncDisposableTestPerson? GetSyncDisposableMethod() =>
            syncDisposable;

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

        [SuppressMessage("Performance", "CA1822:Mark members as static")]
        public SyncDisposableTestPerson GetPersonNamedAfterType(Type type) =>
            new(type.Name);

        public SyncDisposableTestPerson GetPersonNamedAfterType<T>() =>
            GetPersonNamedAfterType(typeof(T));
    }

    #endregion Helper Classes

    [TestMethod]
    public void BlockOnAsyncDisposalEnabled()
    {
        AsyncDisposableTestPerson? person;
        var observer = ExpressionObserverHelpers.Create(new ExpressionObserverOptions { BlockOnAsyncDisposal = true });
        using (var expr = observer.Observe(() => new AsyncDisposableTestPerson()))
            person = expr.Evaluation.Result;
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.IsTrue(person!.IsDisposed);
    }

    [TestMethod]
    public void DisposalUnsupported()
    {
        var options = new ExpressionObserverOptions();
        var notSupportedThrown = false;
        var expr = Expression.Lambda<Func<int>>(Expression.Block(Expression.Constant(3)));
        try
        {
            options.AddExpressionValueDisposal(expr);
        }
        catch (NotSupportedException)
        {
            notSupportedThrown = true;
        }
        Assert.IsTrue(notSupportedThrown);
        notSupportedThrown = false;
        try
        {
            options.IsExpressionValueDisposed(expr);
        }
        catch (NotSupportedException)
        {
            notSupportedThrown = true;
        }
        Assert.IsTrue(notSupportedThrown);
        notSupportedThrown = false;
        try
        {
            options.RemoveExpressionValueDisposal(expr);
        }
        catch (NotSupportedException)
        {
            notSupportedThrown = true;
        }
        Assert.IsTrue(notSupportedThrown);
    }

    [TestMethod]
    public void DisposeBinaryResult()
    {
        var options = new ExpressionObserverOptions();
        Assert.IsTrue(options.AddExpressionValueDisposal(() => SyncDisposableTestPerson.CreateJohn() + SyncDisposableTestPerson.CreateEmily()));
        Assert.IsTrue(options.IsExpressionValueDisposed(() => SyncDisposableTestPerson.CreateJohn() + SyncDisposableTestPerson.CreateEmily()));
        Assert.IsTrue(options.RemoveExpressionValueDisposal(() => SyncDisposableTestPerson.CreateJohn() + SyncDisposableTestPerson.CreateEmily()));
    }

    [TestMethod]
    public void DisposeConstructedType()
    {
        var options = new ExpressionObserverOptions();
        Assert.IsTrue(options.AddConstructedTypeDisposal(typeof(SyncDisposableTestPerson)));
        Assert.IsTrue(options.IsConstructedTypeDisposed(typeof(SyncDisposableTestPerson)));
        Assert.IsTrue(options.RemoveConstructedTypeDisposal(typeof(SyncDisposableTestPerson)));
    }

    [TestMethod]
    public void DisposeConstructor()
    {
        var options = new ExpressionObserverOptions();
        Assert.IsTrue(options.AddExpressionValueDisposal(() => new SyncDisposableTestPerson()));
        Assert.IsTrue(options.IsExpressionValueDisposed(() => new SyncDisposableTestPerson()));
        Assert.IsTrue(options.RemoveExpressionValueDisposal(() => new SyncDisposableTestPerson()));
    }

    [TestMethod]
    public void DisposeGenericMethodReturnValue()
    {
        SyncDisposableTestPerson? person;
        using (var expr = ExpressionObserverHelpers.Create().Observe(() => new TestObject().GetPersonNamedAfterType<string>()))
        {
            person = expr.Evaluation.Result!;
            Assert.AreEqual(typeof(string).Name, person.Name);
        }
        Assert.IsFalse(person.IsDisposed);
        person.Dispose();
        var options1 = new ExpressionObserverOptions();
        options1.AddExpressionValueDisposal(() => default(TestObject)!.GetPersonNamedAfterType<int>());
        using (var expr = ExpressionObserverHelpers.Create(options1).Observe(() => new TestObject().GetPersonNamedAfterType<string>()))
        {
            person = expr.Evaluation.Result!;
            Assert.AreEqual(typeof(string).Name, person.Name);
        }
        Assert.IsFalse(person.IsDisposed);
        person.Dispose();
        var options2 = new ExpressionObserverOptions();
        options2.AddExpressionValueDisposal(() => default(TestObject)!.GetPersonNamedAfterType<int>(), true);
        using (var expr = ExpressionObserverHelpers.Create(options2).Observe(() => new TestObject().GetPersonNamedAfterType<string>()))
        {
            person = expr.Evaluation.Result!;
            Assert.AreEqual(typeof(string).Name, person.Name);
        }
        Assert.IsTrue(person.IsDisposed);
    }

    [TestMethod]
    public void DisposeIndexerValue()
    {
        var collectionType = typeof(ObservableCollection<SyncDisposableTestPerson>);
        var indexer = collectionType.GetProperty("Item");
        var options = new ExpressionObserverOptions();
        Assert.IsTrue(options.AddExpressionValueDisposal(Expression.Lambda<Func<SyncDisposableTestPerson>>(Expression.MakeIndex(Expression.New(collectionType), indexer, new Expression[] { Expression.Constant(0) }))));
        Assert.IsTrue(options.IsExpressionValueDisposed(Expression.Lambda<Func<SyncDisposableTestPerson>>(Expression.MakeIndex(Expression.New(collectionType), indexer, new Expression[] { Expression.Constant(0) }))));
        Assert.IsTrue(options.RemoveExpressionValueDisposal(Expression.Lambda<Func<SyncDisposableTestPerson>>(Expression.MakeIndex(Expression.New(collectionType), indexer, new Expression[] { Expression.Constant(0) }))));
    }

    [TestMethod]
    public void DisposeMethodReturnValue()
    {
        var options = new ExpressionObserverOptions();
        Assert.IsTrue(options.AddExpressionValueDisposal(() => new TestObject().GetSyncDisposableMethod()));
        Assert.IsTrue(options.IsExpressionValueDisposed(() => new TestObject().GetSyncDisposableMethod()));
        Assert.IsTrue(options.RemoveExpressionValueDisposal(() => new TestObject().GetSyncDisposableMethod()));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void DisposeNonGenericMethodReturnValueAsGenericFails() =>
        new ExpressionObserverOptions().AddExpressionValueDisposal(() => default(TestObject)!.GetPersonNamedAfterType(default!), true);

    [TestMethod]
    public void DisposePropertyValueByExpression()
    {
        var options = new ExpressionObserverOptions();
        Assert.IsTrue(options.AddExpressionValueDisposal(() => new ObservableCollection<SyncDisposableTestPerson>()[0]));
        Assert.IsTrue(options.IsExpressionValueDisposed(() => new ObservableCollection<SyncDisposableTestPerson>()[0]));
        Assert.IsTrue(options.RemoveExpressionValueDisposal(() => new ObservableCollection<SyncDisposableTestPerson>()[0]));
    }

    [TestMethod]
    public void DisposePropertyValueByReflection()
    {
        var testObjectType = typeof(TestObject);
        var property = testObjectType.GetProperty(nameof(TestObject.SyncDisposable))!;
        var options = new ExpressionObserverOptions();
        Assert.IsTrue(options.AddExpressionValueDisposal(Expression.Lambda<Func<SyncDisposableTestPerson>>(Expression.MakeMemberAccess(Expression.New(testObjectType), property))));
        Assert.IsTrue(options.IsExpressionValueDisposed(Expression.Lambda<Func<SyncDisposableTestPerson>>(Expression.MakeMemberAccess(Expression.New(testObjectType), property))));
        Assert.IsTrue(options.RemoveExpressionValueDisposal(Expression.Lambda<Func<SyncDisposableTestPerson>>(Expression.MakeMemberAccess(Expression.New(testObjectType), property))));
    }

    [TestMethod]
    public void DisposeUnaryResult()
    {
        var options = new ExpressionObserverOptions();
        Assert.IsTrue(options.AddExpressionValueDisposal(() => -SyncDisposableTestPerson.CreateEmily()));
        Assert.IsTrue(options.IsExpressionValueDisposed(() => -SyncDisposableTestPerson.CreateEmily()));
        Assert.IsTrue(options.RemoveExpressionValueDisposal(() => -SyncDisposableTestPerson.CreateEmily()));
    }

    [TestMethod]
    public void OptimizerAppliedDeMorgan()
    {
        var a = Expression.Parameter(typeof(bool));
        var b = Expression.Parameter(typeof(bool));
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe<bool>(Expression.Lambda<Func<bool, bool, bool>>(Expression.AndAlso(Expression.Not(a), Expression.Not(b)), a, b), false, false))
            Assert.AreEqual("Not((False OrElse False))", expr.ToString());
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public async Task PreferAsyncDisposalDisabledAsync()
    {
        DisposableTestPerson? person;
        var disposedTcs = new TaskCompletionSource<object?>();
        var observer = ExpressionObserverHelpers.Create(new ExpressionObserverOptions { BlockOnAsyncDisposal = true });
        using (var expr = observer.Observe(() => new DisposableTestPerson()))
        {
            person = expr.Evaluation.Result;
            person!.Disposed += (sender, e) => disposedTcs.SetResult(null);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        await Task.WhenAny(disposedTcs.Task, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.IsTrue(person.IsDisposed);
    }
}
