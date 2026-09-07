namespace Epiforge.Extensions.Expressions.Observable;

enum SourceNotificationKind
{
    CollectionChanged,
    DictionaryChanged,
    PropertyChanged
}

class SourceNotificationAttachment
{
    internal SourceNotificationAttachment(Action<object?, EventArgs>? handler) =>
        Handler = handler;

    internal Action<object?, EventArgs>? Handler;
    internal volatile bool IsRemoved;
    internal volatile SourceNotificationAttachment? Next;
    internal SourceNotificationOwner? Owner;
    internal SourceNotificationAttachment? Previous;
}

/// <summary>
/// The first attachment to one event of one source object, which is also what is registered with that event, so that however many nodes are interested every one of them reacts within a single propagation and no consumer sees a value composed of inputs which were never simultaneously current
/// </summary>
/// <remarks>
/// The registration is made when this is constructed and released only when the last attachment goes, and is never exchanged for another: .NET captures an event's invocation list when it raises, so swapping a registration while a dispatch is in flight would let two handlers run in separate propagations, which is the defect this exists to prevent. That is why this remains the owner of the list after its own handler is detached, as a removed attachment the walk skips.
/// </remarks>
sealed class SourceNotificationOwner :
    SourceNotificationAttachment
{
    internal SourceNotificationOwner(object source, SourceNotificationKind kind, Action<object?, EventArgs> handler) :
        base(handler)
    {
        this.kind = kind;
        this.source = source;
        Owner = this;
        last = this;
        Live = 1;
        switch (kind)
        {
            case SourceNotificationKind.DictionaryChanged:
                ((INotifyDictionaryChanged)source).DictionaryChanged += SourceDictionaryChanged;
                break;
            case SourceNotificationKind.CollectionChanged:
                ((INotifyCollectionChanged)source).CollectionChanged += SourceCollectionChanged;
                break;
            default:
                ((INotifyPropertyChanged)source).PropertyChanged += SourcePropertyChanged;
                break;
        }
    }

    readonly SourceNotificationKind kind;
    SourceNotificationAttachment last;
    readonly object source;

    internal int Live;

    internal (object Source, SourceNotificationKind Kind) Key =>
        (source, kind);

    /// <remarks>
    /// The registry calls this only while holding its own lock, which is what makes the list safe to mutate without one of its own
    /// </remarks>
    internal void Attach(SourceNotificationAttachment attachment)
    {
        attachment.Owner = this;
        attachment.Previous = last;
        last.Next = attachment;
        last = attachment;
        ++Live;
    }

    /// <remarks>
    /// The registry calls this only while holding its own lock, which is what makes the list safe to mutate without one of its own. The owner is never unlinked, because the walk begins at it
    /// </remarks>
    internal void Detach(SourceNotificationAttachment attachment)
    {
        if (attachment.IsRemoved)
            return;
        attachment.IsRemoved = true;
        attachment.Handler = null;
        --Live;
        if (ReferenceEquals(attachment, this))
            return;
        if (attachment.Previous is { } previous)
            previous.Next = attachment.Next;
        if (attachment.Next is { } following)
            following.Previous = attachment.Previous;
        else
            last = attachment.Previous!;
        attachment.Previous = null;
    }

    void NotifyAttachments(object? sender, EventArgs e)
    {
        using var propagation = new PropagationScope();
        SourceNotificationAttachment? current = this;
        while (current is not null)
        {
            var following = current.Next;
            if (!current.IsRemoved && current.Handler is { } handler)
                handler(sender, e);
            current = following;
        }
    }

    internal void Release()
    {
        switch (kind)
        {
            case SourceNotificationKind.DictionaryChanged:
                ((INotifyDictionaryChanged)source).DictionaryChanged -= SourceDictionaryChanged;
                break;
            case SourceNotificationKind.CollectionChanged:
                ((INotifyCollectionChanged)source).CollectionChanged -= SourceCollectionChanged;
                break;
            default:
                ((INotifyPropertyChanged)source).PropertyChanged -= SourcePropertyChanged;
                break;
        }
    }

    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        NotifyAttachments(sender, e);

    void SourceDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e) =>
        NotifyAttachments(sender, e);

    void SourcePropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        NotifyAttachments(sender, e);
}

sealed class SourceNotificationRegistry
{
    sealed class KeyComparer :
        IEqualityComparer<(object Source, SourceNotificationKind Kind)>
    {
        internal static readonly KeyComparer Default = new();

        public bool Equals((object Source, SourceNotificationKind Kind) x, (object Source, SourceNotificationKind Kind) y) =>
            ReferenceEquals(x.Source, y.Source) && x.Kind == y.Kind;

        public int GetHashCode((object Source, SourceNotificationKind Kind) obj) =>
            HashCode.Combine(RuntimeHelpers.GetHashCode(obj.Source), obj.Kind);
    }

#if IS_NET_9_0_OR_GREATER
    readonly Lock ownersAccess = new();
#else
    readonly object ownersAccess = new();
#endif
    readonly Dictionary<(object Source, SourceNotificationKind Kind), SourceNotificationOwner> owners = new(KeyComparer.Default);

    internal SourceNotificationAttachment Attach(object source, SourceNotificationKind kind, Action<object?, EventArgs> handler)
    {
        lock (ownersAccess)
        {
            var key = (source, kind);
            if (!owners.TryGetValue(key, out var owner))
            {
                owner = new SourceNotificationOwner(source, kind, handler);
                owners.Add(key, owner);
                return owner;
            }
            var attachment = new SourceNotificationAttachment(handler);
            owner.Attach(attachment);
            return attachment;
        }
    }

    internal void Detach(SourceNotificationAttachment? attachment)
    {
        if (attachment?.Owner is not { } owner)
            return;
        lock (ownersAccess)
        {
            owner.Detach(attachment);
            if (owner.Live > 0)
                return;
            owners.Remove(owner.Key);
            owner.Release();
        }
    }
}
