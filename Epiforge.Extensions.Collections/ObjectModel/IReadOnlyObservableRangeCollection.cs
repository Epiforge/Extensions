namespace Epiforge.Extensions.Collections.ObjectModel;

/// <summary>
/// Represents a read-only dynamic data collection that supports bulk reads and provides notifications when items get added, removed, or when the whole list is refreshed
/// </summary>
/// <typeparam name="T">The type of elements in the collection</typeparam>
public interface IReadOnlyObservableRangeCollection<T> :
    ICollection,
    ICollection<T>,
    IEnumerable,
    IEnumerable<T>,
    IList,
    IList<T>,
    INotifyCollectionChanged,
    INotifyPropertyChanged,
    IReadOnlyCollection<T>,
    IReadOnlyList<T>
{
    /// <summary>
    /// Gets the element at the specified index
    /// </summary>
    /// <param name="index">The index</param>
    new T this[int index] { get; }

    /// <summary>
    /// Gets the number of elements contained in the <see cref="IReadOnlyObservableRangeCollection{T}"/>
    /// </summary>
    new int Count { get; }

    /// <summary>
    /// Gets the elements in the range starting at the specified index and of the specified length
    /// </summary>
    /// <param name="index">The index of the element at the start of the range</param>
    /// <param name="count">The number of elements in the range</param>
    /// <returns>The elements in the range</returns>
    IReadOnlyList<T> GetRange(int index, int count);
}
