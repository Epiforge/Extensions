namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionPrependQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> sequence, TElement prepend) :
    ObservableCollectionQuery<TElement>(collectionObserver)
{
    int sequenceCount;

    internal readonly TElement Prepended = prepend;

    public override TElement this[int index] =>
        index == 0 ? Prepended : sequence[index - 1];

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
        sequence.Prepend(Prepended).GetEnumerator();

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
        OnCollectionChanged(e.Action switch
        {
            NotifyCollectionChangedAction.Add => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, e.NewItems, e.NewStartingIndex + 1),
            NotifyCollectionChangedAction.Move => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, e.NewItems, e.NewStartingIndex + 1, e.OldStartingIndex + 1),
            NotifyCollectionChangedAction.Remove => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, e.OldItems, e.OldStartingIndex + 1),
            NotifyCollectionChangedAction.Replace => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, e.NewItems!, e.OldItems!, e.NewStartingIndex + 1),
            NotifyCollectionChangedAction.Reset => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset),
            _ => throw new NotSupportedException(),
        });
    }

    void SequencePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObservableCollectionQuery<>.OperationFault))
            SetOperationFault();
    }

    public override string ToString() =>
        $"{sequence} prepended with {Prepended}";
}
