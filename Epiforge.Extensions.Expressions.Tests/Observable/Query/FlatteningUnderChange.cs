namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// A flattening read, or built over, while a sequence its selector returned changes on another thread, after the flattening has begun observing that sequence
/// </summary>
/// <remarks>
/// Each row holds one thread part way through reading the flattening, either inside a subscriber to one change or inside a query being built, while another thread changes the same inner sequence. Both run on threads of their own, so neither waits on a busy thread pool. The change may rightly wait for the reader to finish, and the test cannot tell a change which is waiting from one which is merely slow, so once the change has begun it gives it a moment to finish and then lets the reader go on. A correct flattening only ever shows what it has announced, and a query built over it ends agreeing with it, which is all these rows assert
/// </remarks>
[TestClass]
public class FlatteningUnderChange
{
    static readonly TimeSpan patience = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Holds the first enumeration of an inner list or read of a member's score made after it is armed until the test releases it
    /// </summary>
    public sealed class Gate
    {
        int armed;

        public ManualResetEventSlim Held { get; } = new();

        public ManualResetEventSlim Released { get; } = new();

        public void Arm() =>
            Volatile.Write(ref armed, 1);

        public void Hold()
        {
            if (Interlocked.Exchange(ref armed, 0) == 1)
            {
                Held.Set();
                if (!Released.Wait(patience))
                    throw new TimeoutException("the test never released a held read");
            }
        }
    }

    /// <summary>
    /// A list which is safe to read while another thread changes it and announces each change after making it, as the readme asks of anything observed across threads
    /// </summary>
    public sealed class GuardedList<T>(Gate gate, IEnumerable<T> items) :
        IReadOnlyList<T>,
        INotifyCollectionChanged
    {
        readonly object access = new();
        readonly List<T> items = [.. items];

        public T this[int index]
        {
            get
            {
                lock (access)
                    return items[index];
            }
        }

        public int Count
        {
            get
            {
                lock (access)
                    return items.Count;
            }
        }

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        public IEnumerator<T> GetEnumerator()
        {
            gate.Hold();
            lock (access)
                return items.ToList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();

        public void Insert(int index, T item)
        {
            lock (access)
                items.Insert(index, item);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }
    }

    public sealed class Holder<T>(IEnumerable<T> items)
    {
        public IEnumerable<T> Items { get; } = items;
    }

    public sealed class Member(Gate gate, string name, int score)
    {
        public string Name { get; } = name;

        public int Score
        {
            get
            {
                gate.Hold();
                return score;
            }
        }

        public override string ToString() =>
            Name;
    }

    static void Release(Gate gate, Action change)
    {
        var changeBegan = new ManualResetEventSlim();
        var changing = Task.Factory.StartNew(() =>
        {
            changeBegan.Set();
            change();
        }, TaskCreationOptions.LongRunning);
        if (!changeBegan.Wait(patience))
            Assert.Fail("the change never began");
        changing.Wait(TimeSpan.FromMilliseconds(200));
        gate.Released.Set();
        if (!changing.Wait(patience))
            Assert.Fail("the change never finished");
    }

    [TestMethod]
    public void AFlatteningReadInsideOneChangeShowsWhatItAnnouncedWhileAnotherChangeWaits()
    {
        var gate = new Gate();
        var inner = new ObservableRangeCollection<int>([1, 2]);
        var outer = new ObservableRangeCollection<Holder<int>>([new Holder<int>(inner)]);
        using var observed = CollectionObserverHelpers.Create().ObserveReadOnlyList(outer);
        using var flattened = observed.ObserveSelectMany(holder => holder.Items);
        var readings = new List<string>();
        var armed = 1;
        flattened.CollectionChanged += (sender, e) =>
        {
            if (Interlocked.Exchange(ref armed, 0) != 1)
                return;
            gate.Held.Set();
            if (!gate.Released.Wait(patience))
                throw new TimeoutException("the test never released the held subscriber");
            readings.Add($"announced {e.NewItems![0]} at {e.NewStartingIndex}, read {flattened[e.NewStartingIndex]} there, count {flattened.Count}");
        };
        var adding = Task.Factory.StartNew(() => inner.Add(3), TaskCreationOptions.LongRunning);
        if (!gate.Held.Wait(patience))
            Assert.Fail("the first change was never announced");
        Release(gate, () => inner.Insert(0, 0));
        if (!adding.Wait(patience))
            Assert.Fail("the first change never finished");
        CollectionAssert.AreEqual(new[] { "announced 3 at 2, read 3 there, count 3" }, readings, $"inside the first change the flattening showed: {string.Join("; ", readings)}");
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, flattened.ToList(), $"the flattening ends holding {string.Join(",", flattened)}");
    }

    [TestMethod]
    public void AWhereBuiltOverAFlatteningWhileAnInnerSequenceGrowsMissesNothingAndTakesNothingTwice()
    {
        var gate = new Gate();
        var inner = new GuardedList<Member>(gate, [new Member(gate, "M0", 1), new Member(gate, "M1", 2)]);
        var outer = new ObservableRangeCollection<Holder<Member>>([new Holder<Member>(inner)]);
        using var observed = CollectionObserverHelpers.Create().ObserveReadOnlyList(outer);
        using var flattened = observed.ObserveSelectMany(holder => holder.Items);
        gate.Arm();
        var building = Task.Factory.StartNew(() => flattened.ObserveWhere(member => member.Score > 0), TaskCreationOptions.LongRunning);
        if (!gate.Held.Wait(patience))
            Assert.Fail("the build never reached a held read");
        Release(gate, () => inner.Insert(0, new Member(gate, "M2", 3)));
        if (!building.Wait(patience))
            Assert.Fail("the query never finished building");
        using var where = building.Result;
        var actual = where.Select(member => member.Name).OrderBy(name => name, StringComparer.Ordinal).ToList();
        CollectionAssert.AreEqual(new[] { "M0", "M1", "M2" }, actual, $"the filter holds {string.Join(",", actual)}");
    }
}
