namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Covers how the wrapper each observation returns forwards the notifications of the query it shares with other observations, whenever a handler is added to it, removed from it or added again, and after another wrapper over the same query is disposed
/// </summary>
/// <remarks>
/// A lookup's wrapper inherits its property notifications from the collection wrapper and adds its dictionary notifications, so the lookup is covered for the latter only; a lookup does not announce its count
/// </remarks>
[TestClass]
public class ScopedQueryForwarding
{
    sealed class Recorder
    {
        public int BoxedDictionaryChanged { get; private set; }
        public int CollectionChanged { get; private set; }
        public int DictionaryChanged { get; private set; }
        public object? LastSender { get; private set; }
        public int PropertyChanged { get; private set; }
        public int PropertyChanging { get; private set; }

        public void Attach(IObservableQuery query)
        {
            query.PropertyChanged += OnPropertyChanged;
            query.PropertyChanging += OnPropertyChanging;
        }

        public void AttachDictionary(IObservableLookupQuery<int, int> lookup)
        {
            ((INotifyDictionaryChanged<int, IObservableGrouping<int, int>>)lookup).DictionaryChanged += OnDictionaryChanged;
            ((INotifyDictionaryChanged)lookup).DictionaryChanged += OnBoxedDictionaryChanged;
        }

        public void AttachCollection(INotifyCollectionChanged query) =>
            query.CollectionChanged += OnCollectionChanged;

        public void AttachDictionary<TKey, TValue>(INotifyDictionaryChanged<TKey, TValue> dictionary)
        {
            dictionary.DictionaryChanged += OnDictionaryChanged;
            ((INotifyDictionaryChanged)dictionary).DictionaryChanged += OnBoxedDictionaryChanged;
        }

        public void Detach(IObservableQuery query)
        {
            query.PropertyChanged -= OnPropertyChanged;
            query.PropertyChanging -= OnPropertyChanging;
        }

        public void DetachDictionary(IObservableLookupQuery<int, int> lookup)
        {
            ((INotifyDictionaryChanged<int, IObservableGrouping<int, int>>)lookup).DictionaryChanged -= OnDictionaryChanged;
            ((INotifyDictionaryChanged)lookup).DictionaryChanged -= OnBoxedDictionaryChanged;
        }

        void OnBoxedDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e)
        {
            ++BoxedDictionaryChanged;
            LastSender = sender;
        }

        void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ++CollectionChanged;
            LastSender = sender;
        }

        void OnDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<int, IObservableGrouping<int, int>> e)
        {
            ++DictionaryChanged;
            LastSender = sender;
        }

        void OnDictionaryChanged<TKey, TValue>(object? sender, NotifyDictionaryChangedEventArgs<TKey, TValue> e)
        {
            ++DictionaryChanged;
            LastSender = sender;
        }

        void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            ++PropertyChanged;
            LastSender = sender;
        }

        void OnPropertyChanging(object? sender, PropertyChangingEventArgs e)
        {
            ++PropertyChanging;
            LastSender = sender;
        }
    }

    /// <summary>
    /// A source query, a way to observe one query over it again and again, and a change to the source which that query announces
    /// </summary>
    sealed class Subject(IObservableQuery source, Func<IObservableQuery> observe, Action mutate)
    {
        public Action Mutate { get; } = mutate;
        public Func<IObservableQuery> Observe { get; } = observe;
        public IObservableQuery Source { get; } = source;
    }

    static readonly Expression<Func<int, int>> identity = n => n;
    static readonly Expression<Func<int, bool>> isPositive = n => n > 0;
    static readonly Expression<Func<string, int, bool>> valueIsPositive = (key, value) => value > 0;

    static void AssertDisposingOneLeavesAnotherForwarding(Subject subject)
    {
        using (subject.Source)
        {
            var first = subject.Observe();
            var cachedAfterFirst = subject.Source.CachedObservableQueries;
            using var second = subject.Observe();
            Assert.AreEqual(cachedAfterFirst, subject.Source.CachedObservableQueries, "the second observation did not share the first's query, so this test cannot say anything");
            var firstRecorder = new Recorder();
            var secondRecorder = new Recorder();
            firstRecorder.Attach(first);
            secondRecorder.Attach(second);
            first.Dispose();
            subject.Mutate();
            Assert.AreEqual(0, firstRecorder.PropertyChanged, "a disposed wrapper forwarded a notification");
            Assert.IsTrue(secondRecorder.PropertyChanged > 0, "disposing one wrapper silenced another over the same query");
            Assert.IsTrue(secondRecorder.PropertyChanging > 0, "disposing one wrapper silenced another over the same query");
            Assert.AreSame(second, secondRecorder.LastSender);
        }
    }

    static void AssertEverythingZero(Recorder recorder)
    {
        Assert.AreEqual(0, recorder.PropertyChanged, "a disposed wrapper forwarded a property change to a handler added after its disposal");
        Assert.AreEqual(0, recorder.PropertyChanging, "a disposed wrapper forwarded a property change to a handler added after its disposal");
        Assert.AreEqual(0, recorder.CollectionChanged, "a disposed wrapper forwarded a collection change to a handler added after its disposal");
        Assert.AreEqual(0, recorder.DictionaryChanged, "a disposed wrapper forwarded a dictionary change to a handler added after its disposal");
        Assert.AreEqual(0, recorder.BoxedDictionaryChanged, "a disposed wrapper forwarded a dictionary change to a handler added after its disposal");
    }

    static void AssertForwardsToAHandlerAddedAfterCreation(Subject subject)
    {
        using (subject.Source)
        using (var query = subject.Observe())
        {
            var recorder = new Recorder();
            recorder.Attach(query);
            subject.Mutate();
            Assert.IsTrue(recorder.PropertyChanged > 0, "a handler added after the wrapper was made was not told of a change");
            Assert.IsTrue(recorder.PropertyChanging > 0, "a handler added after the wrapper was made was not told of a change");
            Assert.AreSame(query, recorder.LastSender, "the notification did not come from the wrapper");
        }
    }

    static void AssertStopsForARemovedHandlerAndResumesForAReaddedOne(Subject subject)
    {
        using (subject.Source)
        using (var query = subject.Observe())
        {
            var recorder = new Recorder();
            recorder.Attach(query);
            recorder.Detach(query);
            subject.Mutate();
            Assert.AreEqual(0, recorder.PropertyChanged, "a removed handler was told of a change");
            Assert.AreEqual(0, recorder.PropertyChanging, "a removed handler was told of a change");
            recorder.Attach(query);
            subject.Mutate();
            Assert.IsTrue(recorder.PropertyChanged > 0, "a handler added again was not told of a change");
            Assert.IsTrue(recorder.PropertyChanging > 0, "a handler added again was not told of a change");
        }
    }

    static Subject CollectionCount()
    {
        var (source, sourceQuery) = Numbers();
        var next = 3;
        return new(sourceQuery, () => sourceQuery.ObserveCount(), () => source.Add(next++));
    }

    [TestMethod]
    public void CollectionCountDisposingOneLeavesAnotherForwarding() =>
        AssertDisposingOneLeavesAnotherForwarding(CollectionCount());

    [TestMethod]
    public void CollectionCountForwardsToAHandlerAddedAfterCreation() =>
        AssertForwardsToAHandlerAddedAfterCreation(CollectionCount());

    [TestMethod]
    public void CollectionCountStopsForARemovedHandlerAndResumesForAReaddedOne() =>
        AssertStopsForARemovedHandlerAndResumesForAReaddedOne(CollectionCount());

    [TestMethod]
    public void CollectionCountTellsNoHandlerAddedAfterDisposal()
    {
        var (source, sourceQuery) = Numbers();
        using (sourceQuery)
        using (var kept = sourceQuery.ObserveCount())
        {
            var disposed = sourceQuery.ObserveCount();
            disposed.Dispose();
            var recorder = new Recorder();
            recorder.Attach(disposed);
            source.Add(3);
            Assert.AreEqual(3, kept.Evaluation.Result);
            AssertEverythingZero(recorder);
        }
    }

    static Subject CollectionWhere()
    {
        var (source, sourceQuery) = Numbers();
        var next = 3;
        return new(sourceQuery, () => sourceQuery.ObserveWhere(isPositive), () => source.Add(next++));
    }

    [TestMethod]
    public void CollectionWhereDisposingOneLeavesAnotherForwarding() =>
        AssertDisposingOneLeavesAnotherForwarding(CollectionWhere());

    [TestMethod]
    public void CollectionWhereForwardsToAHandlerAddedAfterCreation() =>
        AssertForwardsToAHandlerAddedAfterCreation(CollectionWhere());

    [TestMethod]
    public void CollectionWhereStopsForARemovedHandlerAndResumesForAReaddedOne() =>
        AssertStopsForARemovedHandlerAndResumesForAReaddedOne(CollectionWhere());

    [TestMethod]
    public void CollectionWhereTellsNoHandlerAddedAfterDisposal()
    {
        var (source, sourceQuery) = Numbers();
        using (sourceQuery)
        using (var kept = sourceQuery.ObserveWhere(isPositive))
        {
            var disposed = sourceQuery.ObserveWhere(isPositive);
            disposed.Dispose();
            var recorder = new Recorder();
            recorder.Attach(disposed);
            recorder.AttachCollection(disposed);
            source.Add(3);
            Assert.AreEqual(3, kept.Count);
            AssertEverythingZero(recorder);
        }
    }

    static Subject DictionaryWhere()
    {
        var source = new ObservableDictionary<string, int>(new Dictionary<string, int> { ["a"] = 1 });
        var sourceQuery = CollectionObserverHelpers.Create().ObserveReadOnlyDictionary(source);
        var next = 2;
        return new(sourceQuery, () => sourceQuery.ObserveWhere(valueIsPositive), () =>
        {
            source.Add($"k{next}", next);
            ++next;
        });
    }

    [TestMethod]
    public void DictionaryWhereDisposingOneLeavesAnotherForwarding() =>
        AssertDisposingOneLeavesAnotherForwarding(DictionaryWhere());

    [TestMethod]
    public void DictionaryWhereForwardsToAHandlerAddedAfterCreation() =>
        AssertForwardsToAHandlerAddedAfterCreation(DictionaryWhere());

    [TestMethod]
    public void DictionaryWhereStopsForARemovedHandlerAndResumesForAReaddedOne() =>
        AssertStopsForARemovedHandlerAndResumesForAReaddedOne(DictionaryWhere());

    [TestMethod]
    public void DictionaryWhereTellsNoHandlerAddedAfterDisposal()
    {
        var source = new ObservableDictionary<string, int>(new Dictionary<string, int> { ["a"] = 1 });
        using var sourceQuery = CollectionObserverHelpers.Create().ObserveReadOnlyDictionary(source);
        using var kept = sourceQuery.ObserveWhere(valueIsPositive);
        var disposed = sourceQuery.ObserveWhere(valueIsPositive);
        disposed.Dispose();
        var recorder = new Recorder();
        recorder.Attach(disposed);
        recorder.AttachCollection(disposed);
        recorder.AttachDictionary(disposed);
        source.Add("b", 2);
        Assert.AreEqual(2, kept.Count);
        AssertEverythingZero(recorder);
    }

    [TestMethod]
    public void LookupDisposingOneLeavesAnotherForwardingDictionaryChanges()
    {
        var (source, sourceQuery) = Numbers();
        using (sourceQuery)
        {
            var first = sourceQuery.ObserveToLookup(identity);
            using var second = sourceQuery.ObserveToLookup(identity);
            var firstRecorder = new Recorder();
            var secondRecorder = new Recorder();
            firstRecorder.AttachDictionary(first);
            secondRecorder.AttachDictionary(second);
            first.Dispose();
            source.Add(3);
            Assert.AreEqual(0, firstRecorder.DictionaryChanged, "a disposed wrapper forwarded a change");
            Assert.AreEqual(0, firstRecorder.BoxedDictionaryChanged, "a disposed wrapper forwarded a change");
            Assert.IsTrue(secondRecorder.DictionaryChanged > 0, "disposing one wrapper silenced another over the same lookup");
            Assert.IsTrue(secondRecorder.BoxedDictionaryChanged > 0, "disposing one wrapper silenced another over the same lookup");
        }
    }

    [TestMethod]
    public void LookupForwardsDictionaryChangesToHandlersAddedAfterCreation()
    {
        var (source, sourceQuery) = Numbers();
        using (sourceQuery)
        using (var lookup = sourceQuery.ObserveToLookup(identity))
        {
            var recorder = new Recorder();
            recorder.AttachDictionary(lookup);
            source.Add(3);
            Assert.IsTrue(recorder.DictionaryChanged > 0, "a handler added after the wrapper was made was not told of a change");
            Assert.IsTrue(recorder.BoxedDictionaryChanged > 0, "a handler added after the wrapper was made was not told of a change");
            Assert.AreSame(lookup, recorder.LastSender, "the change did not come from the wrapper");
        }
    }

    [TestMethod]
    public void LookupTellsNoHandlerAddedAfterDisposal()
    {
        var (source, sourceQuery) = Numbers();
        using (sourceQuery)
        using (var kept = sourceQuery.ObserveToLookup(identity))
        {
            var disposed = sourceQuery.ObserveToLookup(identity);
            disposed.Dispose();
            var recorder = new Recorder();
            recorder.Attach(disposed);
            recorder.AttachCollection(disposed);
            recorder.AttachDictionary(disposed);
            source.Add(3);
            Assert.IsTrue(kept.ContainsKey(3));
            AssertEverythingZero(recorder);
        }
    }

    [TestMethod]
    public void LookupStopsForwardingDictionaryChangesToARemovedHandlerAndResumesForAReaddedOne()
    {
        var (source, sourceQuery) = Numbers();
        using (sourceQuery)
        using (var lookup = sourceQuery.ObserveToLookup(identity))
        {
            var recorder = new Recorder();
            recorder.AttachDictionary(lookup);
            recorder.DetachDictionary(lookup);
            source.Add(3);
            Assert.AreEqual(0, recorder.DictionaryChanged, "a removed handler was told of a change");
            Assert.AreEqual(0, recorder.BoxedDictionaryChanged, "a removed handler was told of a change");
            recorder.AttachDictionary(lookup);
            source.Add(4);
            Assert.IsTrue(recorder.DictionaryChanged > 0, "a handler added again was not told of a change");
            Assert.IsTrue(recorder.BoxedDictionaryChanged > 0, "a handler added again was not told of a change");
        }
    }

    static (ObservableRangeCollection<int> source, IObservableCollectionQuery<int> sourceQuery) Numbers()
    {
        var source = new ObservableRangeCollection<int>([1, 2]);
        return (source, CollectionObserverHelpers.Create().ObserveReadOnlyList(source));
    }
}
