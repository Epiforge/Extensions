namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Queries over an <see cref="ObservableConcurrentDictionary{TKey, TValue}"/> changed by two threads, whose notifications can therefore arrive in a different order from the changes they describe
/// </summary>
/// <remarks>
/// A handler subscribed ahead of the query holds one change's notification on its writer's thread, standing in for a writer preempted between its change and its notification, while the test changes the same key from another thread. The dictionary allows that order and the query must end agreeing with the dictionary regardless
/// </remarks>
[TestClass]
public class ConcurrentDictionaryNotificationOrder
{
    static readonly TimeSpan patience = TimeSpan.FromSeconds(10);

    static void AssertAgrees(ObservableConcurrentDictionary<int, int> dictionary, IReadOnlyDictionary<int, int> query)
    {
        var expected = string.Join(",", dictionary.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key}={entry.Value}"));
        var actual = string.Join(",", query.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key}={entry.Value}"));
        Assert.AreEqual(expected, actual, "the query does not agree with the dictionary");
    }

    static void HoldFirstAdd(ObservableConcurrentDictionary<int, int> dictionary, ManualResetEventSlim holding, ManualResetEventSlim released)
    {
        var armed = 1;
        dictionary.DictionaryChanged += (sender, e) =>
        {
            if (e.Action is NotifyDictionaryChangedAction.Add && Interlocked.Exchange(ref armed, 0) == 1)
            {
                holding.Set();
                if (!released.Wait(patience))
                    throw new TimeoutException("the test never released the held notification");
            }
        };
    }

    static IObservableDictionaryQuery<int, int> Query(IObservableDictionaryQuery<int, int> observed, string operation) =>
        operation switch
        {
            "Select" => observed.ObserveSelect((key, value) => key, (key, value) => value),
            "Where" => observed.ObserveWhere((key, value) => value > 0),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

    [TestMethod]
    [DataRow("Select")]
    [DataRow("Where")]
    public void AnAddNotifiedAfterALaterAddDoesNotThrowIntoItsWriter(string operation)
    {
        var dictionary = new ObservableConcurrentDictionary<int, int>();
        var holding = new ManualResetEventSlim();
        var released = new ManualResetEventSlim();
        HoldFirstAdd(dictionary, holding, released);
        using var observed = CollectionObserverHelpers.Create().ObserveReadOnlyDictionary(dictionary);
        using var query = Query(observed, operation);
        var adding = Task.Factory.StartNew(() =>
        {
            try
            {
                dictionary.TryAdd(1, 10);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }, TaskCreationOptions.LongRunning);
        if (!holding.Wait(patience))
            Assert.Fail("the first add was never notified");
        Assert.IsTrue(dictionary.TryRemove(1, out _));
        Assert.IsTrue(dictionary.TryAdd(1, 20));
        released.Set();
        if (!adding.Wait(patience))
            Assert.Fail("the first add never finished");
        Assert.IsNull(adding.Result, $"the first add's writer received {adding.Result?.GetType().Name}: {adding.Result?.Message}");
        AssertAgrees(dictionary, query);
    }

    [TestMethod]
    [DataRow("Select")]
    [DataRow("Where")]
    public void ARemoveNotifiedBeforeTheAddItFollowsLeavesTheQueryAgreeing(string operation)
    {
        var dictionary = new ObservableConcurrentDictionary<int, int>();
        var holding = new ManualResetEventSlim();
        var released = new ManualResetEventSlim();
        HoldFirstAdd(dictionary, holding, released);
        using var observed = CollectionObserverHelpers.Create().ObserveReadOnlyDictionary(dictionary);
        using var query = Query(observed, operation);
        var adding = Task.Factory.StartNew(() => dictionary.TryAdd(1, 10), TaskCreationOptions.LongRunning);
        if (!holding.Wait(patience))
            Assert.Fail("the add was never notified");
        Assert.IsTrue(dictionary.TryRemove(1, out _));
        released.Set();
        if (!adding.Wait(patience))
            Assert.Fail("the add never finished");
        AssertAgrees(dictionary, query);
    }
}
