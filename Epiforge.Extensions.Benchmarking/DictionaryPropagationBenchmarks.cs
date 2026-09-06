namespace Epiforge.Extensions.Benchmarking;

using Epiforge.Extensions.Collections.Specialized;

[MemoryDiagnoser]
public class DictionaryPropagationBenchmarks
{
    const int elementCount = 1000;
    const int watchedKey = 0;

    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, bool>> pairPredicate = pair => (pair.Value.Rank & 1) == 0;
    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, KeyValuePair<int, int>>> pairProjection = pair => new KeyValuePair<int, int>(pair.Key, pair.Value.Rank);
    static readonly Expression<Func<int, BenchmarkPerson, int>> keySelector = (key, person) => key;
    static readonly Expression<Func<int, BenchmarkPerson, bool>> predicate = (key, person) => (person.Rank & 1) == 0;
    static readonly Expression<Func<int, BenchmarkPerson, int>> valueSelector = (key, person) => person.Rank;

    IObservableScalarQuery<bool> all = null!;
    bool alternate;
    ExpressionObserver expressionObserver = null!;
    IObservableExpression<BenchmarkPerson> indexed = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, bool>[] observations = null!;
    CollectionObserver observer = null!;
    BenchmarkPerson[] people = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, KeyValuePair<int, int>>[] projections = null!;
    BenchmarkPerson replacement = null!;
    IObservableDictionaryQuery<int, int> select = null!;
    IObservableDictionaryQuery<int, int> selectWithSubscriber = null!;
    ObservableDictionary<int, BenchmarkPerson> source = null!;
    IObservableDictionaryQuery<int, BenchmarkPerson> sourceQuery = null!;
    IObservableDictionaryQuery<int, BenchmarkPerson> where = null!;
    IObservableDictionaryQuery<int, BenchmarkPerson> whereWithSubscriber = null!;

    [Benchmark]
    public void ChangeEveryValueInAnAllQuery() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueObservedWithoutAQuery() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueProjectedWithoutAQuery() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueInASelectQuery() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueInASelectQueryWithASubscriber() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueInAWhereQuery() =>
        ChangeEveryValue();

    [Benchmark]
    public void ChangeEveryValueInAWhereQueryWithASubscriber() =>
        ChangeEveryValue();

    void ChangeEveryValue()
    {
        for (var i = 0; i < elementCount; ++i)
            people[i].Rank ^= 1;
    }

    [Benchmark(Baseline = true)]
    public void ChangeEveryValueWithNoObservation() =>
        ChangeEveryValue();

    [GlobalCleanup(Target = nameof(ReplaceOneKeyObservedByAnIndexer))]
    public void CleanupIndexed() =>
        indexed.Dispose();

    [GlobalCleanup(Target = nameof(ChangeEveryValueObservedWithoutAQuery))]
    public void CleanupObservations()
    {
        for (var i = 0; i < elementCount; ++i)
            observations[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueProjectedWithoutAQuery))]
    public void CleanupProjections()
    {
        for (var i = 0; i < elementCount; ++i)
            projections[i].Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueInAnAllQuery))]
    public void CleanupAllQuery()
    {
        all.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueInASelectQuery))]
    public void CleanupSelectQuery()
    {
        select.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueInASelectQueryWithASubscriber))]
    public void CleanupSelectQueryWithSubscriber()
    {
        ((INotifyDictionaryChanged<int, int>)selectWithSubscriber).DictionaryChanged -= IgnoreSelected;
        selectWithSubscriber.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueInAWhereQuery))]
    public void CleanupWhereQuery()
    {
        where.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalCleanup(Target = nameof(ChangeEveryValueInAWhereQueryWithASubscriber))]
    public void CleanupWhereQueryWithSubscriber()
    {
        ((INotifyDictionaryChanged<int, BenchmarkPerson>)whereWithSubscriber).DictionaryChanged -= Ignore;
        whereWithSubscriber.Dispose();
        sourceQuery.Dispose();
    }

    [GlobalCleanup(Target = nameof(ReplaceOneKeyObservedByABoxedSubscriber))]
    public void CleanupBoxedSubscriber() =>
        ((INotifyDictionaryChanged)source).DictionaryChanged -= IgnoreBoxed;

    [GlobalCleanup(Target = nameof(ReplaceOneKeyObservedByBoxedAndPropertyChangedSubscribers))]
    public void CleanupBoxedAndPropertyChangedSubscribers()
    {
        ((INotifyDictionaryChanged)source).DictionaryChanged -= IgnoreBoxed;
        source.PropertyChanged -= IgnoreProperty;
    }

    [GlobalCleanup(Target = nameof(ReplaceOneKeyObservedByAPropertyChangedSubscriber))]
    public void CleanupPropertyChangedSubscriber() =>
        source.PropertyChanged -= IgnoreProperty;

    [GlobalCleanup(Target = nameof(ReplaceOneKeyObservedByATypedSubscriber))]
    public void CleanupTypedSubscriber() =>
        source.DictionaryChanged -= Ignore;

    [Benchmark]
    public void ReplaceOneKeyObservedByAnIndexer() =>
        ReplaceOneKey();

    [Benchmark]
    public void ReplaceOneKeyObservedByABoxedSubscriber() =>
        ReplaceOneKey();

    [Benchmark]
    public void ReplaceOneKeyObservedByATypedSubscriber() =>
        ReplaceOneKey();

    [Benchmark]
    public void ReplaceOneKeyObservedByAPropertyChangedSubscriber() =>
        ReplaceOneKey();

    [Benchmark]
    public void ReplaceOneKeyObservedByBoxedAndPropertyChangedSubscribers() =>
        ReplaceOneKey();

    void ReplaceOneKey()
    {
        for (var i = 0; i < elementCount; ++i)
        {
            alternate = !alternate;
            source[watchedKey] = alternate ? replacement : people[watchedKey];
        }
    }

    [Benchmark]
    public void ReplaceOneKeyWithNoObservation() =>
        ReplaceOneKey();

    [GlobalSetup(Targets = [nameof(ChangeEveryValueWithNoObservation), nameof(ReplaceOneKeyWithNoObservation)])]
    public void SetupBare() =>
        SetupDictionary();

    void SetupDictionary()
    {
        people = new BenchmarkPerson[elementCount];
        source = new ObservableDictionary<int, BenchmarkPerson>();
        for (var i = 0; i < elementCount; ++i)
        {
            people[i] = new BenchmarkPerson($"P{i}", i);
            source.Add(i, people[i]);
        }
        replacement = new BenchmarkPerson("replacement", 0);
    }

    [GlobalSetup(Target = nameof(ReplaceOneKeyObservedByAnIndexer))]
    public void SetupIndexed()
    {
        SetupDictionary();
        expressionObserver = new ExpressionObserver();
        indexed = expressionObserver.ObserveWithoutOptimization(() => source[watchedKey]);
    }

    [GlobalSetup(Target = nameof(ReplaceOneKeyObservedByABoxedSubscriber))]
    public void SetupBoxedSubscriber()
    {
        SetupDictionary();
        ((INotifyDictionaryChanged)source).DictionaryChanged += IgnoreBoxed;
    }

    [GlobalSetup(Target = nameof(ReplaceOneKeyObservedByBoxedAndPropertyChangedSubscribers))]
    public void SetupBoxedAndPropertyChangedSubscribers()
    {
        SetupDictionary();
        ((INotifyDictionaryChanged)source).DictionaryChanged += IgnoreBoxed;
        source.PropertyChanged += IgnoreProperty;
    }

    [GlobalSetup(Target = nameof(ReplaceOneKeyObservedByAPropertyChangedSubscriber))]
    public void SetupPropertyChangedSubscriber()
    {
        SetupDictionary();
        source.PropertyChanged += IgnoreProperty;
    }

    [GlobalSetup(Target = nameof(ReplaceOneKeyObservedByATypedSubscriber))]
    public void SetupTypedSubscriber()
    {
        SetupDictionary();
        source.DictionaryChanged += Ignore;
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueObservedWithoutAQuery))]
    public void SetupObservations()
    {
        SetupDictionary();
        expressionObserver = new ExpressionObserver();
        observations = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, bool>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            observations[i] = expressionObserver.ObserveWithoutOptimization(pairPredicate, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueProjectedWithoutAQuery))]
    public void SetupProjections()
    {
        SetupDictionary();
        expressionObserver = new ExpressionObserver();
        projections = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, KeyValuePair<int, int>>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            projections[i] = expressionObserver.ObserveWithoutOptimization(pairProjection, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueInAnAllQuery))]
    public void SetupAllQuery()
    {
        SetupDictionary();
        observer = new CollectionObserver();
        sourceQuery = observer.ObserveReadOnlyDictionary(source);
        all = sourceQuery.ObserveAll(predicate);
    }

    static void Ignore(object? sender, NotifyDictionaryChangedEventArgs<int, BenchmarkPerson> e)
    {
    }

    static void IgnoreSelected(object? sender, NotifyDictionaryChangedEventArgs<int, int> e)
    {
    }

    static void IgnoreBoxed(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e)
    {
    }

    static void IgnoreProperty(object? sender, PropertyChangedEventArgs e)
    {
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueInAWhereQueryWithASubscriber))]
    public void SetupWhereQueryWithSubscriber()
    {
        SetupDictionary();
        observer = new CollectionObserver();
        sourceQuery = observer.ObserveReadOnlyDictionary(source);
        whereWithSubscriber = sourceQuery.ObserveWhere(predicate);
        ((INotifyDictionaryChanged<int, BenchmarkPerson>)whereWithSubscriber).DictionaryChanged += Ignore;
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueInASelectQuery))]
    public void SetupSelectQuery()
    {
        SetupDictionary();
        observer = new CollectionObserver();
        sourceQuery = observer.ObserveReadOnlyDictionary(source);
        select = sourceQuery.ObserveSelect(keySelector, valueSelector);
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueInASelectQueryWithASubscriber))]
    public void SetupSelectQueryWithSubscriber()
    {
        SetupDictionary();
        observer = new CollectionObserver();
        sourceQuery = observer.ObserveReadOnlyDictionary(source);
        selectWithSubscriber = sourceQuery.ObserveSelect(keySelector, valueSelector);
        ((INotifyDictionaryChanged<int, int>)selectWithSubscriber).DictionaryChanged += IgnoreSelected;
    }

    [GlobalSetup(Target = nameof(ChangeEveryValueInAWhereQuery))]
    public void SetupWhereQuery()
    {
        SetupDictionary();
        observer = new CollectionObserver();
        sourceQuery = observer.ObserveReadOnlyDictionary(source);
        where = sourceQuery.ObserveWhere(predicate);
    }
}
