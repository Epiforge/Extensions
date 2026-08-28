namespace Epiforge.Extensions.Components.Tests;

using System.Runtime.CompilerServices;

[TestClass]
public class DisposalFinalization
{
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

    static int finalizations;

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void AbandonWithoutDisposing() =>
        _ = new FinalizedDerivation();

    static void CollectAndFinalize()
    {
        for (var attempt = 0; attempt < 3; ++attempt)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        GC.Collect();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void DisposeWhileADisposedHandlerThrows()
    {
        var derivation = new FinalizedDerivation();
        derivation.Disposed += (sender, e) =>
            throw new InvalidOperationException();
        try
        {
            derivation.Dispose();
        }
        catch (InvalidOperationException)
        {
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void DisposeWhileAPropertyChangedHandlerThrows()
    {
        var derivation = new FinalizedDerivation();
        derivation.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(Components.SyncDisposable.IsDisposed))
                throw new InvalidOperationException();
        };
        try
        {
            derivation.Dispose();
        }
        catch (InvalidOperationException)
        {
        }
    }

    [TestMethod]
    [Timeout(30000)]
    public void AnObjectDisposedWhileAHandlerThrowsIsStillNotFinalized()
    {
        Volatile.Write(ref finalizations, 0);
        AbandonWithoutDisposing();
        CollectAndFinalize();
        Assert.AreNotEqual(0, Volatile.Read(ref finalizations), "an abandoned object was never finalized, so this test cannot tell whether disposal suppressed finalization");

        Volatile.Write(ref finalizations, 0);
        DisposeWhileADisposedHandlerThrows();
        CollectAndFinalize();
        Assert.AreEqual(0, Volatile.Read(ref finalizations), "a Disposed handler which threw left the object finalizable");

        Volatile.Write(ref finalizations, 0);
        DisposeWhileAPropertyChangedHandlerThrows();
        CollectAndFinalize();
        Assert.AreEqual(0, Volatile.Read(ref finalizations), "a PropertyChanged handler which threw left the object finalizable");
    }
}
