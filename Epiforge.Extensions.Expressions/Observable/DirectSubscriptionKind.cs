namespace Epiforge.Extensions.Expressions.Observable;

/// <summary>
/// Describes an event to which a direct subscription attaches
/// </summary>
public enum DirectSubscriptionKind
{
    /// <summary>
    /// No event, which is the state of a default subscription
    /// </summary>
    None,

    /// <summary>
    /// <see cref="INotifyPropertyChanged.PropertyChanged" />, acted upon when the name reported is the subscription's or is absent
    /// </summary>
    MemberPropertyChanged,

    /// <summary>
    /// <see cref="INotifyPropertyChanged.PropertyChanged" />, acted upon only when the name reported is the subscription's
    /// </summary>
    IndexerPropertyChanged,

    /// <summary>
    /// <see cref="INotifyDictionaryChanged.DictionaryChanged" /> when the value implements it, and otherwise <see cref="INotifyCollectionChanged.CollectionChanged" /> when the value implements that
    /// </summary>
    DictionaryOrCollectionChanged,

    /// <summary>
    /// <see cref="INotifyDictionaryChanged.DictionaryChanged" />, the other having been excluded by the options
    /// </summary>
    DictionaryChanged,

    /// <summary>
    /// <see cref="INotifyCollectionChanged.CollectionChanged" />, the other having been excluded by the options or by the value
    /// </summary>
    CollectionChanged
}
