namespace Epiforge.Extensions.Components.Tests;

[TestClass]
public class DisposalExtensions
{
    public class Dependency :
        Components.Disposable
    {
        protected override bool Dispose(bool disposing) =>
            true;

        protected override ValueTask<bool> DisposeAsync(bool disposing) =>
            new(true);
    }

    public class Dependent :
        Components.SyncDisposable
    {
        protected override bool Dispose(bool disposing) =>
            true;
    }

    [TestMethod]
    public void DisposeWhenDependentDisposedNoFactoryArguments()
    {
        var dependency = new Dependency();
        var dependent = dependency.DisposeWhenDependentDisposed(() => new Dependent());
        dependent.Dispose();
        Assert.IsTrue(dependency.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeWhenDependentDisposedNoFactoryArgumentsDeferredAsync()
    {
        var dependency = new Dependency();
        var dependent = dependency.DisposeWhenDependentDisposed(() => new Dependent(), TimeSpan.FromSeconds(0.25));
        dependent.Dispose();
        Assert.IsFalse(dependency.IsDisposed);
        await Task.Delay(500);
        Assert.IsTrue(dependency.IsDisposed);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void DisposeWhenDependentDisposedNoFactoryArgumentsDeferredInvalid()
    {
        var dependency = new Dependency();
        dependency.DisposeWhenDependentDisposed(() => new Dependent(), TimeSpan.FromSeconds(-0.25));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeWhenDependentDisposedNoFactoryArgumentsNullDependency() =>
        default(Dependency)!.DisposeWhenDependentDisposed(() => new Dependent());

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeWhenDependentDisposedNoFactoryArgumentsNullFactory() =>
        new Dependency().DisposeWhenDependentDisposed((Func<Dependent>)null!);

    [TestMethod]
    public void DisposeWhenDependentDisposed()
    {
        var dependency = new Dependency();
        var dependent = dependency.DisposeWhenDependentDisposed(_ => new Dependent());
        dependent.Dispose();
        Assert.IsTrue(dependency.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeWhenDependentDisposedDeferredAsync()
    {
        var dependency = new Dependency();
        var dependent = dependency.DisposeWhenDependentDisposed(_ => new Dependent(), TimeSpan.FromSeconds(0.25));
        dependent.Dispose();
        Assert.IsFalse(dependency.IsDisposed);
        await Task.Delay(500);
        Assert.IsTrue(dependency.IsDisposed);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void DisposeWhenDependentDisposedDeferredInvalid()
    {
        var dependency = new Dependency();
        dependency.DisposeWhenDependentDisposed(_ => new Dependent(), TimeSpan.FromSeconds(-0.25));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeWhenDependentDisposedNullDependency() =>
        default(Dependency)!.DisposeWhenDependentDisposed(_ => new Dependent());

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeWhenDependentDisposedNullFactory() =>
        new Dependency().DisposeWhenDependentDisposed((Func<Dependency, Dependent>)null!);

    [TestMethod]
    public async Task DisposeWhenDependentDisposedAsyncNoFactoryArgumentsAsync()
    {
        var dependency = new Dependency();
        var dependent = await dependency.DisposeWhenDependentDisposedAsync(() => Task.FromResult(new Dependent()));
        dependent.Dispose();
        Assert.IsTrue(dependency.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeWhenDependentDisposedAsyncNoFactoryArgumentsDeferredAsync()
    {
        var dependency = new Dependency();
        var dependent = await dependency.DisposeWhenDependentDisposedAsync(() => Task.FromResult(new Dependent()), TimeSpan.FromSeconds(0.25));
        dependent.Dispose();
        Assert.IsFalse(dependency.IsDisposed);
        await Task.Delay(500);
        Assert.IsTrue(dependency.IsDisposed);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public async Task DisposeWhenDependentDisposedAsyncNoFactoryArgumentsInvalidDeferredAsync()
    {
        var dependency = new Dependency();
        await dependency.DisposeWhenDependentDisposedAsync(() => Task.FromResult(new Dependent()), TimeSpan.FromSeconds(-0.25));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task DisposeWhenDependentDisposedAsyncNoFactoryArgumentsNullDependency() =>
        await default(Dependency)!.DisposeWhenDependentDisposedAsync(() => Task.FromResult(new Dependent()));

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task DisposeWhenDependentDisposedAsyncNoFactoryArgumentsNullFactory() =>
        await new Dependency().DisposeWhenDependentDisposedAsync((Func<Task<Dependent>>)null!);

    [TestMethod]
    public async Task DisposeWhenDependentDisposedAsync()
    {
        var dependency = new Dependency();
        var dependent = await dependency.DisposeWhenDependentDisposedAsync(_ => Task.FromResult(new Dependent()));
        dependent.Dispose();
        Assert.IsTrue(dependency.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeWhenDependentDisposedAsyncDeferredAsync()
    {
        var dependency = new Dependency();
        var dependent = await dependency.DisposeWhenDependentDisposedAsync(_ => Task.FromResult(new Dependent()), TimeSpan.FromSeconds(0.25));
        dependent.Dispose();
        Assert.IsFalse(dependency.IsDisposed);
        await Task.Delay(500);
        Assert.IsTrue(dependency.IsDisposed);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public async Task DisposeWhenDependentDisposedAsyncInvalidDeferredAsync()
    {
        var dependency = new Dependency();
        await dependency.DisposeWhenDependentDisposedAsync(_ => Task.FromResult(new Dependent()), TimeSpan.FromSeconds(-0.25));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task DisposeWhenDependentDisposedAsyncNullDependency() =>
        await default(Dependency)!.DisposeWhenDependentDisposedAsync(_ => Task.FromResult(new Dependent()));

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task DisposeWhenDependentDisposedAsyncNullFactory() =>
        await new Dependency().DisposeWhenDependentDisposedAsync((Func<Dependency, Task<Dependent>>)null!);

    [TestMethod]
    public void DisposeAsyncWhenDependentDisposedNoFactoryArguments()
    {
        var dependency = new Dependency();
        var dependent = dependency.DisposeAsyncWhenDependentDisposed(() => new Dependent());
        dependent.Dispose();
        while (!dependency.IsDisposed)
        {
        }
        Assert.IsTrue(dependency.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsyncWhenDependentDisposedNoFactoryArgumentsDeferredAsync()
    {
        var dependency = new Dependency();
        var dependent = dependency.DisposeAsyncWhenDependentDisposed(() => new Dependent(), TimeSpan.FromSeconds(0.25));
        dependent.Dispose();
        Assert.IsFalse(dependency.IsDisposed);
        await Task.Delay(500);
        Assert.IsTrue(dependency.IsDisposed);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public async Task DisposeAsyncWhenDependentDisposedNoFactoryArgumentsInvalidDeferredAsync()
    {
        var dependency = new Dependency();
        dependency.DisposeAsyncWhenDependentDisposed(() => new Dependent(), TimeSpan.FromSeconds(-0.25));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeAsyncWhenDependentDisposedNoFactoryArgumentsNullDependency() =>
        default(Dependency)!.DisposeAsyncWhenDependentDisposed(() => new Dependent());

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeAsyncWhenDependentDisposedNoFactoryArgumentsNullFactory() =>
        new Dependency().DisposeAsyncWhenDependentDisposed((Func<Dependent>)null!);

    [TestMethod]
    public void DisposeAsyncWhenDependentDisposed()
    {
        var dependency = new Dependency();
        var dependent = dependency.DisposeAsyncWhenDependentDisposed(_ => new Dependent());
        dependent.Dispose();
        while (!dependency.IsDisposed)
        {
        }
        Assert.IsTrue(dependency.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsyncWhenDependentDisposedDeferredAsync()
    {
        var dependency = new Dependency();
        var dependent = dependency.DisposeAsyncWhenDependentDisposed(_ => new Dependent(), TimeSpan.FromSeconds(0.25));
        dependent.Dispose();
        Assert.IsFalse(dependency.IsDisposed);
        await Task.Delay(500);
        Assert.IsTrue(dependency.IsDisposed);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public async Task DisposeAsyncWhenDependentDisposedDeferredInvalidAsync()
    {
        var dependency = new Dependency();
        dependency.DisposeAsyncWhenDependentDisposed(_ => new Dependent(), TimeSpan.FromSeconds(-0.25));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeAsyncWhenDependentDisposeNullDependency() =>
        default(Dependency)!.DisposeAsyncWhenDependentDisposed(_ => new Dependent());

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeAsyncWhenDependentDisposeNullFactory() =>
        new Dependency().DisposeAsyncWhenDependentDisposed((Func<Dependency, Dependent>)null!);

    [TestMethod]
    public async Task DisposeAsyncWhenDependentDisposedNoFactoryArgumentsAsync()
    {
        var dependency = new Dependency();
        var dependent = await dependency.DisposeAsyncWhenDependentDisposedAsync(() => Task.FromResult(new Dependent()));
        dependent.Dispose();
        while (!dependency.IsDisposed)
        {
        }
        Assert.IsTrue(dependency.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsyncWhenDependentDisposedNoFactoryArgumentsDeferredAsync2()
    {
        var dependency = new Dependency();
        var dependent = await dependency.DisposeAsyncWhenDependentDisposedAsync(() => Task.FromResult(new Dependent()), TimeSpan.FromSeconds(0.25));
        dependent.Dispose();
        Assert.IsFalse(dependency.IsDisposed);
        await Task.Delay(500);
        Assert.IsTrue(dependency.IsDisposed);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public async Task DisposeAsyncWhenDependentDisposedNoFactoryArgumentsInvalidDeferredAsync2()
    {
        var dependency = new Dependency();
        await dependency.DisposeAsyncWhenDependentDisposedAsync(() => Task.FromResult(new Dependent()), TimeSpan.FromSeconds(-0.25));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task DisposeAsyncWhenDependentDisposedNoFactoryArgumentsNullDependencyAsync() =>
        await default(Dependency)!.DisposeAsyncWhenDependentDisposedAsync(() => Task.FromResult(new Dependent()));

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task DisposeAsyncWhenDependentDisposedNoFactoryArgumentsNullFactoryAsync() =>
        await new Dependency().DisposeAsyncWhenDependentDisposedAsync((Func<Task<Dependent>>)null!);

    [TestMethod]
    public async Task DisposeAsyncWhenDependentDisposedAsync()
    {
        var dependency = new Dependency();
        var dependent = await dependency.DisposeAsyncWhenDependentDisposedAsync(_ => Task.FromResult(new Dependent()));
        dependent.Dispose();
        while (!dependency.IsDisposed)
        {
        }
        Assert.IsTrue(dependency.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsyncWhenDependentDisposedDeferredAsync2()
    {
        var dependency = new Dependency();
        var dependent = await dependency.DisposeAsyncWhenDependentDisposedAsync(_ => Task.FromResult(new Dependent()), TimeSpan.FromSeconds(0.25));
        dependent.Dispose();
        Assert.IsFalse(dependency.IsDisposed);
        await Task.Delay(500);
        Assert.IsTrue(dependency.IsDisposed);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public async Task DisposeAsyncWhenDependentDisposedInvalidDeferredAsync2()
    {
        var dependency = new Dependency();
        await dependency.DisposeAsyncWhenDependentDisposedAsync(_ => Task.FromResult(new Dependent()), TimeSpan.FromSeconds(-0.25));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task DisposeAsyncWhenDependentDisposeNullDependencyAsync() =>
        await default(Dependency)!.DisposeAsyncWhenDependentDisposedAsync(_ => Task.FromResult(new Dependent()));

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task DisposeAsyncWhenDependentDisposeNullFactoryAsync() =>
        await new Dependency().DisposeAsyncWhenDependentDisposedAsync((Func<Dependency, Task<Dependent>>)null!);

    [TestMethod]
    public void DisposeDependenciesWhenDisposed()
    {
        var dependency1 = new Dependency();
        var dependency2 = new Dependency();
        var dependent = new Dependent().DisposeDependenciesWhenDisposed(dependency1, dependency2);
        dependent.Dispose();
        Assert.IsTrue(dependency1.IsDisposed);
        Assert.IsTrue(dependency2.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeDependenciesWhenDisposedDeferredAsync()
    {
        var dependency1 = new Dependency();
        var dependency2 = new Dependency();
        var dependent = new Dependent().DisposeDependenciesWhenDisposed(TimeSpan.FromSeconds(0.25), dependency1, dependency2);
        dependent.Dispose();
        Assert.IsFalse(dependency1.IsDisposed);
        Assert.IsFalse(dependency2.IsDisposed);
        await Task.Delay(500);
        Assert.IsTrue(dependency1.IsDisposed);
        Assert.IsTrue(dependency2.IsDisposed);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public async Task DisposeDependenciesWhenDisposedInvalidDeferredAsync()
    {
        var dependency1 = new Dependency();
        var dependency2 = new Dependency();
        new Dependent().DisposeDependenciesWhenDisposed(TimeSpan.FromSeconds(-0.25), dependency1, dependency2);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeDependenciesWhenDisposedNullDependent() =>
        default(Dependent)!.DisposeDependenciesWhenDisposed(default(Dependency)!, default(Dependency)!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeDependenciesWhenDisposeNullDependencies() =>
        new Dependent().DisposeDependenciesWhenDisposed(null!);

    [TestMethod]
    public void DisposeAsyncDependenciesWhenDisposed()
    {
        var dependency1 = new Dependency();
        var dependency2 = new Dependency();
        var dependent = new Dependent().DisposeAsyncDependenciesWhenDisposed(dependency1, dependency2);
        dependent.Dispose();
        while (!dependency1.IsDisposed)
        {
        }
        Assert.IsTrue(dependency1.IsDisposed);
        Assert.IsTrue(dependency2.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsyncDependenciesWhenDisposedDeferred()
    {
        var dependency1 = new Dependency();
        var dependency2 = new Dependency();
        var dependent = new Dependent().DisposeAsyncDependenciesWhenDisposed(TimeSpan.FromSeconds(0.25), dependency1, dependency2);
        dependent.Dispose();
        Assert.IsFalse(dependency1.IsDisposed);
        Assert.IsFalse(dependency2.IsDisposed);
        await Task.Delay(500);
        Assert.IsTrue(dependency1.IsDisposed);
        Assert.IsTrue(dependency2.IsDisposed);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public async Task DisposeAsyncDependenciesWhenDisposedInvalidDeferred()
    {
        var dependency1 = new Dependency();
        var dependency2 = new Dependency();
        new Dependent().DisposeAsyncDependenciesWhenDisposed(TimeSpan.FromSeconds(-0.25), dependency1, dependency2);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeAsyncDependenciesWhenDisposedNullDependent() =>
        default(Dependent)!.DisposeAsyncDependenciesWhenDisposed(default(Dependency)!, default(Dependency)!);

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeAsyncDependenciesWhenDisposeNullDependencies() =>
        new Dependent().DisposeAsyncDependenciesWhenDisposed(null!);
}
