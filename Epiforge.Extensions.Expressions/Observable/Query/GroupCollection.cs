namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Holds the elements of one group of a grouping or a lookup, and removes an element leaving the group by finding the very instance that left, since an equal element beside it is a different element which stayed
/// </summary>
/// <remarks>
/// For a reference type the instance is found by comparing references, which reads none of the elements it passes; a value type has no instance to find, so its first equal element is removed
/// </remarks>
sealed class GroupCollection<T> :
    ObservableRangeCollection<T>
{
    public GroupCollection()
    {
    }

    public GroupCollection(ILogger logger) :
        base(logger)
    {
    }

    internal void RemoveInstance(T item)
    {
        if (!typeof(T).IsValueType && Items is List<T> list)
        {
            var elements = CollectionsMarshal.AsSpan(list);
            for (var i = 0; i < elements.Length; ++i)
                if (ReferenceEquals(elements[i], item))
                {
                    RemoveAt(i);
                    return;
                }
        }
        Remove(item);
    }
}
