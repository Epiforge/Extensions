namespace Epiforge.Extensions.Expressions.Tests.Observable;

public sealed class SubscriptionLog
{
    readonly List<(object Target, string EventName, int Delta)> entries = [];
    readonly Dictionary<object, int> identities = new(ReferenceEqualityComparer.Instance);

    public int Outstanding
    {
        get
        {
            var outstanding = 0;
            for (int i = 0, ii = entries.Count; i < ii; ++i)
                outstanding += entries[i].Delta;
            return outstanding;
        }
    }

    public string Describe(object target, string eventName) =>
        $"{eventName}#{IdentityOf(target)}";

    public IReadOnlyList<string> Attachments()
    {
        var attachments = new List<string>();
        for (int i = 0, ii = entries.Count; i < ii; ++i)
            if (entries[i] is { Delta: > 0 } entry)
                attachments.Add(Describe(entry.Target, entry.EventName));
        attachments.Sort(StringComparer.Ordinal);
        return attachments;
    }

    internal void Record(object target, string eventName, int delta) =>
        entries.Add((target, eventName, delta));

    int IdentityOf(object target)
    {
        if (!identities.TryGetValue(target, out var identity))
        {
            identity = identities.Count;
            identities.Add(target, identity);
        }
        return identity;
    }
}

public class Recorded(SubscriptionLog log) :
    INotifyPropertyChanged
{
    Recorded? next;
    PropertyChangedEventHandler? propertyChanged;
    int rank;
    int score;
    string? tag;

    internal readonly SubscriptionLog Log = log;

    public Recorded? Next
    {
        get => next;
        set
        {
            next = value;
            propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Next)));
        }
    }

    public string? Tag
    {
        get => tag;
        set
        {
            tag = value;
            propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Tag)));
        }
    }

    public int Rank
    {
        get => rank;
        set
        {
            rank = value;
            propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Rank)));
        }
    }

    public int Score
    {
        get => score;
        set
        {
            score = value;
            propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Score)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add
        {
            Log.Record(this, nameof(PropertyChanged), 1);
            propertyChanged += value;
        }
        remove
        {
            Log.Record(this, nameof(PropertyChanged), -1);
            propertyChanged -= value;
        }
    }
}

public sealed class RecordedCollection(SubscriptionLog log) :
    Collection<Recorded>,
    INotifyCollectionChanged,
    INotifyPropertyChanged
{
    NotifyCollectionChangedEventHandler? collectionChanged;
    PropertyChangedEventHandler? propertyChanged;

    public void Announce()
    {
        collectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add
        {
            log.Record(this, nameof(CollectionChanged), 1);
            collectionChanged += value;
        }
        remove
        {
            log.Record(this, nameof(CollectionChanged), -1);
            collectionChanged -= value;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add
        {
            log.Record(this, nameof(PropertyChanged), 1);
            propertyChanged += value;
        }
        remove
        {
            log.Record(this, nameof(PropertyChanged), -1);
            propertyChanged -= value;
        }
    }
}

public sealed class RecordedTable(SubscriptionLog log) :
    INotifyCollectionChanged,
    INotifyDictionaryChanged,
    INotifyPropertyChanged
{
    NotifyCollectionChangedEventHandler? collectionChanged;
    EventHandler<NotifyDictionaryChangedEventArgs<object?, object?>>? dictionaryChanged;
    PropertyChangedEventHandler? propertyChanged;

    public int Count { get; private set; }

    public void Announce()
    {
        ++Count;
        collectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        dictionaryChanged?.Invoke(this, new NotifyDictionaryChangedEventArgs<object?, object?>(NotifyDictionaryChangedAction.Reset));
        propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add
        {
            log.Record(this, nameof(CollectionChanged), 1);
            collectionChanged += value;
        }
        remove
        {
            log.Record(this, nameof(CollectionChanged), -1);
            collectionChanged -= value;
        }
    }

    public event EventHandler<NotifyDictionaryChangedEventArgs<object?, object?>>? DictionaryChanged
    {
        add
        {
            log.Record(this, nameof(DictionaryChanged), 1);
            dictionaryChanged += value;
        }
        remove
        {
            log.Record(this, nameof(DictionaryChanged), -1);
            dictionaryChanged -= value;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add
        {
            log.Record(this, nameof(PropertyChanged), 1);
            propertyChanged += value;
        }
        remove
        {
            log.Record(this, nameof(PropertyChanged), -1);
            propertyChanged -= value;
        }
    }
}
