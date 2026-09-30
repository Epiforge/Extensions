namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// A group taken from a grouping or a lookup, which reports its owner's fault as any query built over another does, whether the fault arose beneath the owner or in its key selector, and whichever group the faulting element would have joined
/// </summary>
[TestClass]
public class GroupFaults
{
    sealed class Member(string tag, int group, int value) :
        PropertyChangeNotifier
    {
        int group = group;
        int guard = 1;

        public int Group
        {
            get => group;
            set => SetBackedProperty(ref group, in value);
        }

        public int Guard
        {
            get => guard;
            set => SetBackedProperty(ref guard, in value);
        }

        public string Tag { get; } = tag;

        public int Value { get; } = value;
    }

    [TestMethod]
    [DataRow("grouping", "filter")]
    [DataRow("grouping", "key selector")]
    [DataRow("lookup", "filter")]
    [DataRow("lookup", "key selector")]
    public void AGroupReportsItsOwnersFault(string owner, string faultingIn)
    {
        var first = new Member("first", 1, 1);
        var second = new Member("second", 1, 2);
        var third = new Member("third", 2, 4);
        var observer = CollectionObserverHelpers.Create();
        using var sourceQuery = observer.ObserveReadOnlyList(new ObservableRangeCollection<Member>([first, second, third]));
        using var filter = faultingIn == "filter" ? sourceQuery.ObserveWhere(member => 10 / member.Guard > 0) : sourceQuery.ObserveWhere(member => member.Guard >= 0);
        Expression<Func<Member, long>> keySelector = faultingIn == "filter" ? member => member.Group : member => member.Group + 0 / member.Guard;
        IDisposable ownerQuery;
        Func<long, IObservableGrouping<long, Member>> groupFor;
        if (owner == "grouping")
        {
            var grouping = filter.ObserveGroupBy(keySelector);
            ownerQuery = grouping;
            groupFor = key => grouping.Single(group => group.Key == key);
        }
        else
        {
            var lookup = filter.ObserveToLookup(keySelector);
            ownerQuery = lookup;
            groupFor = key => lookup[key];
        }
        var takenBefore = groupFor(1);
        using var sum = takenBefore.ObserveSum(member => member.Value);
        var takenBeforeFaultWhileClean = takenBefore.OperationFault;
        var sumWhileClean = sum.Evaluation;
        third.Guard = 0;
        second.Group = 3;
        var takenWhileFaulting = groupFor(3);
        var takenBeforeFaultWhileFaulting = takenBefore.OperationFault;
        var takenWhileFaultingFaultWhileFaulting = takenWhileFaulting.OperationFault;
        var sumWhileFaulting = sum.Evaluation;
        third.Guard = 1;
        var takenBeforeFaultAfterRecovery = takenBefore.OperationFault;
        var takenWhileFaultingFaultAfterRecovery = takenWhileFaulting.OperationFault;
        var sumAfterRecovery = sum.Evaluation;
        sum.Dispose();
        ownerQuery.Dispose();
        Assert.IsNull(takenBeforeFaultWhileClean, "the group reported a fault before anything threw");
        Assert.AreEqual((null, 3), sumWhileClean, "the sum over the group was wrong before anything threw");
        Assert.IsNotNull(takenBeforeFaultWhileFaulting, "a group taken before the fault arose did not report it");
        Assert.IsNotNull(takenWhileFaultingFaultWhileFaulting, "a group taken while the owner faulted did not report the fault");
        Assert.IsNotNull(sumWhileFaulting.Fault, $"the sum over the group computed {sumWhileFaulting.Result} as though nothing faulted");
        Assert.IsNull(takenBeforeFaultAfterRecovery, "a group taken before the fault arose still reported it after it cleared");
        Assert.IsNull(takenWhileFaultingFaultAfterRecovery, "a group taken while the owner faulted still reported the fault after it cleared");
        Assert.AreEqual((null, 1), sumAfterRecovery, "the sum over the group was wrong after the fault cleared");
    }
}
