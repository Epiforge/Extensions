namespace Epiforge.Extensions.Blazor.Tests;

public class TrackedCollection<T> :
    ObservableCollection<T>
{
    NotifyCollectionChangedEventHandler? collectionChanged;

    public override event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add => collectionChanged += value;
        remove => collectionChanged -= value;
    }

    public int CollectionChangedSubscribers =>
        collectionChanged?.GetInvocationList().Length ?? 0;

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e) =>
        collectionChanged?.Invoke(this, e);
}
