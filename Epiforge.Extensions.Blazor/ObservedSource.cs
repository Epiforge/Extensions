namespace Epiforge.Extensions.Blazor;

/// <summary>
/// An object a reactivity manager subscribes to, with the property names it observes there and whether it observes its contents
/// </summary>
sealed class ObservedSource(ReactivityManager manager, object target)
{
    NotifyCollectionChangedEventHandler? collectionChangedHandler;
    public int CollectionStamp;
    public bool IsLive = true;
    readonly ReactivityManager manager = manager;
    PropertyChangedEventHandler? propertyChangedHandler;
    NameSlot[] slots = [];
    public readonly object Target = target;

    public bool IsCollectionSubscribed =>
        collectionChangedHandler is not null;

    public bool IsSubscribed =>
        collectionChangedHandler is not null || propertyChangedHandler is not null;

    public NameSlot AddSlot(string name)
    {
        var slot = new NameSlot(this, name);
        var current = slots;
        var next = new NameSlot[current.Length + 1];
        Array.Copy(current, next, current.Length);
        next[current.Length] = slot;
        Volatile.Write(ref slots, next);
        if (propertyChangedHandler is null)
        {
            propertyChangedHandler = PropertyChanged;
            ((INotifyPropertyChanged)Target).PropertyChanged += propertyChangedHandler;
        }
        return slot;
    }

    void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        manager.ObservedChanged();

    public void Dispose()
    {
        IsLive = false;
        UnsubscribeCollection();
        if (propertyChangedHandler is not null)
        {
            ((INotifyPropertyChanged)Target).PropertyChanged -= propertyChangedHandler;
            propertyChangedHandler = null;
        }
        foreach (var slot in slots)
            slot.IsLive = false;
    }

    public NameSlot? FindSlot(string name)
    {
        var current = slots;
        for (int i = 0, ii = current.Length; i < ii; ++i)
        {
            var slot = current[i];
            if (string.Equals(slot.Name, name, StringComparison.Ordinal))
                return slot;
        }
        return null;
    }

    void PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var name = e.PropertyName;
        if (string.IsNullOrEmpty(name))
        {
            manager.ObservedChanged();
            return;
        }
        var current = Volatile.Read(ref slots);
        for (int i = 0, ii = current.Length; i < ii; ++i)
            if (string.Equals(current[i].Name, name, StringComparison.Ordinal))
            {
                manager.ObservedChanged();
                return;
            }
    }

    public void SubscribeCollection()
    {
        collectionChangedHandler = CollectionChanged;
        ((INotifyCollectionChanged)Target).CollectionChanged += collectionChangedHandler;
    }

    /// <summary>
    /// Stops observing the property names the render cycle with the specified stamp did not use, and returns how many were dropped
    /// </summary>
    public int SweepSlots(int stamp)
    {
        var current = slots;
        var kept = 0;
        for (int i = 0, ii = current.Length; i < ii; ++i)
            if (current[i].Stamp == stamp)
                ++kept;
        if (kept == current.Length)
            return 0;
        var next = kept == 0 ? [] : new NameSlot[kept];
        var k = 0;
        for (int i = 0, ii = current.Length; i < ii; ++i)
        {
            var slot = current[i];
            if (slot.Stamp == stamp)
                next[k++] = slot;
            else
                slot.IsLive = false;
        }
        Volatile.Write(ref slots, next);
        if (kept == 0 && propertyChangedHandler is not null)
        {
            ((INotifyPropertyChanged)Target).PropertyChanged -= propertyChangedHandler;
            propertyChangedHandler = null;
        }
        return current.Length - kept;
    }

    public void UnsubscribeCollection()
    {
        if (collectionChangedHandler is not null)
        {
            ((INotifyCollectionChanged)Target).CollectionChanged -= collectionChangedHandler;
            collectionChangedHandler = null;
        }
    }
}
