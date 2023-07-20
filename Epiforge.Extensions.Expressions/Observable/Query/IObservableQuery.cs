namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Represents the result of an observable query
/// </summary>
public interface IObservableQuery :
    IDisposable,
    IDisposalStatus,
    INotifyDisposalOverridden,
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
