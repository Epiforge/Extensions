namespace Epiforge.Extensions.Expressions.Observable;

sealed class DirectSubscriptionAttachment
{
    internal DirectSubscriptionAttachment(DirectObservableExpression observation, bool forcesNotification)
    {
        Observation = observation;
        ForcesNotification = forcesNotification;
    }

    internal readonly bool ForcesNotification;
    internal volatile bool IsRemoved;
    internal volatile DirectSubscriptionAttachment? Next;
    internal readonly DirectObservableExpression Observation;
    internal DirectSubscriptionAttachment? Previous;
    internal DirectSubscriptionSource? Source;
}

sealed class DirectSubscriptionSource
{
    internal DirectSubscriptionSource(object source, DirectSubscriptionKind kind, string? propertyName)
    {
        this.kind = kind;
        this.propertyName = propertyName;
        this.source = source;
        switch (kind)
        {
            case DirectSubscriptionKind.DictionaryChanged:
                ((INotifyDictionaryChanged)source).DictionaryChanged += SourceDictionaryChanged;
                break;
            case DirectSubscriptionKind.CollectionChanged:
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
    DirectSubscriptionAttachment? firstAttachment;
    readonly DirectSubscriptionKind kind;
    DirectSubscriptionAttachment? lastAttachment;
    readonly string? propertyName;
    readonly object source;

    internal int Attachments;

    internal (object Source, DirectSubscriptionKind Kind, string? PropertyName) Key =>
        (source, kind, propertyName);

    internal void Attach(DirectSubscriptionAttachment attachment)
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

    internal void Detach(DirectSubscriptionAttachment attachment)
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

    internal void Release()
    {
        switch (kind)
        {
            case DirectSubscriptionKind.DictionaryChanged:
                ((INotifyDictionaryChanged)source).DictionaryChanged -= SourceDictionaryChanged;
                break;
            case DirectSubscriptionKind.CollectionChanged:
                ((INotifyCollectionChanged)source).CollectionChanged -= SourceCollectionChanged;
                break;
            default:
                ((INotifyPropertyChanged)source).PropertyChanged -= SourcePropertyChanged;
                break;
        }
    }

    void NotifyAttachments()
    {
        var current = Volatile.Read(ref firstAttachment);
        while (current is not null)
        {
            var following = current.Next;
            if (!current.IsRemoved)
                current.Observation.OnSourceChanged(current.ForcesNotification);
            current = following;
        }
    }

    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        using var propagation = new PropagationScope();
        NotifyAttachments();
    }

    void SourceDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e)
    {
        using var propagation = new PropagationScope();
        NotifyAttachments();
    }

    void SourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (kind is DirectSubscriptionKind.MemberPropertyChanged ? !(string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == propertyName) : e.PropertyName != propertyName)
            return;
        using var propagation = new PropagationScope();
        NotifyAttachments();
    }
}

sealed class DirectSubscriptionRegistry
{
    sealed class KeyComparer :
        IEqualityComparer<(object Source, DirectSubscriptionKind Kind, string? PropertyName)>
    {
        internal static readonly KeyComparer Default = new();

        public bool Equals((object Source, DirectSubscriptionKind Kind, string? PropertyName) x, (object Source, DirectSubscriptionKind Kind, string? PropertyName) y) =>
            ReferenceEquals(x.Source, y.Source) && x.Kind == y.Kind && x.PropertyName == y.PropertyName;

        public int GetHashCode((object Source, DirectSubscriptionKind Kind, string? PropertyName) obj) =>
            HashCode.Combine(RuntimeHelpers.GetHashCode(obj.Source), obj.Kind, obj.PropertyName);
    }

#if IS_NET_9_0_OR_GREATER
    readonly Lock sourcesAccess = new();
#else
    readonly object sourcesAccess = new();
#endif
    readonly Dictionary<(object Source, DirectSubscriptionKind Kind, string? PropertyName), DirectSubscriptionSource> sources = new(KeyComparer.Default);

    internal DirectSubscriptionAttachment Attach(object source, DirectSubscriptionKind kind, string? propertyName, DirectObservableExpression observation, bool forcesNotification)
    {
        var attachment = new DirectSubscriptionAttachment(observation, forcesNotification);
        lock (sourcesAccess)
        {
            var key = (source, kind, propertyName);
            if (!sources.TryGetValue(key, out var subscriptionSource))
            {
                subscriptionSource = new DirectSubscriptionSource(source, kind, propertyName);
                sources.Add(key, subscriptionSource);
            }
            subscriptionSource.Attach(attachment);
        }
        return attachment;
    }

    internal void Detach(DirectSubscriptionAttachment attachment)
    {
        if (attachment.Source is not { } subscriptionSource)
            return;
        lock (sourcesAccess)
        {
            subscriptionSource.Detach(attachment);
            if (subscriptionSource.Attachments > 0)
                return;
            sources.Remove(subscriptionSource.Key);
            subscriptionSource.Release();
        }
    }
}
