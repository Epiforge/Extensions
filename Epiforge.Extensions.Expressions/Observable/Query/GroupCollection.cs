namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Holds the elements of one group of a grouping or a lookup, and removes an element leaving the group by finding the very instance that left, since an equal element beside it is a different element which stayed
/// </summary>
/// <remarks>
/// For a reference type the instance is found by comparing references, which reads none of the elements it passes; a value type has no instance to find, so its first equal element is removed. An element occurring only once is sought from both ends at once, since an element joins a group at its end, so that one which moved lately sits near the end and one which has been there longest near the front, and either is found in as many steps as it is from the nearer end
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

    static bool IsInstance(T element, T item) =>
        typeof(T).IsValueType ? EqualityComparer<T>.Default.Equals(element, item) : ReferenceEquals(element, item);

    internal void RemoveInstance(T item, bool isOnlyOccurrence)
    {
        if (Items is List<T> list)
        {
            var elements = CollectionsMarshal.AsSpan(list);
            if (isOnlyOccurrence)
            {
                for (int front = 0, back = elements.Length - 1; front <= back; ++front, --back)
                {
                    if (IsInstance(elements[front], item))
                    {
                        RemoveAt(front);
                        return;
                    }
                    if (IsInstance(elements[back], item))
                    {
                        RemoveAt(back);
                        return;
                    }
                }
            }
            else if (!typeof(T).IsValueType)
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
