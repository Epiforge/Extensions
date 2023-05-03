namespace Epiforge.Extensions.Components.Tests;

[TestClass]
public class DynamicDisposable
{
    [ExcludeFromCodeCoverage]
    public class Derivation :
        Components.Disposable
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

        protected override ValueTask<bool> DisposeAsync(bool disposing)
        {
            if (disposing)
                return new(--referenceCount == 0);
            return new(true);
        }
    }

    [TestMethod]
    [ExcludeFromCodeCoverage]
    public void Dispose()
    {
        var disposalNotificationEvents = new List<(string name, DisposalNotificationEventArgs args)>();
        var mockLogger = new Mock<ILogger<Derivation>>();
        Derivation derivation;
        using (derivation = new Derivation(mockLogger.Object))
        {
            Assert.IsFalse(derivation.IsDisposed);
            derivation.Disposing += (_, e) => disposalNotificationEvents.Add((nameof(INotifyDisposing.Disposing), e));
            derivation.DisposalOverridden += (_, e) => disposalNotificationEvents.Add((nameof(INotifyDisposalOverridden.DisposalOverridden), e));
            derivation.Disposed += (_, e) => disposalNotificationEvents.Add((nameof(INotifyDisposed.Disposed), e));
            derivation.AddReference();
            derivation.Dispose();
            Assert.IsFalse(derivation.IsDisposed);
            mockLogger.VerifyLogDebug("Dispose called");
            mockLogger.VerifyLogDebug($"Raising Disposing event");
            mockLogger.VerifyLogDebug($"Raising DisposalOverridden event");
            mockLogger.VerifyLogDebug($"Raising Disposed event", Times.Never());
            mockLogger.Invocations.Clear();
        }
        Assert.IsTrue(derivation.IsDisposed);
        mockLogger.VerifyLogDebug("Dispose called");
        mockLogger.VerifyLogDebug($"Raising Disposing event");
        mockLogger.VerifyLogDebug($"Raising DisposalOverridden event", Times.Never());
        mockLogger.VerifyLogDebug($"Raising Disposed event");
        mockLogger.Invocations.Clear();
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
    [ExcludeFromCodeCoverage]
    public async Task DisposeAsync()
    {
        var disposalNotificationEvents = new List<(string name, DisposalNotificationEventArgs args)>();
        var mockLogger = new Mock<ILogger<Derivation>>();
        Derivation derivation;
        await using (derivation = new Derivation(mockLogger.Object))
        {
            Assert.IsFalse(derivation.IsDisposed);
            derivation.Disposing += (_, e) => disposalNotificationEvents.Add((nameof(INotifyDisposing.Disposing), e));
            derivation.DisposalOverridden += (_, e) => disposalNotificationEvents.Add((nameof(INotifyDisposalOverridden.DisposalOverridden), e));
            derivation.Disposed += (_, e) => disposalNotificationEvents.Add((nameof(INotifyDisposed.Disposed), e));
            derivation.AddReference();
            await derivation.DisposeAsync();
            Assert.IsFalse(derivation.IsDisposed);
            mockLogger.VerifyLogDebug("DisposeAsync called");
            mockLogger.VerifyLogDebug($"Raising Disposing event");
            mockLogger.VerifyLogDebug($"Raising DisposalOverridden event");
            mockLogger.VerifyLogDebug($"Raising Disposed event", Times.Never());
            mockLogger.Invocations.Clear();
        }
        Assert.IsTrue(derivation.IsDisposed);
        mockLogger.VerifyLogDebug("DisposeAsync called");
        mockLogger.VerifyLogDebug($"Raising Disposing event");
        mockLogger.VerifyLogDebug($"Raising DisposalOverridden event", Times.Never());
        mockLogger.VerifyLogDebug($"Raising Disposed event");
        mockLogger.Invocations.Clear();
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
    [ExcludeFromCodeCoverage]
    [ExpectedException(typeof(ObjectDisposedException))]
    public void ThrowIfDisposed()
    {
        var mockLogger = new Mock<ILogger<Derivation>>();
        Derivation derivation;
        using (derivation = new Derivation(mockLogger.Object))
        {
        }
        derivation.AddReference();
    }

    [TestMethod]
    [ExcludeFromCodeCoverage]
    [ExpectedException(typeof(ObjectDisposedException))]
    public async Task ThrowIfDisposedAsync()
    {
        var mockLogger = new Mock<ILogger<Derivation>>();
        Derivation derivation;
        await using (derivation = new Derivation(mockLogger.Object))
        {
        }
        derivation.AddReference();
    }
}
