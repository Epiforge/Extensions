namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// Queries built on one thread while what they are built over changes on another, after the collection observer has begun observing the sequence
/// </summary>
/// <remarks>
/// Each query here is held part way through its build, at a read of a member or a comparison the query cannot build without, while another thread makes its change. Both run on threads of their own, so neither waits on a busy thread pool. The change may rightly wait for the build to finish, and the test cannot tell a change which is waiting from one which is merely slow, so once the change has begun it gives it a moment to finish and then lets the build go on. Under either order a correct query ends agreeing with what it is built over, which is all these rows assert
/// </remarks>
[TestClass]
public class QueryConstructionUnderChange
{
    static readonly TimeSpan patience = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Holds the first read made after it is armed until the test releases it
    /// </summary>
    public sealed class Gate
    {
        int armed;

        public ManualResetEventSlim Reading { get; } = new();

        public ManualResetEventSlim Released { get; } = new();

        public void Arm() =>
            Volatile.Write(ref armed, 1);

        public void Hold()
        {
            if (Interlocked.Exchange(ref armed, 0) == 1)
            {
                Reading.Set();
                if (!Released.Wait(patience))
                    throw new TimeoutException("the test never released a held read");
            }
        }
    }

    public sealed class GatedComparer(Gate gate) :
        IComparer<int>
    {
        public int Compare(int x, int y)
        {
            gate.Hold();
            return x.CompareTo(y);
        }
    }

    public sealed class Member(Gate gate, string name, int rank, int score) :
        INotifyPropertyChanged
    {
        int rank = rank;

        public string Name { get; } = name;

        public int Rank
        {
            get => Volatile.Read(ref rank);
            set
            {
                Volatile.Write(ref rank, value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Rank)));
            }
        }

        public int Score
        {
            get
            {
                gate.Hold();
                return score;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public override string ToString() =>
            Name;
    }

    static TQuery Race<TQuery>(Gate gate, Func<TQuery> build, Action change)
    {
        gate.Arm();
        var building = Task.Factory.StartNew(build, TaskCreationOptions.LongRunning);
        if (!gate.Reading.Wait(patience))
            Assert.Fail("the build never reached the held read");
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
        if (!building.Wait(patience))
            Assert.Fail("the query never finished building");
        if (!changing.Wait(patience))
            Assert.Fail("the change never finished");
        return building.Result;
    }

    static ObservableRangeCollection<Member> Members(Gate gate, params (int rank, int score)[] members) =>
        new(members.Select((member, index) => new Member(gate, $"M{index}", member.rank, member.score)));

    [TestMethod]
    public void AGroupingBuiltWhileTheObservedSequenceGrowsMissesNothing()
    {
        var gate = new Gate();
        var members = Members(gate, (1, 1), (1, 2));
        using var observed = CollectionObserverHelpers.Create().ObserveReadOnlyList(members);
        using var groups = Race(gate, () => observed.ObserveGroupBy(member => member.Score % 2), () => members.Add(new Member(gate, "M2", 1, 3)));
        var actual = groups.Select(group => $"{group.Key}:{string.Join(",", ((IEnumerable<Member>)group).Select(member => member.Name).OrderBy(text => text, StringComparer.Ordinal))}").OrderBy(text => text, StringComparer.Ordinal).ToList();
        var expected = members.GroupBy(member => member.Score % 2).Select(group => $"{group.Key}:{string.Join(",", group.Select(member => member.Name).OrderBy(text => text, StringComparer.Ordinal))}").OrderBy(text => text, StringComparer.Ordinal).ToList();
        CollectionAssert.AreEqual(expected, actual, $"the grouping holds {string.Join(" ", actual)}; the sequence gives {string.Join(" ", expected)}");
        Assert.IsNull(groups.OperationFault, $"the grouping faulted: {groups.OperationFault?.GetType().Name}");
    }

    [TestMethod]
    public void AMaximumByComparerBuiltWhileTheObservedSequenceGrowsMissesNothing()
    {
        var gate = new Gate();
        var numbers = new ObservableRangeCollection<int>([1, 2]);
        using var observed = CollectionObserverHelpers.Create().ObserveReadOnlyList(numbers);
        using var maximum = Race(gate, () => observed.ObserveMax(new GatedComparer(gate)), () => numbers.Add(9));
        Assert.AreEqual(9, maximum.Evaluation.Result, "the element added while the maximum was being built was missed");
        numbers.Add(0);
        Assert.AreEqual(9, maximum.Evaluation.Result, "a later change did not repair the missed element");
    }

    [TestMethod]
    public void AnOrderingBuiltWhileTheObservedSequenceGrowsMissesNothing()
    {
        var gate = new Gate();
        var members = Members(gate, (1, 3), (1, 1));
        using var observed = CollectionObserverHelpers.Create().ObserveReadOnlyList(members);
        using var ordered = Race(gate, () => observed.ObserveOrderBy(member => member.Score), () => members.Add(new Member(gate, "M2", 1, 2)));
        CollectionAssert.AreEqual(new[] { "M1", "M2", "M0" }, ordered.Select(member => member.Name).ToList(), $"the ordering holds {string.Join(",", ordered)}");
        Assert.IsNull(ordered.OperationFault, $"the ordering faulted: {ordered.OperationFault?.GetType().Name}");
    }

    [TestMethod]
    public void ASelectionBuiltWhileTheObservedSequenceGrowsMissesNothing()
    {
        var gate = new Gate();
        var members = Members(gate, (1, 1));
        using var observed = CollectionObserverHelpers.Create().ObserveReadOnlyList(members);
        using var scores = Race(gate, () => observed.ObserveSelect(member => member.Score), () => members.Add(new Member(gate, "M1", 1, 2)));
        CollectionAssert.AreEqual(new[] { 1, 2 }, scores.ToList(), $"the selection holds {string.Join(",", scores)}");
    }

    [TestMethod]
    public void ASumBuiltWhileTheObservedSequenceGrowsMissesNothing()
    {
        var gate = new Gate();
        var members = Members(gate, (1, 1), (1, 2));
        using var observed = CollectionObserverHelpers.Create().ObserveReadOnlyList(members);
        using var sum = Race(gate, () => observed.ObserveSum(member => member.Score), () => members.Add(new Member(gate, "M2", 1, 4)));
        Assert.AreEqual(7, sum.Evaluation.Result, "the element added while the sum was being built was missed");
    }

    [TestMethod]
    public void AWhereBuiltOverAWhereWhileItsMembershipChangesMissesNothing()
    {
        var gate = new Gate();
        var members = Members(gate, (1, 1), (-1, 1));
        using var observed = CollectionObserverHelpers.Create().ObserveReadOnlyList(members);
        using var ranked = observed.ObserveWhere(member => member.Rank > 0);
        using var scored = Race(gate, () => ranked.ObserveWhere(member => member.Score > 0), () => members[1].Rank = 1);
        CollectionAssert.AreEqual(ranked.ToList(), scored.ToList(), $"the inner query holds {string.Join(",", ranked)} and the query built over it holds {string.Join(",", scored)}");
    }

    [TestMethod]
    public void AWhereBuiltWhileTheObservedDictionaryGrowsMissesNothing()
    {
        var gate = new Gate();
        var members = new ObservableDictionary<int, Member> { { 1, new Member(gate, "M1", 1, 1) } };
        using var observed = CollectionObserverHelpers.Create().ObserveReadOnlyDictionary(members);
        using var scored = Race(gate, () => observed.ObserveWhere((key, member) => member.Score > 0), () => members.Add(2, new Member(gate, "M2", 1, 1)));
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, scored.Keys.ToList(), $"the query holds keys {string.Join(",", scored.Keys)}");
    }

    [TestMethod]
    public void AWhereBuiltWhileTheObservedSequenceGrowsMissesNothing()
    {
        var gate = new Gate();
        var members = Members(gate, (1, 1));
        using var observed = CollectionObserverHelpers.Create().ObserveReadOnlyList(members);
        using var scored = Race(gate, () => observed.ObserveWhere(member => member.Score > 0), () => members.Add(new Member(gate, "M1", 1, 1)));
        CollectionAssert.AreEqual(members.ToList(), scored.ToList(), $"the query holds {string.Join(",", scored)}");
    }

    [TestMethod]
    public void ToDictionaryBuiltWhileTheObservedSequenceGrowsMissesNothing()
    {
        var gate = new Gate();
        var members = Members(gate, (1, 1));
        using var observed = CollectionObserverHelpers.Create().ObserveReadOnlyList(members);
        using var scores = Race(gate, () => observed.ObserveToDictionary(member => member.Name, member => member.Score), () => members.Add(new Member(gate, "M1", 1, 2)));
        CollectionAssert.AreEquivalent(new[] { "M0", "M1" }, scores.Keys.ToList(), $"the dictionary holds keys {string.Join(",", scores.Keys)}");
    }
}
