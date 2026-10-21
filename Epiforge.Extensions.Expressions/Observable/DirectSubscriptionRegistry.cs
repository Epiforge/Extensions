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

/// <summary>
/// One subscription's place on a source object's event, which registers with the event itself while it is the only one there and is joined to a shared registration once another arrives
/// </summary>
/// <remarks>
/// Most objects a query observes are observed by one subscription, for which a shared registration is an object and a hop that serve nobody. The first subscription over an object and event therefore registers its own handler, and a second creates the shared registration without registering again: it takes over the first one's handler, which forwards to it from then on, so the object sees one handler added however many subscriptions come and go
/// </remarks>
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
    /// <summary>
    /// The observation notified, which is let go of on detachment because an attachment whose handler a shared registration took over stays registered until the last subscription over its object leaves
    /// </summary>
    internal volatile DirectObservableExpression? Observation;
    internal DirectSubscriptionAttachment? Previous;
    internal readonly string? PropertyName;
    /// <summary>
    /// The object registered with while this attachment registers for itself, or the <see cref="DirectSubscriptionSource"/> it belongs to once the registration is shared
    /// </summary>
    internal volatile object? Source;

    /// <summary>
    /// Makes the handler this attachment registers with the source's event while it is the only subscription there
    /// </summary>
    internal Delegate CreateHandler(DirectSubscriptionRegistration registration) =>
        registration switch
        {
            DirectSubscriptionRegistration.DictionaryChanged => new EventHandler<NotifyDictionaryChangedEventArgs<object?, object?>>(SourceDictionaryChanged),
            DirectSubscriptionRegistration.CollectionChanged => new NotifyCollectionChangedEventHandler(SourceCollectionChanged),
            _ => new PropertyChangedEventHandler(SourcePropertyChanged)
        };

    /// <summary>
    /// Notifies the observation of a change its source announced, unless the attachment has been detached
    /// </summary>
    internal void Notify()
    {
        if (!IsRemoved && Observation is { } observation)
            observation.OnSourceChanged(ForcesNotification);
    }

    void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Source is DirectSubscriptionSource shared)
        {
            shared.SourceCollectionChanged(sender, e);
            return;
        }
        if (IsRemoved)
            return;
        using var propagation = new PropagationScope();
        Notify();
    }

    void SourceDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e)
    {
        if (Source is DirectSubscriptionSource shared)
        {
            shared.SourceDictionaryChanged(sender, e);
            return;
        }
        if (IsRemoved)
            return;
        using var propagation = new PropagationScope();
        Notify();
    }

    /// <remarks>
    /// A change to a property this attachment did not name begins no propagation, as a shared registration finding no attachment which wants the name begins none
    /// </remarks>
    void SourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Source is DirectSubscriptionSource shared)
        {
            shared.SourcePropertyChanged(sender, e);
            return;
        }
        if (IsRemoved || !Wants(e.PropertyName))
            return;
        using var propagation = new PropagationScope();
        Notify();
    }

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
/// A registration is shared only once a second subscription arrives, and is then made around the handler the first one registered, which forwards to it; see <see cref="DirectSubscriptionAttachment"/>
/// </remarks>
sealed class DirectSubscriptionSource
{
    internal DirectSubscriptionSource(object source, DirectSubscriptionRegistration registration, Delegate handler)
    {
        this.handler = handler;
        this.registration = registration;
        this.source = source;
    }

    DirectSubscriptionAttachment? firstAttachment;
    /// <summary>
    /// The handler registered, which the attachment first over this object registered for itself and which is the one removed on release, since removing another made from the same method would allocate it
    /// </summary>
    readonly Delegate handler;
    DirectSubscriptionAttachment? lastAttachment;
    readonly DirectSubscriptionRegistration registration;
    readonly object source;

    internal int Attachments;

    internal (object Source, DirectSubscriptionRegistration Registration) Key =>
        (source, registration);

    /// <remarks>
    /// The registry calls this only while holding its own lock, which is what makes the list safe to mutate without one of its own. The attachment learns it belongs here only once it is in the list, because the attachment whose handler this registration took over forwards to it from that moment, and a change forwarded before it was listed would not reach it
    /// </remarks>
    internal void Attach(DirectSubscriptionAttachment attachment)
    {
        attachment.Previous = lastAttachment;
        if (lastAttachment is null)
            Volatile.Write(ref firstAttachment, attachment);
        else
            lastAttachment.Next = attachment;
        lastAttachment = attachment;
        ++Attachments;
        attachment.Source = this;
    }

    /// <remarks>
    /// The registry calls this only while holding its own lock, which is what makes the list safe to mutate without one of its own
    /// </remarks>
    internal void Detach(DirectSubscriptionAttachment attachment)
    {
        if (attachment.IsRemoved)
            return;
        attachment.IsRemoved = true;
        attachment.Observation = null;
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

    internal void Release() =>
        DirectSubscriptionRegistry.Unregister(source, registration, handler);

    void NotifyAttachments()
    {
        var current = Volatile.Read(ref firstAttachment);
        while (current is not null)
        {
            var following = current.Next;
            current.Notify();
            current = following;
        }
    }

    internal void SourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        using var propagation = new PropagationScope();
        NotifyAttachments();
    }

    internal void SourceDictionaryChanged(object? sender, NotifyDictionaryChangedEventArgs<object?, object?> e)
    {
        using var propagation = new PropagationScope();
        NotifyAttachments();
    }

    /// <remarks>
    /// The list is walked to the first attachment which wants the name before a propagation is entered, so that a change to a property nobody named begins no propagation, which is what a handler registered per property did by returning
    /// </remarks>
    internal void SourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
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
            if (current.Wants(reportedName))
                current.Notify();
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

    static void Register(object source, DirectSubscriptionRegistration registration, Delegate handler)
    {
        switch (registration)
        {
            case DirectSubscriptionRegistration.DictionaryChanged:
                ((INotifyDictionaryChanged)source).DictionaryChanged += (EventHandler<NotifyDictionaryChangedEventArgs<object?, object?>>)handler;
                break;
            case DirectSubscriptionRegistration.CollectionChanged:
                ((INotifyCollectionChanged)source).CollectionChanged += (NotifyCollectionChangedEventHandler)handler;
                break;
            default:
                ((INotifyPropertyChanged)source).PropertyChanged += (PropertyChangedEventHandler)handler;
                break;
        }
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

    internal static void Unregister(object source, DirectSubscriptionRegistration registration, Delegate handler)
    {
        switch (registration)
        {
            case DirectSubscriptionRegistration.DictionaryChanged:
                ((INotifyDictionaryChanged)source).DictionaryChanged -= (EventHandler<NotifyDictionaryChangedEventArgs<object?, object?>>)handler;
                break;
            case DirectSubscriptionRegistration.CollectionChanged:
                ((INotifyCollectionChanged)source).CollectionChanged -= (NotifyCollectionChangedEventHandler)handler;
                break;
            default:
                ((INotifyPropertyChanged)source).PropertyChanged -= (PropertyChangedEventHandler)handler;
                break;
        }
    }

    /// <summary>
    /// What each object and event is registered through: the handler of the one attachment registered for itself, or the shared registration once there is more than one
    /// </summary>
    readonly Dictionary<(object Source, DirectSubscriptionRegistration Registration), object> registrations = new(KeyComparer.Default);
#if IS_NET_9_0_OR_GREATER
    readonly Lock registrationsAccess = new();
#else
    readonly object registrationsAccess = new();
#endif

    internal DirectSubscriptionAttachment Attach(object source, DirectSubscriptionKind kind, string? propertyName, DirectObservableExpression observation, bool forcesNotification)
    {
        var attachment = new DirectSubscriptionAttachment(observation, kind, propertyName, forcesNotification);
        lock (registrationsAccess)
        {
            var registration = RegistrationOf(kind);
            var key = (source, registration);
            if (!registrations.TryGetValue(key, out var existing))
            {
                var handler = attachment.CreateHandler(registration);
                Register(source, registration, handler);
                attachment.Source = source;
                registrations.Add(key, handler);
            }
            else if (existing is DirectSubscriptionSource shared)
                shared.Attach(attachment);
            else
            {
                var firstHandler = (Delegate)existing;
                var first = (DirectSubscriptionAttachment)firstHandler.Target!;
                var sharing = new DirectSubscriptionSource(source, registration, firstHandler);
                sharing.Attach(first);
                sharing.Attach(attachment);
                registrations[key] = sharing;
            }
        }
        return attachment;
    }

    internal void Detach(DirectSubscriptionAttachment attachment)
    {
        if (attachment.Source is null)
            return;
        lock (registrationsAccess)
        {
            if (attachment.IsRemoved)
                return;
            if (attachment.Source is DirectSubscriptionSource shared)
            {
                shared.Detach(attachment);
                if (shared.Attachments > 0)
                    return;
                registrations.Remove(shared.Key);
                shared.Release();
                return;
            }
            var source = attachment.Source;
            attachment.IsRemoved = true;
            attachment.Observation = null;
            var registration = RegistrationOf(attachment.Kind);
            if (registrations.Remove((source, registration), out var handler))
                Unregister(source, registration, (Delegate)handler);
        }
    }
}
