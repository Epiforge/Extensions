namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Queries disposed of on one thread while what they are built over changes on another
/// </summary>
/// <remarks>
/// Each query is held part way through its disposal, where it stops listening to one of its elements, while another thread adds an element. Both run on threads of their own, so neither waits on a busy thread pool. The change may rightly wait for the disposal to finish, and the test cannot tell a change which is waiting from one which is merely slow, so once the change has begun it gives it a moment to finish and then lets the disposal go on. Under either order a correct query throws into neither thread and is listening to no element once it is disposed of, which is all these rows assert
/// </remarks>
[TestClass]
public class QueryDisposalUnderChange
{
    static readonly TimeSpan patience = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Holds the first unsubscription made after it is armed until the test releases it
    /// </summary>
    public sealed class Gate
    {
        int armed;

        public ManualResetEventSlim Leaving { get; } = new();

        public ManualResetEventSlim Released { get; } = new();

        public void Arm() =>
            Volatile.Write(ref armed, 1);

        public void Hold()
        {
            if (Interlocked.Exchange(ref armed, 0) == 1)
            {
                Leaving.Set();
                if (!Released.Wait(patience))
                    throw new TimeoutException("the test never released a held unsubscription");
            }
        }
    }

    public sealed class Member(Gate gate, string name, int rank) :
        INotifyPropertyChanged
    {
        readonly object access = new();
        PropertyChangedEventHandler? propertyChanged;
        int subscribers;

        public string Name { get; } = name;

        public int Rank { get; } = rank;

        public int Subscribers =>
            Volatile.Read(ref subscribers);

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                lock (access)
                {
                    propertyChanged += value;
                    ++subscribers;
                }
            }
            remove
            {
                gate.Hold();
                lock (access)
                {
                    propertyChanged -= value;
                    --subscribers;
                }
            }
        }

        public override string ToString() =>
            Name;
    }

    static IDisposable Query(IObservableCollectionQuery<Member> observed, string operation) =>
        operation switch
        {
            "Select" => observed.ObserveSelect(member => member.Rank),
            "Where" => observed.ObserveWhere(member => member.Rank > 0),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

    static IDisposable Query(IObservableDictionaryQuery<string, Member> observed, string operation) =>
        operation switch
        {
            "Select" => observed.ObserveSelect((key, member) => key, (key, member) => member.Rank),
            "ToCollection" => observed.ObserveToCollection((key, member) => member.Rank),
            "Where" => observed.ObserveWhere((key, member) => member.Rank > 0),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

    static void Race(Gate gate, IDisposable query, Action change, Func<IEnumerable<Member>> members)
    {
        gate.Arm();
        var disposing = Task.Factory.StartNew(() =>
        {
            try
            {
                query.Dispose();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }, TaskCreationOptions.LongRunning);
        if (!gate.Leaving.Wait(patience))
            Assert.Fail("the disposal never reached the held unsubscription");
        var changeBegan = new ManualResetEventSlim();
        var changing = Task.Factory.StartNew(() =>
        {
            changeBegan.Set();
            try
            {
                change();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }, TaskCreationOptions.LongRunning);
        if (!changeBegan.Wait(patience))
            Assert.Fail("the change never began");
        changing.Wait(TimeSpan.FromMilliseconds(200));
        gate.Released.Set();
        if (!disposing.Wait(patience))
            Assert.Fail("the disposal never finished");
        if (!changing.Wait(patience))
            Assert.Fail("the change never finished");
        Assert.IsNull(disposing.Result, $"the disposal threw {disposing.Result?.GetType().Name}: {disposing.Result?.Message}");
        Assert.IsNull(changing.Result, $"the change threw {changing.Result?.GetType().Name}: {changing.Result?.Message}");
        var watched = members().Where(member => member.Subscribers != 0).Select(member => $"{member.Name}={member.Subscribers}").ToList();
        Assert.AreEqual(0, watched.Count, $"the disposed query still watches {string.Join(", ", watched)}");
    }

    [TestMethod]
    [DataRow("Select")]
    [DataRow("Where")]
    public void AQueryDisposedOfWhileTheObservedSequenceGrowsThrowsNothingAndLeavesNoElementWatched(string operation)
    {
        var gate = new Gate();
        var members = new ObservableRangeCollection<Member>([new Member(gate, "M0", 1), new Member(gate, "M1", 2)]);
        using var observed = CollectionObserverHelpers.Create().ObserveReadOnlyList(members);
        Race(gate, Query(observed, operation), () => members.Add(new Member(gate, "M2", 3)), () => members);
    }

    [TestMethod]
    [DataRow("Select")]
    [DataRow("ToCollection")]
    [DataRow("Where")]
    public void AQueryDisposedOfWhileTheObservedDictionaryGrowsThrowsNothingAndLeavesNoValueWatched(string operation)
    {
        var gate = new Gate();
        var members = new ObservableDictionary<string, Member> { ["M0"] = new Member(gate, "M0", 1), ["M1"] = new Member(gate, "M1", 2) };
        using var observed = CollectionObserverHelpers.Create().ObserveReadOnlyDictionary(members);
        Race(gate, Query(observed, operation), () => members.Add("M2", new Member(gate, "M2", 3)), () => members.Values);
    }
}
