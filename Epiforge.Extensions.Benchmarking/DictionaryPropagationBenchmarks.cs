namespace Epiforge.Extensions.Benchmarking;

using Epiforge.Extensions.Collections.Specialized;

[MemoryDiagnoser]
public class DictionaryPropagationBenchmarks
{
    const int elementCount = 1000;
    const int watchedKey = 0;

    static readonly Expression<Func<KeyValuePair<int, BenchmarkPerson>, bool>> pairPredicate = pair => (pair.Value.Rank & 1) == 0;
    static readonly Expression<Func<int, BenchmarkPerson, bool>> predicate = (key, person) => (person.Rank & 1) == 0;

    IObservableScalarQuery<bool> all = null!;
    bool alternate;
    ExpressionObserver expressionObserver = null!;
    IObservableExpression<BenchmarkPerson> indexed = null!;
    IObservableExpression<KeyValuePair<int, BenchmarkPerson>, bool>[] observations = null!;
    CollectionObserver observer = null!;
    BenchmarkPerson[] people = null!;
    BenchmarkPerson replacement = null!;
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

    [GlobalCleanup(Target = nameof(ChangeEveryValueInAnAllQuery))]
    public void CleanupAllQuery()
    {
        all.Dispose();
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

    [Benchmark]
    public void ReplaceOneKeyObservedByAnIndexer() =>
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

    [GlobalSetup(Target = nameof(ChangeEveryValueObservedWithoutAQuery))]
    public void SetupObservations()
    {
        SetupDictionary();
        expressionObserver = new ExpressionObserver();
        observations = new IObservableExpression<KeyValuePair<int, BenchmarkPerson>, bool>[elementCount];
        for (var i = 0; i < elementCount; ++i)
            observations[i] = expressionObserver.ObserveWithoutOptimization(pairPredicate, new KeyValuePair<int, BenchmarkPerson>(i, people[i]));
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

    [GlobalSetup(Target = nameof(ChangeEveryValueInAWhereQueryWithASubscriber))]
    public void SetupWhereQueryWithSubscriber()
    {
        SetupDictionary();
        observer = new CollectionObserver();
        sourceQuery = observer.ObserveReadOnlyDictionary(source);
        whereWithSubscriber = sourceQuery.ObserveWhere(predicate);
        ((INotifyDictionaryChanged<int, BenchmarkPerson>)whereWithSubscriber).DictionaryChanged += Ignore;
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
