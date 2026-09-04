namespace Epiforge.Extensions.Expressions.Observable.Query;

/// <summary>
/// Identifies one of the events with which a dictionary query describes a change, so that a query which produces an event's arguments at a cost may produce only the ones somebody is listening for
/// </summary>
enum ObservableDictionaryChangeObservation
{
    BoxedDictionary,
    Collection,
    Dictionary
}
