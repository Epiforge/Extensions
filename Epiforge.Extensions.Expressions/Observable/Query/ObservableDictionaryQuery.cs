namespace Epiforge.Extensions.Expressions.Observable.Query;

abstract class ObservableDictionaryQuery<TKey, TValue> :
    ObservableQuery,
    IObservableDictionaryQuery<TKey, TValue>
    where TKey : notnull
{
    static readonly PropertyChangedEventArgs operationFaultPropertyChangedEventArgs = new(nameof(OperationFault));
    static readonly PropertyChangingEventArgs operationFaultPropertyChangingEventArgs = new(nameof(OperationFault));

    protected ObservableDictionaryQuery(CollectionObserver collectionObserver) :
        base(collectionObserver)
    {
    }

    Exception? operationFault;

    public abstract TValue this[TKey key] { get; }

    public override int CachedObservableQueries
    {
        get
        {
            var count = 0;
            return count;
        }
    }

    public abstract int Count { get; }

    public abstract IEnumerable<TKey> Keys { get; }

    public Exception? OperationFault
    {
        get => operationFault;
        protected set => SetBackedProperty(ref operationFault, in value, operationFaultPropertyChangingEventArgs, operationFaultPropertyChangedEventArgs);
    }

    public abstract IEnumerable<TValue> Values { get; }

    public event EventHandler<NotifyDictionaryChangedEventArgs<TKey, TValue>>? DictionaryChanged;

    public abstract bool ContainsKey(TKey key);

    public abstract IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();

    protected void OnDictionaryChanged(NotifyDictionaryChangedEventArgs<TKey, TValue> e) =>
        DictionaryChanged?.Invoke(this, e);

    public abstract bool TryGetValue(TKey key, out TValue value);

    public IObservableScalarQuery<TResult> ObserveAggregate<TAccumulate, TResult>(Func<TAccumulate> seedFactory, Func<TAccumulate, TKey, TValue, TAccumulate> func, Func<TAccumulate, TResult> resultSelector) => throw new NotImplementedException();

    public IObservableScalarQuery<bool> ObserveAll(Expression<Func<TKey, TValue, bool>> predicate) => throw new NotImplementedException();

    public IObservableScalarQuery<bool> ObserveAny() => throw new NotImplementedException();

    public IObservableScalarQuery<bool> ObserveAny(Expression<Func<TKey, TValue, bool>> predicate) => throw new NotImplementedException();

    public IObservableScalarQuery<TValue> ObserveAverage() => throw new NotImplementedException();

    public IObservableScalarQuery<TResult> ObserveAverage<TResult>(Expression<Func<TKey, TValue, TResult>> selector) => throw new NotImplementedException();

    public IObservableDictionaryQuery<TKey, TValue> ObserveConcurrently() => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst() => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst(IComparer<TKey> keyComparer) => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst(Expression<Func<TKey, TValue, bool>> predicate) => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirst(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> keyComparer) => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault() => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault(IComparer<TKey> keyComparer) => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault(Expression<Func<TKey, TValue, bool>> predicate) => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveFirstOrDefault(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> keyComparer) => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast() => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast(IComparer<TKey> keyComparer) => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast(Expression<Func<TKey, TValue, bool>> predicate) => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLast(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> keyComparer) => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault() => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault(IComparer<TKey> keyComparer) => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault(Expression<Func<TKey, TValue, bool>> predicate) => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveLastOrDefault(Expression<Func<TKey, TValue, bool>> predicate, IComparer<TKey> keyComparer) => throw new NotImplementedException();

    public IObservableScalarQuery<TValue> ObserveMax() => throw new NotImplementedException();

    public IObservableScalarQuery<TResult> ObserveMax<TResult>(Expression<Func<TKey, TValue, TResult>> selector) => throw new NotImplementedException();

    public IObservableScalarQuery<TValue> ObserveMin() => throw new NotImplementedException();

    public IObservableScalarQuery<TResult> ObserveMin<TResult>(Expression<Func<TKey, TValue, TResult>> selector) => throw new NotImplementedException();

    public IObservableDictionaryQuery<TKey, TResult> ObserveOfType<TResult>() => throw new NotImplementedException();

    public IObservableDictionaryQuery<TResultKey, TResultValue> ObserveSelect<TResultKey, TResultValue>(Expression<Func<TKey, TValue, KeyValuePair<TResultKey, TResultValue>>> selector) => throw new NotImplementedException();

    public IObservableDictionaryQuery<TResultKey, TResultValue> ObserveSelect<TResultKey, TResultValue>(Expression<Func<TKey, TValue, KeyValuePair<TResultKey, TResultValue>>> selector, IEqualityComparer<TResultKey> equalityComparer) => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingle() => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingle(Expression<Func<TKey, TValue, bool>> predicate) => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingleOrDefault() => throw new NotImplementedException();

    public IObservableScalarQuery<KeyValuePair<TKey, TValue>> ObserveSingleOrDefault(Expression<Func<TKey, TValue, bool>> predicate) => throw new NotImplementedException();

    public IObservableScalarQuery<TValue> ObserveSum() => throw new NotImplementedException();

    public IObservableScalarQuery<TResult> ObserveSum<TResult>(Expression<Func<TKey, TValue, TResult>> selector) => throw new NotImplementedException();

    public IObservableCollectionQuery<TValue> ObserveToCollection() => throw new NotImplementedException();

    public IObservableCollectionQuery<TElement> ObserveToCollection<TElement>(Func<TKey, TValue, TElement> selector) => throw new NotImplementedException();

    public IObservableScalarQuery<TValue> ObserveValueFor(TKey key) => throw new NotImplementedException();

    public IObservableScalarQuery<TValue> ObserveValueForOrDefault(TKey key) => throw new NotImplementedException();

    public IObservableDictionaryQuery<TKey, TValue> ObserveWhere(Expression<Func<TKey, TValue, bool>> predicate) => throw new NotImplementedException();
}
