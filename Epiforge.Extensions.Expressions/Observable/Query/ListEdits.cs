namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Applies announced changes to a query's private copy of a sequence in place, without the intermediate lists and iterators that casting and range methods would make
/// </summary>
static class ListEdits
{
    /// <summary>
    /// Inserts the elements an announcement carries at an index, casting each as it is appended and before any element of the list moves, so that an element of the wrong type leaves the list as it was
    /// </summary>
    public static void InsertAll<T>(List<T> list, int index, IList items)
    {
        if (items.Count == 1)
        {
            list.Insert(index, (T)items[0]!);
            return;
        }
        var appendedFrom = list.Count;
        list.EnsureCapacity(appendedFrom + items.Count);
        try
        {
            for (int i = 0, ii = items.Count; i < ii; ++i)
                list.Add((T)items[i]!);
        }
        catch
        {
            list.RemoveRange(appendedFrom, list.Count - appendedFrom);
            throw;
        }
        ExchangeAdjacentRuns(CollectionsMarshal.AsSpan(list), index, appendedFrom, list.Count);
    }

    /// <summary>
    /// Inserts into a query's own collection the elements an announcement of its source carries, inserting a lone element as itself so that neither a cast sequence nor a copy of it is made
    /// </summary>
    public static void InsertAnnounced<T>(ObservableRangeCollection<T> elements, int index, IList items)
    {
        if (items.Count == 1)
            elements.Insert(index, (T)items[0]!);
        else
            elements.InsertRange(index, items.Cast<T>());
    }

    /// <summary>
    /// Replaces in a query's own collection the elements an announcement of its source replaced, setting a lone element in place so that neither a cast sequence nor a copy of it is made
    /// </summary>
    public static void ReplaceAnnounced<T>(ObservableRangeCollection<T> elements, int index, IList replacedItems, IList replacementItems)
    {
        if (replacedItems.Count == 1 && replacementItems.Count == 1)
            elements[index] = (T)replacementItems[0]!;
        else
            elements.ReplaceRange(index, replacedItems.Count, replacementItems.Cast<T>());
    }

    /// <summary>
    /// Moves a run of elements as an announced move describes it, the new index being where the run starts once it has moved
    /// </summary>
    public static void MoveRange<T>(List<T> list, int oldIndex, int newIndex, int count)
    {
        var span = CollectionsMarshal.AsSpan(list);
        if (newIndex > oldIndex)
            ExchangeAdjacentRuns(span, oldIndex, oldIndex + count, newIndex + count);
        else if (newIndex < oldIndex)
            ExchangeAdjacentRuns(span, newIndex, oldIndex, oldIndex + count);
    }

    /// <summary>
    /// Exchanges the run from <paramref name="start"/> to <paramref name="middle"/> with the run from <paramref name="middle"/> to <paramref name="end"/>, keeping the order within each
    /// </summary>
    /// <remarks>
    /// The shorter run is set aside, in a local where it is one element and in a pooled array otherwise, and the longer is shifted by one block copy, which costs far less than reversing both runs element by element when a short run crosses a long list
    /// </remarks>
    static void ExchangeAdjacentRuns<T>(Span<T> span, int start, int middle, int end)
    {
        var firstLength = middle - start;
        var secondLength = end - middle;
        if (firstLength == 0 || secondLength == 0)
            return;
        if (firstLength == 1)
        {
            var element = span[start];
            span[middle..end].CopyTo(span[start..]);
            span[start + secondLength] = element;
            return;
        }
        if (secondLength == 1)
        {
            var element = span[middle];
            span[start..middle].CopyTo(span[(start + 1)..]);
            span[start] = element;
            return;
        }
        var setAsideLength = Math.Min(firstLength, secondLength);
        var setAside = System.Buffers.ArrayPool<T>.Shared.Rent(setAsideLength);
        if (firstLength <= secondLength)
        {
            span[start..middle].CopyTo(setAside);
            span[middle..end].CopyTo(span[start..]);
            setAside.AsSpan(0, firstLength).CopyTo(span[(start + secondLength)..]);
        }
        else
        {
            span[middle..end].CopyTo(setAside);
            span[start..middle].CopyTo(span[(start + secondLength)..]);
            setAside.AsSpan(0, secondLength).CopyTo(span[start..]);
        }
        System.Buffers.ArrayPool<T>.Shared.Return(setAside, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
    }
}
