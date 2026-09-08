namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Names the event a source is registered with, which is what a registration is shared by, because one object raising one event serves every subscription over it however many different properties those subscriptions name
/// </summary>
enum DirectSubscriptionRegistration
{
    CollectionChanged,
    DictionaryChanged,
    PropertyChanged
}

sealed class DirectSubscriptionAttachment
{
    internal DirectSubscriptionAttachment(DirectObservableExpression observation, DirectSubscriptionKind kind, string? propertyName, bool forcesNotification)
    {
        ForcesNotification = forcesNotification;
        Kind = kind;
        Observation = observation;
        PropertyName = propertyName;
    }

    internal readonly bool ForcesNotification;
    internal volatile bool IsRemoved;
    internal readonly DirectSubscriptionKind Kind;
    internal volatile DirectSubscriptionAttachment? Next;
    internal readonly DirectObservableExpression Observation;
    internal DirectSubscriptionAttachment? Previous;
    internal readonly string? PropertyName;
    internal DirectSubscriptionSource? Source;

    /// <summary>
    /// Determines whether a property change reported under the specified name is one this attachment named, which its source asks of each of its attachments because they no longer have a source apiece
    /// </summary>
    /// <param name="reportedName">The name the source reported, which is absent where the source means every property</param>
    internal bool Wants(string? reportedName) =>
        Kind switch
        {
            DirectSubscriptionKind.MemberPropertyChanged => string.IsNullOrEmpty(reportedName) || reportedName == PropertyName,
            DirectSubscriptionKind.IndexerPropertyChanged => reportedName == PropertyName || IsConventionalIndexerName(reportedName),
            _ => reportedName == PropertyName
        };

    /// <summary>
    /// Determines whether the reported name is the conventional indexed form of this attachment's property name, compared without composing that form, since composing it would allocate on every change of every other property of the source
    /// </summary>
    bool IsConventionalIndexerName(string? reportedName) =>
        PropertyName is { } propertyName && reportedName is { } reported && reported.Length == propertyName.Length + 2 && reported[^2] == '[' && reported[^1] == ']' && string.CompareOrdinal(reported, 0, propertyName, 0, propertyName.Length) == 0;
}

/// <summary>
/// One registration with one event of one source object, shared by every subscription over that object and event whatever property each of them names
/// </summary>
/// <remarks>
/// The expression graph has always registered once per object and event and decided relevance per node; the fast path registered once per property until 8 September, which put a handler on an object for every property an expression read of it. Sharing the registration means a change to one property walks the attachments of the others, and each of them is asked whether it wants the name before anything is evaluated.
/// </remarks>
sealed class DirectSubscriptionSource
{
    internal DirectSubscriptionSource(object source, DirectSubscriptionRegistration registration)
    {
        this.registration = registration;
        this.source = source;
        switch (registration)
        {
            case DirectSubscriptionRegistration.DictionaryChanged:
                ((INotifyDictionaryChanged)source).DictionaryChanged += SourceDictionaryChanged;
                break;
            case DirectSubscriptionRegistration.CollectionChanged:
                ((INotifyCollectionChanged)source).CollectionChanged += SourceCollectionChanged;
                break;
            default:
                ((INotifyPropertyChanged)source).PropertyChanged += SourcePropertyChanged;
                break;
        }
    }

    DirectSubscriptionAttachment? firstAttachment;
    DirectSubscriptionAttachment? lastAttachment;
    readonly DirectSubscriptionRegistration registration;
    readonly object source;

    internal int Attachments;

    internal (object Source, DirectSubscriptionRegistration Registration) Key =>
        (source, registration);

    /// <remarks>
    /// The registry calls this only while holding its own lock, which is what makes the list safe to mutate without one of its own
    /// </remarks>
    internal void Attach(DirectSubscriptionAttachment attachment)
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

    /// <remarks>
    /// The registry calls this only while holding its own lock, which is what makes the list safe to mutate without one of its own
    /// </remarks>
    internal void Detach(DirectSubscriptionAttachment attachment)
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

    internal void Release()
    {
        switch (registration)
        {
            case DirectSubscriptionRegistration.DictionaryChanged:
                ((INotifyDictionaryChanged)source).DictionaryChanged -= SourceDictionaryChanged;
                break;
            case DirectSubscriptionRegistration.CollectionChanged:
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

    /// <remarks>
    /// The list is walked to the first attachment which wants the name before a propagation is entered, so that a change to a property nobody named begins no propagation, which is what a handler registered per property did by returning
    /// </remarks>
    void SourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var reportedName = e.PropertyName;
        var current = Volatile.Read(ref firstAttachment);
        while (current is not null && (current.IsRemoved || !current.Wants(reportedName)))
            current = current.Next;
        if (current is null)
            return;
        using var propagation = new PropagationScope();
        while (current is not null)
        {
            var following = current.Next;
            if (!current.IsRemoved && current.Wants(reportedName))
                current.Observation.OnSourceChanged(current.ForcesNotification);
            current = following;
        }
    }
}

sealed class DirectSubscriptionRegistry
{
    sealed class KeyComparer :
        IEqualityComparer<(object Source, DirectSubscriptionRegistration Registration)>
    {
        internal static readonly KeyComparer Default = new();

        public bool Equals((object Source, DirectSubscriptionRegistration Registration) x, (object Source, DirectSubscriptionRegistration Registration) y) =>
            ReferenceEquals(x.Source, y.Source) && x.Registration == y.Registration;

        public int GetHashCode((object Source, DirectSubscriptionRegistration Registration) obj) =>
            HashCode.Combine(RuntimeHelpers.GetHashCode(obj.Source), obj.Registration);
    }

    /// <summary>
    /// Yields the event a subscription of the specified kind registers with, which is the same event for every kind reported through <see cref="INotifyPropertyChanged.PropertyChanged" /> however each of them reads the name
    /// </summary>
    static DirectSubscriptionRegistration RegistrationOf(DirectSubscriptionKind kind) =>
        kind switch
        {
            DirectSubscriptionKind.DictionaryChanged => DirectSubscriptionRegistration.DictionaryChanged,
            DirectSubscriptionKind.CollectionChanged => DirectSubscriptionRegistration.CollectionChanged,
            _ => DirectSubscriptionRegistration.PropertyChanged
        };

#if IS_NET_9_0_OR_GREATER
    readonly Lock sourcesAccess = new();
#else
    readonly object sourcesAccess = new();
#endif
    readonly Dictionary<(object Source, DirectSubscriptionRegistration Registration), DirectSubscriptionSource> sources = new(KeyComparer.Default);

    internal DirectSubscriptionAttachment Attach(object source, DirectSubscriptionKind kind, string? propertyName, DirectObservableExpression observation, bool forcesNotification)
    {
        var attachment = new DirectSubscriptionAttachment(observation, kind, propertyName, forcesNotification);
        lock (sourcesAccess)
        {
            var registration = RegistrationOf(kind);
            var key = (source, registration);
            if (!sources.TryGetValue(key, out var subscriptionSource))
            {
                subscriptionSource = new DirectSubscriptionSource(source, registration);
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
            if (attachment.IsRemoved)
                return;
            subscriptionSource.Detach(attachment);
            if (subscriptionSource.Attachments > 0)
                return;
            sources.Remove(subscriptionSource.Key);
            subscriptionSource.Release();
        }
    }
}
