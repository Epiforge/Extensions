namespace Epiforge.Extensions.Collections.Tests.ObjectModel;

/// <summary>
/// An operation of the concurrent dictionary which loses a race with another thread must announce what it actually did, so that its announcements, replayed in order, reproduce the dictionary
/// </summary>
[TestClass]
public class ObservableConcurrentDictionaryContention
{
    /// <summary>
    /// Compares keys as integers, puts every key in one bucket, and when armed holds the next comparison made on the arming thread until released, which pauses that thread inside the dictionary's lock-free read
    /// </summary>
    sealed class GatedComparer :
        IEqualityComparer<int>
    {
        int armed;
        int armingThread;

        public ManualResetEventSlim Reached { get; } = new(false);

        public ManualResetEventSlim Released { get; } = new(false);

        public void Arm()
        {
            armingThread = Environment.CurrentManagedThreadId;
            Volatile.Write(ref armed, 1);
        }

        public bool Equals(int x, int y)
        {
            if (Environment.CurrentManagedThreadId == armingThread && Interlocked.Exchange(ref armed, 0) == 1)
            {
                Reached.Set();
                if (!Released.Wait(Timeout))
                    throw new TimeoutException("the gated comparison was never released");
            }
            return x == y;
        }

        public int GetHashCode(int obj) =>
            0;
    }

    sealed class Recording
    {
        readonly object access = new();
        readonly List<NotifyDictionaryChangedEventArgs<int, int>> announcements = [];
        int countChanges;

        public Recording(ObservableConcurrentDictionary<int, int> dictionary)
        {
            dictionary.DictionaryChanged += (sender, e) =>
            {
                lock (access)
                    announcements.Add(e);
            };
            ((System.ComponentModel.INotifyPropertyChanged)dictionary).PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == "Count")
                    Interlocked.Increment(ref countChanges);
            };
        }

        public void AssertReplaysTo(IReadOnlyDictionary<int, int> initial, ObservableConcurrentDictionary<int, int> dictionary)
        {
            var replayed = initial.ToDictionary(pair => pair.Key, pair => pair.Value);
            var membershipChanges = 0;
            List<NotifyDictionaryChangedEventArgs<int, int>> inOrder;
            lock (access)
                inOrder = [..announcements];
            foreach (var announcement in inOrder)
            {
                var described = $"{announcement.Action} of {string.Join("+", announcement.NewItems.Select(pair => $"{pair.Key}={pair.Value}"))} over {string.Join("+", announcement.OldItems.Select(pair => $"{pair.Key}={pair.Value}"))}";
                switch (announcement.Action)
                {
                    case NotifyDictionaryChangedAction.Add:
                        foreach (var (key, value) in announcement.NewItems)
                            Assert.IsTrue(replayed.TryAdd(key, value), $"{described} added a key already present");
                        membershipChanges += announcement.NewItems.Count;
                        break;
                    case NotifyDictionaryChangedAction.Remove:
                        foreach (var (key, value) in announcement.OldItems)
                        {
                            Assert.IsTrue(replayed.TryGetValue(key, out var present), $"{described} removed a key not present");
                            Assert.AreEqual(present, value, $"{described} removed a value other than the one present");
                            replayed.Remove(key);
                        }
                        membershipChanges += announcement.OldItems.Count;
                        break;
                    case NotifyDictionaryChangedAction.Replace:
                        foreach (var (key, value) in announcement.OldItems)
                        {
                            Assert.IsTrue(replayed.TryGetValue(key, out var present), $"{described} replaced a key not present");
                            Assert.AreEqual(present, value, $"{described} replaced a value other than the one present");
                        }
                        foreach (var (key, value) in announcement.NewItems)
                            replayed[key] = value;
                        break;
                }
            }
            CollectionAssert.AreEquivalent(dictionary.ToArray(), replayed.ToArray(), "the announcements did not replay to the dictionary");
            Assert.AreEqual(membershipChanges, Volatile.Read(ref countChanges), "the count was announced other than once for each key added or removed");
        }
    }

    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    static void Contend(GatedComparer? comparer, ManualResetEventSlim? reached, ManualResetEventSlim? released, Action losing, Action winning)
    {
        var losingThread = Task.Factory.StartNew(() =>
        {
            comparer?.Arm();
            losing();
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var reachedEvent = comparer?.Reached ?? reached!;
        var releasedEvent = comparer?.Released ?? released!;
        try
        {
            Assert.IsTrue(reachedEvent.Wait(Timeout), "the losing operation never reached the point where it was to be held");
            winning();
        }
        finally
        {
            releasedEvent.Set();
        }
        Assert.IsTrue(losingThread.Wait(Timeout), "the losing operation did not finish");
    }

    static Func<int, int, int> HoldingUpdate(ManualResetEventSlim reached, ManualResetEventSlim released, int updated)
    {
        var held = 0;
        return (key, value) =>
        {
            if (Interlocked.Exchange(ref held, 1) == 0)
            {
                reached.Set();
                Assert.IsTrue(released.Wait(Timeout));
            }
            return updated;
        };
    }

    [TestMethod]
    public void AnIndexerSetWhichLosesItsKeyToARemovalAnnouncesAnAddition()
    {
        var comparer = new GatedComparer();
        var dictionary = new ObservableConcurrentDictionary<int, int>(comparer);
        dictionary.TryAdd(1, 10);
        var initial = new Dictionary<int, int>(dictionary);
        var recording = new Recording(dictionary);
        Contend(comparer, null, null, () => dictionary[1] = 20, () => Assert.IsTrue(dictionary.TryRemove(1, out _)));
        Assert.AreEqual(20, dictionary[1]);
        recording.AssertReplaysTo(initial, dictionary);
    }

    [TestMethod]
    [DataRow("value to add")]
    [DataRow("factory to add")]
    [DataRow("factory to add with an argument")]
    public void AnUpdateWhichLosesItsKeyToARemovalAnnouncesAnAddition(string overload)
    {
        var dictionary = new ObservableConcurrentDictionary<int, int>();
        dictionary.TryAdd(1, 10);
        var initial = new Dictionary<int, int>(dictionary);
        var recording = new Recording(dictionary);
        using var reached = new ManualResetEventSlim(false);
        using var released = new ManualResetEventSlim(false);
        var update = HoldingUpdate(reached, released, 20);
        var result = 0;
        Contend(null, reached, released, () =>
            result = overload switch
            {
                "value to add" => dictionary.AddOrUpdate(1, 30, update),
                "factory to add" => dictionary.AddOrUpdate(1, key => 30, update),
                _ => dictionary.AddOrUpdate(1, (key, argument) => argument, (key, value, argument) => update(key, value), 30)
            }, () => Assert.IsTrue(dictionary.TryRemove(1, out _)));
        Assert.AreEqual(30, result);
        Assert.AreEqual(30, dictionary[1]);
        recording.AssertReplaysTo(initial, dictionary);
    }

    [TestMethod]
    [DataRow("factory")]
    [DataRow("factory with an argument")]
    public void AGetOrAddWhichLosesTheAdditionToAnotherThreadAnnouncesNothing(string overload)
    {
        var dictionary = new ObservableConcurrentDictionary<int, int>();
        var initial = new Dictionary<int, int>(dictionary);
        var recording = new Recording(dictionary);
        using var reached = new ManualResetEventSlim(false);
        using var released = new ManualResetEventSlim(false);
        int holdingFactory(int key)
        {
            reached.Set();
            Assert.IsTrue(released.Wait(Timeout));
            return 10;
        }
        var result = 0;
        Contend(null, reached, released, () =>
            result = overload switch
            {
                "factory" => dictionary.GetOrAdd(1, holdingFactory),
                _ => dictionary.GetOrAdd(1, (key, argument) => holdingFactory(key), 0)
            }, () => Assert.AreEqual(30, dictionary.GetOrAdd(1, key => 30)));
        Assert.AreEqual(30, result, "the losing call did not return the winner's value");
        Assert.AreEqual(30, dictionary[1]);
        recording.AssertReplaysTo(initial, dictionary);
    }

    [TestMethod]
    public void AGetOrAddOfAValueWhichLosesTheAdditionToAnotherThreadAnnouncesNothing()
    {
        var comparer = new GatedComparer();
        var dictionary = new ObservableConcurrentDictionary<int, int>(comparer);
        dictionary.TryAdd(2, 2);
        var initial = new Dictionary<int, int>(dictionary);
        var recording = new Recording(dictionary);
        var result = 0;
        Contend(comparer, null, null, () => result = dictionary.GetOrAdd(1, 10), () => Assert.AreEqual(30, dictionary.GetOrAdd(1, 30)));
        Assert.AreEqual(30, result, "the losing call did not return the winner's value");
        Assert.AreEqual(30, dictionary[1]);
        recording.AssertReplaysTo(initial, dictionary);
    }
}
