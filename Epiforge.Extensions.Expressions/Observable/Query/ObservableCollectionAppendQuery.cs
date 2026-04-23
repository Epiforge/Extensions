namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionAppendQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> sequence, TElement append) :
    ObservableCollectionQuery<TElement>(collectionObserver)
{
    int sequenceCount;

    internal readonly TElement Appended = append;

    public override TElement this[int index] =>
        index == sequenceCount ? Appended : sequence[index];

    public override int Count =>
        sequenceCount + 1;

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = sequence.QueryDisposed(this);
            if (removedFromCache)
            {
                sequence.CollectionChanged -= SequenceCollectionChanged;
                sequence.PropertyChanged -= SequencePropertyChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    public override IEnumerator<TElement> GetEnumerator() =>
        sequence.Append(Appended).GetEnumerator();

    protected override void OnInitialization()
    {
        sequenceCount = sequence.Count;
        sequence.CollectionChanged += SequenceCollectionChanged;
        sequence.PropertyChanged += SequencePropertyChanged;
    }

    void SetOperationFault()
    {
        var faultList = new FaultList();
        faultList.Check(sequence);
        OperationFault = faultList.Fault;
    }

    void SequenceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
            sequenceCount = sequence.Count;
        else
            sequenceCount += (e.NewItems?.Count ?? 0) - (e.OldItems?.Count ?? 0);
        OnCollectionChanged(e);
    }

    void SequencePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableCollectionQuery<>.OperationFault))
            SetOperationFault();
    }

    public override string ToString() =>
        $"{sequence} appended with {Appended}";
}
