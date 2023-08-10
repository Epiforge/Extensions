namespace Epiforge.Extensions.Components.Tests;

[TestClass]
public class DynamicAsyncDisposable
{
    public class Derivation :
        Components.DynamicAsyncDisposable
    {
        public Derivation(ILogger<Derivation> logger) =>
            Logger = logger;

        int referenceCount = 1;

        public void AddReference()
        {
            ThrowIfDisposed();
            ++referenceCount;
        }

        protected override ValueTask<bool> DisposeAsync(bool disposing)
        {
            if (disposing)
                return new(--referenceCount == 0);
            return new(true);
        }
    }

    [TestMethod]
    public async Task DisposeAsync()
    {
        var disposalNotificationEvents = new List<(string name, DisposalNotificationEventArgs args)>();
        var logger = Substitute.For<MockLogger<Derivation>>();
        Derivation derivation;
        await using (derivation = new Derivation(logger))
        {
            Assert.IsFalse(derivation.IsDisposed);
            derivation.Disposing += (_, e) => disposalNotificationEvents.Add((nameof(INotifyDisposing.Disposing), e));
            derivation.DisposalOverridden += (_, e) => disposalNotificationEvents.Add((nameof(INotifyDisposalOverridden.DisposalOverridden), e));
            derivation.Disposed += (_, e) => disposalNotificationEvents.Add((nameof(INotifyDisposed.Disposed), e));
            derivation.AddReference();
            await derivation.DisposeAsync();
            Assert.IsFalse(derivation.IsDisposed);
            logger.ReceivedLogDebug("DisposeAsync called");
            logger.ReceivedLogDebug($"Raising Disposing event");
            logger.ReceivedLogDebug($"Raising DisposalOverridden event");
            logger.DidNotReceiveLogDebug($"Raising Disposed event");
            logger.ClearReceivedCalls();
        }
        Assert.IsTrue(derivation.IsDisposed);
        logger.ReceivedLogDebug("DisposeAsync called");
        logger.ReceivedLogDebug($"Raising Disposing event");
        logger.DidNotReceiveLogDebug($"Raising DisposalOverridden event");
        logger.ReceivedLogDebug($"Raising Disposed event");
        logger.ClearReceivedCalls();
        Assert.AreEqual(nameof(INotifyDisposing.Disposing), disposalNotificationEvents[0].name);
        Assert.IsFalse(disposalNotificationEvents[0].args.IsFinalizer);
        Assert.AreEqual(nameof(INotifyDisposalOverridden.DisposalOverridden), disposalNotificationEvents[1].name);
        Assert.IsFalse(disposalNotificationEvents[1].args.IsFinalizer);
        Assert.AreEqual(nameof(INotifyDisposing.Disposing), disposalNotificationEvents[2].name);
        Assert.IsFalse(disposalNotificationEvents[2].args.IsFinalizer);
        Assert.AreEqual(nameof(INotifyDisposed.Disposed), disposalNotificationEvents[3].name);
        Assert.IsFalse(disposalNotificationEvents[3].args.IsFinalizer);
    }

    [TestMethod]
    [ExpectedException(typeof(ObjectDisposedException))]
    public async Task ThrowIfDisposedAsync()
    {
        var logger = Substitute.For<ILogger<Derivation>>();
        Derivation derivation;
        await using (derivation = new Derivation(logger))
        {
        }
        derivation.AddReference();
    }
}
