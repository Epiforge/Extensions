namespace Epiforge.Extensions.Expressions.Observable.Query;

sealed class ObservableCollectionSliceQuery<TElement>(CollectionObserver collectionObserver, ObservableCollectionQuery<TElement> source, Range range) :
    ObservableCollectionQuery<TElement>(collectionObserver)
{
    /// <summary>
    /// One notification this query delivers while following a change of its source, with where its elements begin in the source and how many there are once that notification has been delivered
    /// </summary>
    readonly record struct Step(NotifyCollectionChangedAction Action, int Index, int OldIndex, List<TElement>? OldItems, List<TElement>? NewItems, int Offset, int Count);

#if IS_NET_9_0_OR_GREATER
    readonly Lock access = new();
#else
    readonly object access = new();
#endif
    int count;
    /// <summary>
    /// Whether this query is following a change of its source, during which it delivers each notification of the change before taking the next step, so that a change its subscribers make to the source in the meantime waits until this one has been followed
    /// </summary>
    bool followingSourceChange;
    /// <summary>
    /// Where the elements this query holds begin in its source, as of the notifications it has delivered
    /// </summary>
    int offset;
    internal readonly Range Range = range;
    /// <summary>
    /// Whether the source announced a change while this query was following another, after which the steps left of the one under way no longer describe the source
    /// </summary>
    bool sourceChangedMeanwhile;
    int sourceCount;

    public override TElement this[int index]
    {
        get
        {
            int elementsOffset;
            lock (access)
            {
                if (index < 0 || index >= count)
                    throw ExceptionHelper.IndexArgumentWasOutOfRange;
                elementsOffset = offset;
            }
            return source[elementsOffset + index];
        }
    }

    private protected override int CurrentCount =>
        count;

    internal override bool ChangesThroughOthers =>
        true;

    internal override bool HasEnumerationPenalty =>
        source.HasEnumerationPenalty;

    internal override bool HasIndexerPenalty =>
        source.HasIndexerPenalty;

    internal override void CollectChangeLocks(List<ObservableQuery> queries)
    {
        source.CollectChangeLocks(queries);
        queries.Add(this);
    }

    protected override bool Dispose(bool disposing)
    {
        if (disposing)
        {
            var removedFromCache = source.QueryDisposed(this);
            if (removedFromCache)
            {
                source.CollectionChanged -= SourceCollectionChanged;
                source.PropertyChanged -= SourcePropertyChanged;
                RemovedFromCache();
            }
            return removedFromCache;
        }
        return true;
    }

    /// <summary>
    /// Follows a change of the source, delivering the notification of each step before taking the next, so that a subscriber finds this query as each notification leaves it; a change of the source made in the meantime ends the steps with a reset
    /// </summary>
    void FollowSourceChange(NotifyCollectionChangedEventArgs e)
    {
        if (!TryPlanSteps(e, out var steps))
        {
            Resynchronize();
            return;
        }
        if (steps is null)
            return;
        var currentSourceCount = source.Count;
        for (int i = 0, ii = steps.Count; i < ii; ++i)
        {
            var step = steps[i];
            lock (access)
            {
                sourceCount = currentSourceCount;
                offset = step.Offset;
                count = step.Count;
                OnCollectionChanged(step.Action switch
                {
                    NotifyCollectionChangedAction.Add => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, step.NewItems, step.Index),
                    NotifyCollectionChangedAction.Remove => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, step.OldItems, step.Index),
                    NotifyCollectionChangedAction.Replace => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, step.NewItems!, step.OldItems!, step.Index),
                    _ => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, step.OldItems, step.Index, step.OldIndex)
                });
            }
            if (i + 1 < ii)
            {
                RaiseNotificationsDeferredSoFar();
                if (sourceChangedMeanwhile)
                {
                    Resynchronize();
                    return;
                }
            }
        }
    }

    public override IEnumerator<TElement> GetEnumerator()
    {
        int elementsOffset, elementsCount;
        lock (access)
            (elementsOffset, elementsCount) = (offset, count);
        return source.Skip(elementsOffset).Take(elementsCount).GetEnumerator();
    }

    (int offset, int length) GetWindow(int ofCount)
    {
        var lowerOffset = Math.Clamp(Range.Start.GetOffset(ofCount), 0, ofCount);
        var upperOffset = Math.Clamp(Range.End.GetOffset(ofCount), 0, ofCount);
        return (lowerOffset, Math.Max(0, upperOffset - lowerOffset));
    }

    /// <summary>
    /// Merges a step into the one before it where the two are one notification: removals at one index, additions continuing one another, or a removal and an addition of as many elements at one index, which are a replacement
    /// </summary>
    static void Merge(ref List<Step>? steps, Step step)
    {
        if (steps is { Count: > 0 })
        {
            var previous = steps[^1];
            if (previous.Action is NotifyCollectionChangedAction.Remove && step.Action is NotifyCollectionChangedAction.Remove && previous.Index == step.Index)
            {
                previous.OldItems!.AddRange(step.OldItems!);
                steps[^1] = previous with { Offset = step.Offset, Count = step.Count };
                return;
            }
            if (previous.Action is NotifyCollectionChangedAction.Add && step.Action is NotifyCollectionChangedAction.Add && previous.Index + previous.NewItems!.Count == step.Index)
            {
                previous.NewItems.AddRange(step.NewItems!);
                steps[^1] = previous with { Offset = step.Offset, Count = step.Count };
                return;
            }
            if (previous.Action is NotifyCollectionChangedAction.Remove && step.Action is NotifyCollectionChangedAction.Add && previous.Index == step.Index && previous.OldItems!.Count == step.NewItems!.Count)
            {
                steps[^1] = new(NotifyCollectionChangedAction.Replace, step.Index, step.Index, previous.OldItems, step.NewItems, step.Offset, step.Count);
                return;
            }
        }
        (steps ??= []).Add(step);
    }

    protected override void OnInitialization()
    {
        using var changeHold = HoldChangesOf(source);
        lock (access)
        {
            sourceCount = source.Count;
            (offset, count) = GetWindow(sourceCount);
            InheritOperationFault(source.OperationFault);
            source.CollectionChanged += SourceCollectionChanged;
            source.PropertyChanged += SourcePropertyChanged;
        }
    }

    /// <summary>
    /// Plans the notifications describing what a change of the source did to the elements this query holds, each leaving them a run of the source as it now stands, yielding no steps where the change left them as they were and failing where only a reset describes the change
    /// </summary>
    [SuppressMessage("Maintainability", "CA1502: Avoid excessive complexity")]
    bool TryPlanSteps(NotifyCollectionChangedEventArgs e, out List<Step>? steps)
    {
        int lowerOffset, upperOffset, previousSourceCount;
        lock (access)
            (lowerOffset, upperOffset, previousSourceCount) = (offset, offset + count, sourceCount);
        steps = null;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Move when e.OldItems is { } movedItems && e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0:
                {
                    var moved = movedItems.Count;
                    var (from, to) = (e.OldStartingIndex, e.NewStartingIndex);
                    if (moved == 0 || from == to || Math.Max(from, to) + moved <= lowerOffset || Math.Min(from, to) >= upperOffset)
                        return true;
                    if (from < lowerOffset || from + moved > upperOffset || to < lowerOffset || to + moved > upperOffset)
                        return false;
                    steps = [new(NotifyCollectionChangedAction.Move, to - lowerOffset, from - lowerOffset, [.. movedItems.Cast<TElement>()], null, lowerOffset, upperOffset - lowerOffset)];
                    return true;
                }
            case NotifyCollectionChangedAction.Replace when e.OldItems is { } replacedItems && e.NewItems is { } replacementItems && replacedItems.Count == replacementItems.Count && e.OldStartingIndex >= 0:
                {
                    var start = Math.Max(lowerOffset, e.OldStartingIndex);
                    var end = Math.Min(upperOffset, e.OldStartingIndex + replacedItems.Count);
                    if (start < end)
                    {
                        var oldItems = new List<TElement>(end - start);
                        var newItems = new List<TElement>(end - start);
                        for (var position = start; position < end; ++position)
                        {
                            oldItems.Add((TElement)replacedItems[position - e.OldStartingIndex]!);
                            newItems.Add((TElement)replacementItems[position - e.OldStartingIndex]!);
                        }
                        steps = [new(NotifyCollectionChangedAction.Replace, start - lowerOffset, start - lowerOffset, oldItems, newItems, lowerOffset, upperOffset - lowerOffset)];
                    }
                    return true;
                }
            case NotifyCollectionChangedAction.Add when e.NewItems is not null && e.NewStartingIndex >= 0:
            case NotifyCollectionChangedAction.Remove when e.OldItems is not null && e.OldStartingIndex >= 0:
            case NotifyCollectionChangedAction.Replace when e.OldItems is not null && e.NewItems is not null && e.OldStartingIndex >= 0 && e.OldStartingIndex == e.NewStartingIndex:
                break;
            default:
                return false;
        }
        var index = e.Action is NotifyCollectionChangedAction.Add ? e.NewStartingIndex : e.OldStartingIndex;
        var removed = e.OldItems?.Count ?? 0;
        var inserted = e.NewItems?.Count ?? 0;
        var shift = inserted - removed;
        var (newLowerOffset, newLength) = GetWindow(previousSourceCount + shift);
        var newUpperOffset = newLowerOffset + newLength;
        var leftEnd = Math.Min(upperOffset, index);
        var hasLeft = lowerOffset < leftEnd;
        var rightStart = Math.Max(lowerOffset, index + removed);
        var hasRight = rightStart < upperOffset;
        var (shiftedRightStart, shiftedRightEnd) = (rightStart + shift, upperOffset + shift);
        if (inserted > 0 && removed > 0 && hasLeft && hasRight)
            return false;
        var deletionStart = Math.Max(lowerOffset, index);
        var deletionEnd = Math.Min(upperOffset, index + removed);
        int runStart, runEnd;
        if (hasLeft && hasRight)
            (runStart, runEnd) = (lowerOffset, shiftedRightEnd);
        else if (hasLeft)
            (runStart, runEnd) = (lowerOffset, leftEnd);
        else if (hasRight)
            (runStart, runEnd) = (shiftedRightStart, shiftedRightEnd);
        else
            (runStart, runEnd) = (newLowerOffset, newLowerOffset);
        if (deletionStart < deletionEnd)
        {
            var oldItems = new List<TElement>(deletionEnd - deletionStart);
            for (var position = deletionStart; position < deletionEnd; ++position)
                oldItems.Add((TElement)e.OldItems![position - index]!);
            Merge(ref steps, new(NotifyCollectionChangedAction.Remove, deletionStart - lowerOffset, deletionStart - lowerOffset, oldItems, null, runStart, runEnd - runStart));
        }
        else if (inserted > 0 && hasLeft && hasRight)
        {
            var keepsLeft = lowerOffset < newUpperOffset && newLowerOffset < leftEnd;
            var keepsRight = shiftedRightStart < newUpperOffset && newLowerOffset < shiftedRightEnd;
            var leftLength = leftEnd - lowerOffset;
            if (keepsLeft && keepsRight)
            {
                var newItems = new List<TElement>(inserted);
                for (var i = 0; i < inserted; ++i)
                    newItems.Add((TElement)e.NewItems![i]!);
                Merge(ref steps, new(NotifyCollectionChangedAction.Add, leftLength, leftLength, null, newItems, lowerOffset, runEnd - lowerOffset));
            }
            else if (keepsLeft)
            {
                Merge(ref steps, new(NotifyCollectionChangedAction.Remove, leftLength, leftLength, ReadSource(shiftedRightStart, shiftedRightEnd), null, lowerOffset, leftLength));
                runEnd = leftEnd;
            }
            else if (keepsRight)
            {
                Merge(ref steps, new(NotifyCollectionChangedAction.Remove, 0, 0, ReadSource(lowerOffset, leftEnd), null, shiftedRightStart, shiftedRightEnd - shiftedRightStart));
                runStart = shiftedRightStart;
            }
            else
            {
                var oldItems = ReadSource(lowerOffset, leftEnd);
                oldItems.AddRange(ReadSource(shiftedRightStart, shiftedRightEnd));
                Merge(ref steps, new(NotifyCollectionChangedAction.Remove, 0, 0, oldItems, null, newLowerOffset, 0));
                (runStart, runEnd) = (newLowerOffset, newLowerOffset);
            }
        }
        if (runStart == runEnd)
        {
            if (newLowerOffset < newUpperOffset)
                Merge(ref steps, new(NotifyCollectionChangedAction.Add, 0, 0, null, ReadSource(newLowerOffset, newUpperOffset), newLowerOffset, newLength));
        }
        else if (Math.Max(runStart, newLowerOffset) < Math.Min(runEnd, newUpperOffset))
        {
            if (runStart < newLowerOffset)
            {
                Merge(ref steps, new(NotifyCollectionChangedAction.Remove, 0, 0, ReadSource(runStart, newLowerOffset), null, newLowerOffset, runEnd - newLowerOffset));
                runStart = newLowerOffset;
            }
            if (newUpperOffset < runEnd)
            {
                Merge(ref steps, new(NotifyCollectionChangedAction.Remove, newUpperOffset - runStart, newUpperOffset - runStart, ReadSource(newUpperOffset, runEnd), null, runStart, newUpperOffset - runStart));
                runEnd = newUpperOffset;
            }
            if (newLowerOffset < runStart)
            {
                Merge(ref steps, new(NotifyCollectionChangedAction.Add, 0, 0, null, ReadSource(newLowerOffset, runStart), newLowerOffset, runEnd - newLowerOffset));
                runStart = newLowerOffset;
            }
            if (runEnd < newUpperOffset)
                Merge(ref steps, new(NotifyCollectionChangedAction.Add, runEnd - runStart, runEnd - runStart, null, ReadSource(runEnd, newUpperOffset), runStart, newUpperOffset - runStart));
        }
        else
        {
            Merge(ref steps, new(NotifyCollectionChangedAction.Remove, 0, 0, ReadSource(runStart, runEnd), null, newLowerOffset, 0));
            if (newLowerOffset < newUpperOffset)
                Merge(ref steps, new(NotifyCollectionChangedAction.Add, 0, 0, null, ReadSource(newLowerOffset, newUpperOffset), newLowerOffset, newLength));
        }
        if (steps is null)
            lock (access)
            {
                sourceCount = previousSourceCount + shift;
                (offset, count) = (newLowerOffset, newLength);
            }
        return true;
    }

    /// <summary>
    /// Reads the elements of the source as it now stands from one position up to another
    /// </summary>
    List<TElement> ReadSource(int start, int end)
    {
        var elements = new List<TElement>(end - start);
        for (var position = start; position < end; ++position)
            elements.Add(source[position]);
        return elements;
    }

    /// <summary>
    /// Takes up the source as it now stands and announces a reset
    /// </summary>
    void Resynchronize()
    {
        sourceChangedMeanwhile = false;
        lock (access)
        {
            sourceCount = source.Count;
            (offset, count) = GetWindow(sourceCount);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        using var notificationDeferral = DeferNotificationsUntilMutationCompletes();
        if (followingSourceChange)
        {
            sourceChangedMeanwhile = true;
            return;
        }
        followingSourceChange = true;
        try
        {
            FollowSourceChange(e);
        }
        finally
        {
            followingSourceChange = false;
        }
    }

    void SourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperationFault))
        {
            using var changeHold = HoldOwnChanges();
            lock (access)
                InheritOperationFault(source.OperationFault);
        }
    }
}
