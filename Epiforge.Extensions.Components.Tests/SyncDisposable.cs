namespace Epiforge.Extensions.Components.Tests;

[TestClass]
public class SyncDisposable
{
    public class Derivation :
        Components.SyncDisposable
    {
        public Derivation(ILogger<Derivation> logger) =>
            Logger = logger;

        int referenceCount = 1;

        public void AddReference()
        {
            ThrowIfDisposed();
            ++referenceCount;
        }

        protected override bool Dispose(bool disposing)
        {
            if (disposing)
                return --referenceCount == 0;
            return true;
        }
    }

    [TestMethod]
    public void Dispose()
    {
        var disposalNotificationEvents = new List<(string name, DisposalNotificationEventArgs args)>();
        var logger = Substitute.For<MockLogger<Derivation>>();
        Derivation derivation;
        using (derivation = new Derivation(logger))
        {
            Assert.IsFalse(derivation.IsDisposed);
            derivation.Disposing += (_, e) => disposalNotificationEvents.Add((nameof(INotifyDisposing.Disposing), e));
            derivation.DisposalOverridden += (_, e) => disposalNotificationEvents.Add((nameof(INotifyDisposalOverridden.DisposalOverridden), e));
            derivation.Disposed += (_, e) => disposalNotificationEvents.Add((nameof(INotifyDisposed.Disposed), e));
            derivation.AddReference();
            derivation.Dispose();
            Assert.IsFalse(derivation.IsDisposed);
            logger.ReceivedLogTrace("Dispose called");
            logger.ReceivedLogTrace("Raising Disposing event");
            logger.ReceivedLogTrace("Raising DisposalOverridden event");
            logger.DidNotReceiveLogTrace("Raising Disposed event");
            logger.ClearReceivedCalls();
        }
        Assert.IsTrue(derivation.IsDisposed);
        logger.ReceivedLogTrace("Dispose called");
        logger.ReceivedLogTrace("Raising Disposing event");
        logger.DidNotReceiveLogTrace("Raising DisposalOverridden event");
        logger.ReceivedLogTrace("Raising Disposed event");
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
    public void ThrowIfDisposed()
    {
        var logger = Substitute.For<ILogger<Derivation>>();
        Derivation derivation;
        using (derivation = new Derivation(logger))
        {
        }
        derivation.AddReference();
    }
}
