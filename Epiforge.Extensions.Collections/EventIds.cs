namespace Epiforge.Extensions.Collections;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

/// <summary>
/// Provides all the event IDs for the Epiforge.Extensions.Collections namespace
/// </summary>
public static class EventIds
{
    public static readonly EventId Epiforge_Extensions_Collections_RaisingCollectionChanged = new(1, nameof(Epiforge_Extensions_Collections_RaisingCollectionChanged));
    public static readonly EventId Epiforge_Extensions_Collections_RaisedCollectionChanged = new(2, nameof(Epiforge_Extensions_Collections_RaisedCollectionChanged));
    public static readonly EventId Epiforge_Extensions_Collections_RaisingDictionaryChanged = new(3, nameof(Epiforge_Extensions_Collections_RaisingDictionaryChanged));
    public static readonly EventId Epiforge_Extensions_Collections_RaisedDictionaryChanged = new(4, nameof(Epiforge_Extensions_Collections_RaisedDictionaryChanged));
}
