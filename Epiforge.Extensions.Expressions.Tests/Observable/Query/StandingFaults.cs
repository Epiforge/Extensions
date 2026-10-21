namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// A query whose fault stands for the same reason through a change of its source neither announces a new fault nor replaces the one it reported, since nothing its dependents could read has changed
/// </summary>
[TestClass]
public class StandingFaults
{
    static void AssertTheFaultStands(INotifyPropertyChanged query, string propertyName, Func<Exception?> readFault, Action change)
    {
        var standing = readFault();
        Assert.IsNotNull(standing, "the query was not faulted before the change");
        var announcements = 0;
        void propertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == propertyName)
                ++announcements;
        }
        query.PropertyChanged += propertyChanged;
        change();
        query.PropertyChanged -= propertyChanged;
        Assert.AreSame(standing, readFault(), "the query replaced a fault which stood for the same reason");
        Assert.AreEqual(0, announcements, $"the query announced {propertyName} although its fault stood for the same reason");
    }

    [TestMethod]
    [DataRow("element at")]
    [DataRow("element at a position computed from the count")]
    [DataRow("single")]
    public void AScalarOverACollectionKeepsAStandingFault(string scalar)
    {
        var source = new ObservableRangeCollection<int>([1, 2]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using var query = scalar switch
            {
                "element at" => sourceQuery.ObserveElementAt(5),
                "element at a position computed from the count" => sourceQuery.ObserveElementAt(count => count + 1),
                _ => sourceQuery.ObserveSingle()
            };
            AssertTheFaultStands(query, nameof(IObservableScalarQuery<int>.Evaluation), () => query.Evaluation.Fault, () => source.Add(3));
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ASingleOverADictionaryKeepsAStandingFault()
    {
        var source = new ObservableDictionary<int, int> { { 1, 1 }, { 2, 2 } };
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using var query = sourceQuery.ObserveSingle();
            AssertTheFaultStands(query, nameof(IObservableScalarQuery<int>.Evaluation), () => query.Evaluation.Fault, () => source.Add(3, 3));
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    public void ASelectOverACollectionKeepsAStandingFaultOfSeveralElements()
    {
        var source = new ObservableRangeCollection<int>([0, 0, 1]);
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyList(source))
        {
            using var query = sourceQuery.ObserveSelect(element => 10 / element);
            AssertTheFaultStands(query, nameof(IObservableCollectionQuery<int>.OperationFault), () => query.OperationFault, () => source.Add(2));
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }

    [TestMethod]
    [DataRow("duplicate keys")]
    [DataRow("a null key")]
    public void ASelectOverADictionaryKeepsAStandingFault(string reason)
    {
        var source = new ObservableDictionary<int, int> { { 1, 10 }, { 2, 20 }, { 3, 30 } };
        var collectionObserver = CollectionObserverHelpers.Create();
        using (var sourceQuery = collectionObserver.ObserveReadOnlyDictionary(source))
        {
            using var query = reason == "duplicate keys"
                ? sourceQuery.ObserveSelect((key, value) => key == 3 ? "three" : "other", (key, value) => value)
                : sourceQuery.ObserveSelect((key, value) => key == 1 ? null! : key.ToString(), (key, value) => value);
            AssertTheFaultStands(query, nameof(IObservableDictionaryQuery<string, int>.OperationFault), () => query.OperationFault, () => source[3] = 31);
        }
        Assert.AreEqual(0, collectionObserver.CachedObservableQueries);
        Assert.AreEqual(0, collectionObserver.ExpressionObserver.CachedObservableExpressions);
    }
}
