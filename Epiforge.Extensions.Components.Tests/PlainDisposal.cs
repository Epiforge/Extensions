namespace Epiforge.Extensions.Components.Tests;

using System.Runtime.CompilerServices;

[TestClass]
public class PlainDisposal
{
    class AsyncDerivation :
        PlainAsyncDisposable
    {
        internal int disposals;

        protected override ValueTask<bool> DisposeAsyncCore()
        {
            ++disposals;
            return new(true);
        }
    }

    class GatedDerivation :
        PlainDisposable
    {
        internal bool allowed = true;
        internal int disposals;

        protected override bool DisposeCore()
        {
            if (!allowed)
                return false;
            ++disposals;
            return true;
        }

        protected override async ValueTask<bool> DisposeAsyncCore()
        {
            await Task.Yield();
            return DisposeCore();
        }
    }

    class SyncDerivation :
        PlainSyncDisposable
    {
        internal int disposals;

        protected override bool DisposeCore()
        {
            ++disposals;
            return true;
        }
    }

    static int finalizations;

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void AbandonAPlainDerivationWithoutDisposing() =>
        _ = new SyncDerivation();

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void AbandonANotifyingDerivationWithoutDisposing() =>
        _ = new FinalizedDerivation();

    class FinalizedDerivation :
        Components.SyncDisposable
    {
        protected override bool Dispose(bool disposing)
        {
            if (!disposing)
                Interlocked.Increment(ref finalizations);
            return true;
        }
    }

    static void CollectAndFinalize()
    {
        for (var attempt = 0; attempt < 3; ++attempt)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        GC.Collect();
    }

    [TestMethod]
    [Timeout(30000)]
    public void APlainDerivationIsNotFinalizable()
    {
        Volatile.Write(ref finalizations, 0);
        AbandonANotifyingDerivationWithoutDisposing();
        CollectAndFinalize();
        Assert.AreNotEqual(0, Volatile.Read(ref finalizations), "an abandoned notifying derivation was never finalized, so this test cannot tell whether a plain one avoids finalization");

        const BindingFlags declared = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        Assert.IsNotNull(typeof(Components.SyncDisposable).GetMethod("Finalize", declared), "SyncDisposable no longer declares a finalizer, so this test is no longer checking anything");
        Assert.IsNull(typeof(PlainDisposable).GetMethod("Finalize", declared), "PlainDisposable declares a finalizer");
        Assert.IsNull(typeof(PlainSyncDisposable).GetMethod("Finalize", declared), "PlainSyncDisposable declares a finalizer");
        Assert.IsNull(typeof(PlainAsyncDisposable).GetMethod("Finalize", declared), "PlainAsyncDisposable declares a finalizer");

        AbandonAPlainDerivationWithoutDisposing();
        CollectAndFinalize();
    }

    [TestMethod]
    [Timeout(5000)]
    public void DisposalIsNotRepeatedAfterADisposedHandlerThrows()
    {
        var derivation = new SyncDerivation();
        derivation.Disposed += (sender, e) =>
            throw new InvalidOperationException();
        Assert.ThrowsException<InvalidOperationException>(derivation.Dispose);
        Assert.IsTrue(derivation.IsDisposed);
        derivation.Dispose();
        Assert.AreEqual(1, derivation.disposals);
    }

    [TestMethod]
    [Timeout(5000)]
    public void DisposalIsRetriedAfterADisposingHandlerThrows()
    {
        var derivation = new SyncDerivation();
        void thrower(object? sender, EventArgs e) =>
            throw new InvalidOperationException();
        derivation.Disposing += thrower;
        Assert.ThrowsException<InvalidOperationException>(derivation.Dispose);
        Assert.IsFalse(derivation.IsDisposed);
        derivation.Disposing -= thrower;
        derivation.Dispose();
        Assert.AreEqual(1, derivation.disposals);
        Assert.IsTrue(derivation.IsDisposed);
    }

    [TestMethod]
    [Timeout(5000)]
    public void OverriddenDisposalRaisesDisposalOverriddenAndMayBeRetried()
    {
        var derivation = new GatedDerivation { allowed = false };
        var overridden = 0;
        derivation.DisposalOverridden += (sender, e) => ++overridden;
        derivation.Dispose();
        Assert.AreEqual(1, overridden);
        Assert.IsFalse(derivation.IsDisposed);
        Assert.AreEqual(0, derivation.disposals);
        derivation.allowed = true;
        derivation.Dispose();
        Assert.AreEqual(1, derivation.disposals);
        Assert.IsTrue(derivation.IsDisposed);
    }

    [TestMethod]
    [Timeout(5000)]
    public void RepeatedDisposalDisposesOnce()
    {
        var derivation = new SyncDerivation();
        derivation.Dispose();
        derivation.Dispose();
        derivation.Dispose();
        Assert.AreEqual(1, derivation.disposals);
        Assert.IsTrue(derivation.IsDisposed);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task RepeatedDisposalDisposesOnceAsynchronously()
    {
        var derivation = new AsyncDerivation();
        await derivation.DisposeAsync();
        await derivation.DisposeAsync();
        Assert.AreEqual(1, derivation.disposals);
        Assert.IsTrue(derivation.IsDisposed);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task TheCombinedVariantDisposesOnceAcrossBothPaths()
    {
        var derivation = new GatedDerivation();
        derivation.Dispose();
        await derivation.DisposeAsync();
        Assert.AreEqual(1, derivation.disposals);
        Assert.IsTrue(derivation.IsDisposed);
    }
}
