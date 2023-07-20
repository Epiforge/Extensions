namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class ObservableMemberInitExpression
{
    #region TestMethod Classes

    public class FieldyTestPerson
    {
        public string? Name;

        public override string ToString() =>
            $"{{{Name}}}";
    }

    public class ThrowyTestPerson :
        PropertyChangeNotifier
    {
        public ThrowyTestPerson(string name)
        {
            if (name is null)
                throw new ArgumentNullException(nameof(name));
            this.name = name;
        }

        string name;

        public override string ToString() =>
            $"{{{name}}}";

        public string Name
        {
            get => name;
            set
            {
                if (value is null)
                    throw new NullReferenceException();
                SetBackedProperty(ref name, in value);
            }
        }
    }

    public struct StructyTestPerson
    {
        public StructyTestPerson(string? name) =>
            Name = name;

        public string? Name;

        public override string ToString() =>
            $"{{{Name}}}";
    }

    #endregion

    [TestMethod]
    public void ClassFields()
    {
        var emily = TestPerson.CreateEmily();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(() => new FieldyTestPerson { Name = emily.Name! }))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            Assert.IsNotNull(expr.Evaluation.Result);
            Assert.AreEqual("Emily", expr.Evaluation.Result!.Name);
            emily.Name = "Em";
            Assert.IsNull(expr.Evaluation.Fault);
            Assert.IsNotNull(expr.Evaluation.Result);
            Assert.AreEqual("Em", expr.Evaluation.Result!.Name);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void ClassProperties()
    {
        var emily = TestPerson.CreateEmily();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(() => new TestPerson { Name = emily.Name! }))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            Assert.IsNotNull(expr.Evaluation.Result);
            Assert.AreEqual("Emily", expr.Evaluation.Result!.Name);
            emily.Name = "Em";
            Assert.IsNull(expr.Evaluation.Fault);
            Assert.IsNotNull(expr.Evaluation.Result);
            Assert.AreEqual("Em", expr.Evaluation.Result!.Name);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void MemberAssignmentFaultPropagation()
    {
        var emily = TestPerson.CreateEmily();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(() => new ThrowyTestPerson("Emily") { Name = emily.Name! }))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            Assert.IsInstanceOfType(expr.Evaluation.Result, typeof(ThrowyTestPerson));
            emily.Name = null;
            Assert.IsNotNull(expr.Evaluation.Fault);
            Assert.IsNull(expr.Evaluation.Result);
            emily.Name = "Emily";
            Assert.IsNull(expr.Evaluation.Fault);
            Assert.IsInstanceOfType(expr.Evaluation.Result, typeof(ThrowyTestPerson));
            Assert.AreEqual(emily.Name, expr.Evaluation.Result!.Name);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void NewFaultPropagation()
    {
        var emily = TestPerson.CreateEmily();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(() => new ThrowyTestPerson(emily.Name!) { Name = "Emily" }))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            Assert.IsInstanceOfType(expr.Evaluation.Result, typeof(ThrowyTestPerson));
            emily.Name = null;
            Assert.IsNotNull(expr.Evaluation.Fault);
            Assert.IsNull(expr.Evaluation.Result);
            emily.Name = "Emily";
            Assert.IsNull(expr.Evaluation.Fault);
            Assert.IsInstanceOfType(expr.Evaluation.Result, typeof(ThrowyTestPerson));
            Assert.AreEqual(emily.Name, expr.Evaluation.Result!.Name);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    [ExpectedException(typeof(NotSupportedException))]
    public void StructFields() =>
        ExpressionObserverHelpers.Create().Observe(() => new StructyTestPerson { Name = TestPerson.CreateEmily().Name! });

    [TestMethod]
    public async Task ValueAsyncDisposalAsync()
    {
        AsyncDisposableTestPerson? emily;
        var disposedTcs = new TaskCompletionSource<object?>();
        var options = new ExpressionObserverOptions();
        options.AddExpressionValueDisposal(() => new AsyncDisposableTestPerson());
        var observer = ExpressionObserverHelpers.Create(options);
        using (var expr = observer.Observe(() => new AsyncDisposableTestPerson { Name = "Emily" }))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            emily = expr.Evaluation.Result;
            Assert.IsFalse(emily!.IsDisposed);
            emily.Disposed += (sender, e) => disposedTcs.SetResult(null);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        await Task.WhenAny(disposedTcs.Task, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.IsTrue(emily.IsDisposed);
    }

    [TestMethod]
    public void ValueDisposal()
    {
        SyncDisposableTestPerson? emily;
        var options = new ExpressionObserverOptions();
        options.AddExpressionValueDisposal(() => new SyncDisposableTestPerson());
        var observer = ExpressionObserverHelpers.Create(options);
        using (var expr = observer.Observe(() => new SyncDisposableTestPerson { Name = "Emily" }))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            emily = expr.Evaluation.Result;
            Assert.IsFalse(emily!.IsDisposed);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.IsTrue(emily.IsDisposed);
    }
}
