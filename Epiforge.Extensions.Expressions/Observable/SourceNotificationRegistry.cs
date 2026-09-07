namespace Epiforge.Extensions.Expressions.Observable;

enum SourceNotificationKind
{
    CollectionChanged,
    DictionaryChanged,
    PropertyChanged
}

sealed class SourceNotificationAttachment
{
    internal SourceNotificationAttachment(Action<object?, EventArgs> handler) =>
        Handler = handler;

    internal readonly Action<object?, EventArgs> Handler;
    internal volatile bool IsRemoved;
    internal volatile SourceNotificationAttachment? Next;
    internal SourceNotificationAttachment? Previous;
    internal SourceNotificationSource? Source;
}

/// <summary>
/// One event of one source object, to which the graph attaches a single handler however many nodes are interested, so that every node reacts within one propagation and no consumer sees a value composed of inputs which were never simultaneously current
/// </summary>
sealed class SourceNotificationSource
{
    internal SourceNotificationSource(object source, SourceNotificationKind kind)
    {
        this.kind = kind;
        this.source = source;
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

#if IS_NET_9_0_OR_GREATER
    readonly Lock attachmentsAccess = new();
#else
    readonly object attachmentsAccess = new();
#endif
    SourceNotificationAttachment? firstAttachment;
    readonly SourceNotificationKind kind;
    SourceNotificationAttachment? lastAttachment;
    readonly object source;

    internal int Attachments;

    internal (object Source, SourceNotificationKind Kind) Key =>
        (source, kind);

    internal void Attach(SourceNotificationAttachment attachment)
    {
        lock (attachmentsAccess)
        {
            attachment.Source = this;
            attachment.Previous = lastAttachment;
            if (lastAttachment is null)
                Volatile.Write(ref firstAttachment, attachment);
            else
                lastAttachment.Next = attachment;
            lastAttachment = attachment;
            ++Attachments;
        }
    }

    internal void Detach(SourceNotificationAttachment attachment)
    {
        lock (attachmentsAccess)
        {
            if (attachment.IsRemoved)
                return;
            attachment.IsRemoved = true;
            if (attachment.Previous is null)
                Volatile.Write(ref firstAttachment, attachment.Next);
            else
                attachment.Previous.Next = attachment.Next;
            if (attachment.Next is null)
                lastAttachment = attachment.Previous;
            else
                attachment.Next.Previous = attachment.Previous;
            attachment.Previous = null;
            --Attachments;
        }
    }

    void NotifyAttachments(object? sender, EventArgs e)
    {
        using var propagation = new PropagationScope();
        var current = Volatile.Read(ref firstAttachment);
        while (current is not null)
        {
            var following = current.Next;
            if (!current.IsRemoved)
                current.Handler(sender, e);
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
    readonly Lock sourcesAccess = new();
#else
    readonly object sourcesAccess = new();
#endif
    readonly Dictionary<(object Source, SourceNotificationKind Kind), SourceNotificationSource> sources = new(KeyComparer.Default);

    internal SourceNotificationAttachment Attach(object source, SourceNotificationKind kind, Action<object?, EventArgs> handler)
    {
        var attachment = new SourceNotificationAttachment(handler);
        lock (sourcesAccess)
        {
            var key = (source, kind);
            if (!sources.TryGetValue(key, out var notificationSource))
            {
                notificationSource = new SourceNotificationSource(source, kind);
                sources.Add(key, notificationSource);
            }
            notificationSource.Attach(attachment);
        }
        return attachment;
    }

    internal void Detach(SourceNotificationAttachment? attachment)
    {
        if (attachment?.Source is not { } notificationSource)
            return;
        lock (sourcesAccess)
        {
            notificationSource.Detach(attachment);
            if (notificationSource.Attachments > 0)
                return;
            sources.Remove(notificationSource.Key);
            notificationSource.Release();
        }
    }
}
