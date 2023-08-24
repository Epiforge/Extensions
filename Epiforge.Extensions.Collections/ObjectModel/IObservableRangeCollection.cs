namespace Epiforge.Extensions.Collections.ObjectModel;

/// <summary>
/// Represents a dynamic data collection that supports bulk operations and provides notifications when items get added, removed, or when the whole list is refreshed
/// </summary>
/// <typeparam name="T">The type of elements in the collection</typeparam>
public interface IObservableRangeCollection<T> :
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
    /// Gets the number of elements contained in the <see cref="IObservableRangeCollection{T}"/>
    /// </summary>
    new int Count { get; }

    /// <summary>
    /// Adds objects to the end of the <see cref="IObservableRangeCollection{T}"/>
    /// </summary>
    /// <param name="items">The objects to be added to the end of the <see cref="IObservableRangeCollection{T}"/></param>
    void AddRange(IEnumerable<T> items);

    /// <summary>
    /// Adds objects to the end of the <see cref="IObservableRangeCollection{T}"/>
    /// </summary>
    /// <param name="items">The objects to be added to the end of the <see cref="IObservableRangeCollection{T}"/></param>
    void AddRange(IList<T> items);

    /// <summary>
    /// Removes all object from the <see cref="IObservableRangeCollection{T}"/> that satisfy the <paramref name="predicate"/>
    /// </summary>
    /// <param name="predicate">A predicate used to determine whether to remove an object from the <see cref="IObservableRangeCollection{T}"/></param>
    /// <returns>The items that were removed</returns>
    IReadOnlyList<T> GetAndRemoveAll(Func<T, bool> predicate);

    /// <summary>
    /// Gets the element at the specified index and removes it from the <see cref="IObservableRangeCollection{T}"/>
    /// </summary>
    /// <param name="index">The zero-based index of the element</param>
    /// <returns>The element at the specified index</returns>
    T GetAndRemoveAt(int index);

    /// <summary>
    /// Gets the elements in the range starting at the specified index and of the specified length
    /// </summary>
    /// <param name="index">The index of the element at the start of the range</param>
    /// <param name="count">The number of elements in the range</param>
    /// <returns>The elements in the range</returns>
    IReadOnlyList<T> GetRange(int index, int count);

    /// <summary>
    /// Inserts elements into the <see cref="IObservableRangeCollection{T}"/> at the specified index
    /// </summary>
    /// <param name="index">The zero-based index at which <paramref name="items"/> should be inserted</param>
    /// <param name="items">The objects to insert</param>
    void InsertRange(int index, IEnumerable<T> items);

    /// <summary>
    /// Inserts elements into the <see cref="IObservableRangeCollection{T}"/> at the specified index
    /// </summary>
    /// <param name="index">The zero-based index at which <paramref name="items"/> should be inserted</param>
    /// <param name="items">The objects to insert</param>
    void InsertRange(int index, IList<T> items);

    /// <summary>
    /// Moves the items at the specified index to a new location in the collection
    /// </summary>
    /// <param name="oldStartIndex">The zero-based index specifying the location of the items to be moved</param>
    /// <param name="newStartIndex">The zero-based index specifying the new location of the items</param>
    /// <param name="count">The number of items to move</param>
    void MoveRange(int oldStartIndex, int newStartIndex, int count);

    /// <summary>
    /// Removes all object from the <see cref="IObservableRangeCollection{T}"/> that satisfy the <paramref name="predicate"/>
    /// </summary>
    /// <param name="predicate">A predicate used to determine whether to remove an object from the <see cref="IObservableRangeCollection{T}"/></param>
    /// <returns>The number of items that were removed</returns>
    int RemoveAll(Func<T, bool> predicate);

    /// <summary>
    /// Removes the specified items from the <see cref="IObservableRangeCollection{T}"/>
    /// </summary>
    /// <param name="items">The items to be removed</param>
    /// <returns>The number of items that were removed</returns>
    void RemoveRange(IEnumerable<T> items);

    /// <summary>
    /// Removes the specified items from the <see cref="IObservableRangeCollection{T}"/>
    /// </summary>
    /// <param name="items">The items to be removed</param>
    /// <returns>The number of items that were removed</returns>
    void RemoveRange(IList<T> items);

    /// <summary>
    /// Removes the specified range of items from the <see cref="IObservableRangeCollection{T}"/>
    /// </summary>
    /// <param name="index">The index of the first item in the range</param>
    /// <param name="count">The number of items in the range</param>
    void RemoveRange(int index, int count);

    /// <summary>
    /// Replace all items in the <see cref="IObservableRangeCollection{T}"/> with the items in the specified collection
    /// </summary>
    /// <param name="items">The collection of replacement items</param>
    void ReplaceAll(IEnumerable<T> items);

    /// <summary>
    /// Replace all items in the <see cref="IObservableRangeCollection{T}"/> with the items in the specified collection
    /// </summary>
    /// <param name="items">The collection of replacement items</param>
    void ReplaceAll(IList<T> items);

    /// <summary>
    /// Replaces the specified range of items from the <see cref="IObservableRangeCollection{T}"/> with the items in the specified collection
    /// </summary>
    /// <param name="index">The index of the first item in the range</param>
    /// <param name="count">The number of items in the range</param>
    /// <param name="collection">The collection of replacement items</param>
    /// <returns>The items that were replaced</returns>
    IReadOnlyList<T> ReplaceRange(int index, int count, IEnumerable<T>? collection = null);

    /// <summary>
    /// Replaces the specified range of items from the <see cref="IObservableRangeCollection{T}"/> with the items in the specified list
    /// </summary>
    /// <param name="index">The index of the first item in the range</param>
    /// <param name="count">The number of items in the range</param>
    /// <param name="list">The list of replacement items</param>
    /// <returns>The items that were replaced</returns>
    IReadOnlyList<T> ReplaceRange(int index, int count, IList<T> list);

    /// <summary>
    /// Resets the <see cref="IObservableRangeCollection{T}"/> with the specified collection of items
    /// </summary>
    /// <param name="newCollection">The collection of items</param>
    void Reset(IEnumerable<T> newCollection);
}
