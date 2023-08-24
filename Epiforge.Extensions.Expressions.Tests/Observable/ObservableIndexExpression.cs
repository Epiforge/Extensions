namespace Epiforge.Extensions.Expressions.Tests.Observable;

[TestClass]
public class ObservableIndexExpression
{
    public class TestObservableRangeCollection<T> :
        ObservableRangeCollection<T>
    {
        public TestObservableRangeCollection() : base()
        {
        }

        public TestObservableRangeCollection(IEnumerable<T> collection) : base(collection)
        {
        }

        public void ChangeElementAndOnlyNotifyProperty(int index, T value)
        {
            Items[index] = value;
            OnPropertyChanged(new PropertyChangedEventArgs("Item"));
        }
    }

    [TestMethod]
    public void ArgumentChanges()
    {
        var reversedNumbersList = Enumerable.Range(1, 10).Reverse().ToImmutableList();
        var john = TestPerson.CreateJohn();
        var values = new BlockingCollection<int>();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe((p1, p2) => p1[p2.Name!.Length], reversedNumbersList, john))
        {
            void propertyChanged(object? sender, PropertyChangedEventArgs e) =>
                values.Add(expr.Evaluation.Result);
            expr.PropertyChanged += propertyChanged;
            values.Add(expr.Evaluation.Result);
            john.Name = "J";
            john.Name = "Joh";
            john.Name = string.Empty;
            john.Name = "Johnny";
            john.Name = "John";
            expr.PropertyChanged -= propertyChanged;
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.IsTrue(new int[] { 6, 9, 7, 10, 4, 6 }.SequenceEqual(values));
    }

    [TestMethod]
    public void ArgumentFaultPropagation()
    {
        var numbers = new ObservableCollection<int>(Enumerable.Range(0, 10));
        var john = TestPerson.CreateJohn();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe((p1, p2) => p1[p2.Name!.Length], numbers, john))
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
    public void CollectionChanges()
    {
        var numbers = new ObservableRangeCollection<int>(Enumerable.Range(1, 10));
        var values = new BlockingCollection<int>();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(p1 => p1[5], numbers))
        {
            void propertyChanged(object? sender, PropertyChangedEventArgs e) =>
                values.Add(expr.Evaluation.Result);
            expr.PropertyChanged += propertyChanged;
            values.Add(expr.Evaluation.Result);
            numbers.Add(11);
            numbers.Insert(0, 0);
            numbers.Remove(11);
            numbers.Remove(0);
            numbers[4] = 50;
            numbers[4] = 5;
            numbers[5] = 60;
            numbers[5] = 6;
            numbers[6] = 70;
            numbers[6] = 7;
            numbers.Move(0, 1);
            numbers.Move(0, 1);
            numbers.MoveRange(0, 5, 5);
            numbers.MoveRange(0, 5, 5);
            numbers.MoveRange(5, 0, 5);
            numbers.MoveRange(5, 0, 5);
            numbers.Reset(numbers.Select(i => i * 10).ToImmutableArray());
            expr.PropertyChanged -= propertyChanged;
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.IsTrue(new int[] { 6, 5, 6, 60, 6, 1, 6, 1, 6, 60 }.SequenceEqual(values));
    }

    [TestMethod]
    public void DictionaryChanges()
    {
        var perfectNumbers = new ObservableDictionary<int, int>(Enumerable.Range(1, 10).ToDictionary(i => i, i => i * i));
        var values = new BlockingCollection<int>();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(p1 => p1[5], perfectNumbers))
        {
            void propertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(IObservableExpression<object?>.Evaluation))
                    values.Add(expr.Evaluation.Result);
            }

            expr.PropertyChanged += propertyChanged;
            values.Add(expr.Evaluation.Result);
            perfectNumbers.Add(11, 11 * 11);
            perfectNumbers.AddRange(Enumerable.Range(12, 3).ToDictionary(i => i, i => i * i));
            perfectNumbers.Remove(11);
            perfectNumbers.RemoveRange(Enumerable.Range(12, 3));
            perfectNumbers.Remove(5);
            perfectNumbers.Add(5, 30);
            perfectNumbers[5] = 25;
            perfectNumbers.RemoveRange(Enumerable.Range(4, 3));
            perfectNumbers.AddRange(Enumerable.Range(4, 3).ToDictionary(i => i, i => i * i));
            perfectNumbers.Clear();
            expr.PropertyChanged -= propertyChanged;
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
        Assert.IsTrue(new int[] { 25, 0, 0, 30, 25, 0, 0, 25, 0, 0 }.SequenceEqual(values));
    }

    [TestMethod]
    public void ManualCreation()
    {
        var people = new List<TestPerson>() { TestPerson.CreateEmily() };
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(Expression.Lambda<Func<string>>(Expression.MakeMemberAccess(Expression.MakeIndex(Expression.Constant(people), typeof(List<TestPerson>).GetProperties().First(p => p.GetIndexParameters().Length > 0), new Expression[] { Expression.Constant(0) }), typeof(TestPerson).GetProperty(nameof(TestPerson.Name))!))))
        {
            Assert.IsNull(expr.Evaluation.Fault);
            Assert.AreEqual("Emily", expr.Evaluation.Result);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void ObjectChanges()
    {
        var john = TestPerson.CreateJohn();
        var men = new ObservableCollection<TestPerson> { john };
        var emily = TestPerson.CreateEmily();
        var women = new ObservableCollection<TestPerson> { emily };
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe((p1, p2) => (p1.Count > 0 ? p1 : p2)[0], men, women))
        {
            Assert.AreSame(john, expr.Evaluation.Result);
            men.Clear();
            Assert.AreSame(emily, expr.Evaluation.Result);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public void ObjectFaultPropagation()
    {
        var numbers = new ObservableCollection<int>(Enumerable.Range(0, 10));
        var otherNumbers = new ObservableCollection<int>(Enumerable.Range(0, 10));
        var john = TestPerson.CreateJohn();
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe((p1, p2, p3) => (p3.Name!.Length == 0 ? p1 : p2)[0], numbers, otherNumbers, john))
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
    public void ObjectValueChanges()
    {
        var numbers = new TestObservableRangeCollection<int>(Enumerable.Range(0, 10));
        var observer = ExpressionObserverHelpers.Create();
        using (var expr = observer.Observe(p1 => p1[0], numbers))
        {
            Assert.AreEqual(expr.Evaluation.Result, 0);
            numbers.ChangeElementAndOnlyNotifyProperty(0, 100);
            Assert.AreEqual(expr.Evaluation.Result, 100);
        }
        Assert.AreEqual(0, observer.CachedObservableExpressions);
    }

    [TestMethod]
    public async Task ValueAsyncDisposalAsync()
    {
        var john = AsyncDisposableTestPerson.CreateJohn();
        var emily = AsyncDisposableTestPerson.CreateEmily();
        var people = new ObservableCollection<AsyncDisposableTestPerson> { john };
        var disposedTcs = new TaskCompletionSource<object?>();
        var options = new ExpressionObserverOptions();
        options.AddExpressionValueDisposal(() => new ObservableCollection<AsyncDisposableTestPerson>()[0]);
        var observer = ExpressionObserverHelpers.Create(options);
        using (var expr = observer.Observe(p => p[0], people))
        {
            Assert.AreSame(john, expr.Evaluation.Result);
            Assert.IsFalse(john.IsDisposed);
            john.Disposed += (sender, e) => disposedTcs.SetResult(null);
            people[0] = emily;
            await Task.WhenAny(disposedTcs.Task, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.AreSame(emily, expr.Evaluation.Result);
            Assert.IsTrue(john.IsDisposed);
            disposedTcs = new TaskCompletionSource<object?>();
            emily.Disposed += (sender, e) => disposedTcs.SetResult(null);
        }
        await Task.WhenAny(disposedTcs.Task, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.IsTrue(emily.IsDisposed);
    }

    [TestMethod]
    public void ValueDisposal()
    {
        var john = SyncDisposableTestPerson.CreateJohn();
        var emily = SyncDisposableTestPerson.CreateEmily();
        var people = new ObservableCollection<SyncDisposableTestPerson> { john };
        var options = new ExpressionObserverOptions();
        options.AddExpressionValueDisposal(() => new ObservableCollection<SyncDisposableTestPerson>()[0]);
        var observer = ExpressionObserverHelpers.Create(options);
        using (var expr = observer.Observe(p => p[0], people))
        {
            Assert.AreSame(john, expr.Evaluation.Result);
            Assert.IsFalse(john.IsDisposed);
            people[0] = emily;
            Assert.AreSame(emily, expr.Evaluation.Result);
            Assert.IsTrue(john.IsDisposed);
        }
        Assert.IsTrue(emily.IsDisposed);
    }
}
