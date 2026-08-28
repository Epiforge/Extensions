namespace Epiforge.Extensions.Components.Tests;

[TestClass]
public class DisposalReentrancy
{
    class AsyncDerivation :
        Components.AsyncDisposable
    {
        internal int disposals;

        protected override ValueTask<bool> DisposeAsyncCore()
        {
            ++disposals;
            return new(true);
        }
    }

    class GatedDerivation :
        Components.Disposable
    {
        internal int disposals;

        protected override bool Dispose(bool disposing)
        {
            ++disposals;
            return true;
        }

        protected override ValueTask<bool> DisposeAsyncCore()
        {
            ++disposals;
            return new(true);
        }
    }

    class MonitoredDerivation :
        Components.SyncDisposable
    {
        internal int disposals;

        protected override bool Dispose(bool disposing)
        {
            ++disposals;
            return true;
        }
    }

    [TestMethod]
    [Timeout(5000)]
    public void DisposingHandlerMayDisposeTheSender()
    {
        var derivation = new MonitoredDerivation();
        derivation.Disposing += (sender, e) => ((IDisposable)sender!).Dispose();
        derivation.Dispose();
        Assert.AreEqual(1, derivation.disposals);
        Assert.IsTrue(derivation.IsDisposed);
    }

    [TestMethod]
    [Timeout(5000)]
    public void DisposedHandlerMayDisposeTheSender()
    {
        var derivation = new GatedDerivation();
        derivation.Disposed += (sender, e) => ((IDisposable)sender!).Dispose();
        derivation.Dispose();
        Assert.AreEqual(1, derivation.disposals);
        Assert.IsTrue(derivation.IsDisposed);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task DisposingHandlerMayDisposeTheSenderAsynchronously()
    {
        var derivation = new AsyncDerivation();
        derivation.Disposing += (sender, e) => ((IAsyncDisposable)sender!).DisposeAsync().AsTask().Wait();
        await derivation.DisposeAsync();
        Assert.AreEqual(1, derivation.disposals);
        Assert.IsTrue(derivation.IsDisposed);
    }

    [TestMethod]
    [Timeout(5000)]
    public void DisposalIsRetriedAfterAHandlerThrows()
    {
        var derivation = new MonitoredDerivation();
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
    public void DisposalIsNotRepeatedAfterADisposedHandlerThrows()
    {
        var derivation = new MonitoredDerivation();
        derivation.Disposed += (sender, e) =>
            throw new InvalidOperationException();
        Assert.ThrowsException<InvalidOperationException>(derivation.Dispose);
        Assert.IsTrue(derivation.IsDisposed);
        derivation.Dispose();
        Assert.AreEqual(1, derivation.disposals);
    }

    [TestMethod]
    [Timeout(5000)]
    public void DisposalIsNotRepeatedAfterAPropertyChangedHandlerThrows()
    {
        var derivation = new MonitoredDerivation();
        derivation.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(derivation.IsDisposed))
                throw new InvalidOperationException();
        };
        Assert.ThrowsException<InvalidOperationException>(derivation.Dispose);
        Assert.IsTrue(derivation.IsDisposed);
        derivation.Dispose();
        Assert.AreEqual(1, derivation.disposals);
    }
}
