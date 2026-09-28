namespace Epiforge.Extensions.Expressions.Tests.Observable.Query;

/// <summary>
/// A concatenation whose second operand is a query implemented outside this library, read while that operand changes on another thread after the concatenation has begun observing it
/// </summary>
/// <remarks>
/// The row holds a subscriber inside one change to the operand while another thread changes it again. Both run on threads of their own, so neither waits on a busy thread pool. The second change may rightly wait for the subscriber to finish, and the test cannot tell a change which is waiting from one which is merely slow, so once the change has begun it gives it a moment to finish and then lets the subscriber go on. A correct concatenation only ever shows what it has announced, which is all this row asserts
/// </remarks>
[TestClass]
public class ConcatenationUnderChange
{
    static readonly TimeSpan patience = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A query implemented outside this library, which answers each member a list answers by asking the list, and nothing else
    /// </summary>
    public class ForeignQuery :
        DispatchProxy
    {
        public object? Target { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            var target = Target!;
            var declaringType = targetMethod.DeclaringType!;
            if (declaringType.IsInterface && declaringType.IsAssignableFrom(target.GetType()))
            {
                var map = target.GetType().GetInterfaceMap(declaringType);
                return map.TargetMethods[Array.IndexOf(map.InterfaceMethods, targetMethod)].Invoke(target, args);
            }
            return targetMethod.Name switch
            {
                "get_OperationFault" => null,
                "get_CachedObservableQueries" => 0,
                "get_IsDisposed" => false,
                "Dispose" or "add_Disposed" or "remove_Disposed" or "add_Disposing" or "remove_Disposing" or "add_PropertyChanging" or "remove_PropertyChanging" => null,
                _ => throw new NotSupportedException($"{targetMethod.Name} is not something a list answers")
            };
        }
    }

    [TestMethod]
    public void AConcatenationReadInsideOneChangeToAForeignOperandShowsWhatItAnnouncedWhileAnotherChangeWaits()
    {
        var inner = new ObservableRangeCollection<int>([2, 3]);
        var second = DispatchProxy.Create<IObservableCollectionQuery<int>, ForeignQuery>();
        ((ForeignQuery)(object)second).Target = inner;
        using var first = CollectionObserverHelpers.Create().ObserveReadOnlyList(new ObservableRangeCollection<int>([1]));
        using var concatenated = first.ObserveConcat(second);
        var held = new ManualResetEventSlim();
        var released = new ManualResetEventSlim();
        var readings = new List<string>();
        var armed = 1;
        concatenated.CollectionChanged += (sender, e) =>
        {
            if (Interlocked.Exchange(ref armed, 0) != 1)
                return;
            held.Set();
            if (!released.Wait(patience))
                throw new TimeoutException("the test never released the held subscriber");
            readings.Add($"announced {e.NewItems![0]} at {e.NewStartingIndex}, read {concatenated[e.NewStartingIndex]} there, count {concatenated.Count}");
        };
        var adding = Task.Factory.StartNew(() => inner.Add(4), TaskCreationOptions.LongRunning);
        if (!held.Wait(patience))
            Assert.Fail("the first change was never announced");
        var changeBegan = new ManualResetEventSlim();
        var changing = Task.Factory.StartNew(() =>
        {
            changeBegan.Set();
            inner.Insert(0, 0);
        }, TaskCreationOptions.LongRunning);
        if (!changeBegan.Wait(patience))
            Assert.Fail("the second change never began");
        changing.Wait(TimeSpan.FromMilliseconds(200));
        released.Set();
        if (!adding.Wait(patience))
            Assert.Fail("the first change never finished");
        if (!changing.Wait(patience))
            Assert.Fail("the second change never finished");
        CollectionAssert.AreEqual(new[] { "announced 4 at 3, read 4 there, count 4" }, readings, $"inside the first change the concatenation showed: {string.Join("; ", readings)}");
        CollectionAssert.AreEqual(new[] { 1, 0, 2, 3, 4 }, concatenated.ToList(), $"the concatenation ends holding {string.Join(",", concatenated)}");
    }
}
