namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Represents the result of an observable query
/// </summary>
/// <remarks>
/// Each call which produces one of these returns a distinct instance, even when the query itself is shared; disposing it releases only that observation
/// </remarks>
public interface IObservableQuery :
    IDisposable,
    IDisposalStatus,
    INotifyDisposed,
    INotifyDisposing,
    INotifyPropertyChanged,
    INotifyPropertyChanging
{
    /// <summary>
    /// Gets the number of cached observable queries
    /// </summary>
    int CachedObservableQueries { get; }

    /// <summary>
    /// Gets the collection observer used to observe this collection
    /// </summary>
    ICollectionObserver CollectionObserver { get; }
}
