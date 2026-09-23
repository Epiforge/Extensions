namespace Epiforge.Extensions.Benchmarking;

/// <summary>
/// Prices acquiring a monitor and taking an identity hash code, on objects fresh and long-lived, with and without a sync block, against the <see cref="Lock"/> the query caches already use
/// </summary>
/// <remarks>
/// GS5's profile of 6.0.2 spent 38% of its benchmark thread in <c>Monitor.Enter_Slowpath</c> with nothing else running, largely on per-query lock objects created moments before, and 10.6% in the slow worker behind <c>RuntimeHelpers.GetHashCode</c>. These arms separate the first acquisition of a fresh object from a repeated one, an object whose header holds a hash code or a sync block from one whose header is clear, and a monitor from a <see cref="Lock"/>, so that whichever of them is dear is named by a figure rather than by an account of the runtime
/// </remarks>
[MemoryDiagnoser]
public class MonitorAcquisitionBenchmarks
{
    object hashedOnce = null!;
    object inflated = null!;
    Lock longLivedLock = null!;
    object longLivedObject = null!;

    [Benchmark(Baseline = true)]
    public object AllocateObject() =>
        new();

    [Benchmark]
    public int HashFreshObject() =>
        RuntimeHelpers.GetHashCode(new object());

    [Benchmark]
    public int HashLongLivedObject() =>
        RuntimeHelpers.GetHashCode(hashedOnce);

    [Benchmark]
    public int HashObjectWithSyncBlock() =>
        RuntimeHelpers.GetHashCode(inflated);

    [Benchmark]
    public object LockFreshHashedObject()
    {
        var fresh = new object();
        _ = RuntimeHelpers.GetHashCode(fresh);
        lock (fresh)
        {
        }
        return fresh;
    }

    [Benchmark]
    public Lock LockFreshLock()
    {
        var fresh = new Lock();
        lock (fresh)
        {
        }
        return fresh;
    }

    [Benchmark]
    public object LockFreshObject()
    {
        var fresh = new object();
        lock (fresh)
        {
        }
        return fresh;
    }

    [Benchmark]
    public object LockFreshObjectTwice()
    {
        var fresh = new object();
        lock (fresh)
        {
        }
        lock (fresh)
        {
        }
        return fresh;
    }

    [Benchmark]
    public void LockLongLivedLock()
    {
        lock (longLivedLock)
        {
        }
    }

    [Benchmark]
    public void LockLongLivedObject()
    {
        lock (longLivedObject)
        {
        }
    }

    [Benchmark]
    public void LockObjectWithSyncBlock()
    {
        lock (inflated)
        {
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        hashedOnce = new();
        _ = RuntimeHelpers.GetHashCode(hashedOnce);
        inflated = new();
        lock (inflated)
            _ = RuntimeHelpers.GetHashCode(inflated);
        longLivedObject = new();
        lock (longLivedObject)
        {
        }
        longLivedLock = new();
    }
}
