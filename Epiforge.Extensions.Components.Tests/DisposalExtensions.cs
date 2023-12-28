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
        Components.Disposable
    {
        protected override bool Dispose(bool disposing) =>
            true;

        protected override ValueTask<bool> DisposeAsync(bool disposing) =>
            new(true);
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
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeWhenDependentDisposedNoFactoryArgumentsNullDependency()
    {
        Dependency? dependency = null;
        dependency!.DisposeWhenDependentDisposed(() => new Dependent());
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeWhenDependentDisposedNoFactoryArgumentsNullFactory()
    {
        var dependency = new Dependency();
        dependency.DisposeWhenDependentDisposed((Func<Dependent>)null!);
    }

    [TestMethod]
    public void DisposeWhenDependentDisposed()
    {
        var dependency = new Dependency();
        var dependent = dependency.DisposeWhenDependentDisposed(_ => new Dependent());
        dependent.Dispose();
        Assert.IsTrue(dependency.IsDisposed);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeWhenDependentDisposedNullDependency()
    {
        Dependency? dependency = null;
        dependency!.DisposeWhenDependentDisposed(_ => new Dependent());
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeWhenDependentDisposedNullFactory()
    {
        var dependency = new Dependency();
        dependency.DisposeWhenDependentDisposed((Func<Dependency, Dependent>)null!);
    }

    [TestMethod]
    public async Task DisposeWhenDependentDisposedAsyncNoFactoryArguments()
    {
        var dependency = new Dependency();
        var dependent = await dependency.DisposeWhenDependentDisposedAsync(() => Task.FromResult(new Dependent()));
        dependent.Dispose();
        Assert.IsTrue(dependency.IsDisposed);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task DisposeWhenDependentDisposedAsyncNoFactoryArgumentsNullDependency()
    {
        Dependency? dependency = null;
        await dependency!.DisposeWhenDependentDisposedAsync(() => Task.FromResult(new Dependent()));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task DisposeWhenDependentDisposedAsyncNoFactoryArgumentsNullFactory()
    {
        var dependency = new Dependency();
        await dependency.DisposeWhenDependentDisposedAsync((Func<Task<Dependent>>)null!);
    }

    [TestMethod]
    public async Task DisposeWhenDependentDisposedAsync()
    {
        var dependency = new Dependency();
        var dependent = await dependency.DisposeWhenDependentDisposedAsync(_ => Task.FromResult(new Dependent()));
        dependent.Dispose();
        Assert.IsTrue(dependency.IsDisposed);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task DisposeWhenDependentDisposedAsyncNullDependency()
    {
        Dependency? dependency = null;
        await dependency!.DisposeWhenDependentDisposedAsync(_ => Task.FromResult(new Dependent()));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task DisposeWhenDependentDisposedAsyncNullFactory()
    {
        var dependency = new Dependency();
        await dependency.DisposeWhenDependentDisposedAsync((Func<Dependency, Task<Dependent>>)null!);
    }

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
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeAsyncWhenDependentDisposedNoFactoryArgumentsNullDependency()
    {
        Dependency? dependency = null;
        dependency!.DisposeAsyncWhenDependentDisposed(() => new Dependent());
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeAsyncWhenDependentDisposedNoFactoryArgumentsNullFactory()
    {
        var dependency = new Dependency();
        dependency.DisposeAsyncWhenDependentDisposed((Func<Dependent>)null!);
    }

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
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeAsyncWhenDependentDisposeNullDependency()
    {
        Dependency? dependency = null;
        dependency!.DisposeAsyncWhenDependentDisposed(_ => new Dependent());
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void DisposeAsyncWhenDependentDisposeNullFactory()
    {
        var dependency = new Dependency();
        dependency.DisposeAsyncWhenDependentDisposed((Func<Dependency, Dependent>)null!);
    }

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
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task DisposeAsyncWhenDependentDisposedNoFactoryArgumentsNullDependencyAsync()
    {
        Dependency? dependency = null;
        await dependency!.DisposeAsyncWhenDependentDisposedAsync(() => Task.FromResult(new Dependent()));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task DisposeAsyncWhenDependentDisposedNoFactoryArgumentsNullFactoryAsync()
    {
        var dependency = new Dependency();
        await dependency.DisposeAsyncWhenDependentDisposedAsync((Func<Task<Dependent>>)null!);
    }

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
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task DisposeAsyncWhenDependentDisposeNullDependencyAsync()
    {
        Dependency? dependency = null;
        await dependency!.DisposeAsyncWhenDependentDisposedAsync(_ => Task.FromResult(new Dependent()));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public async Task DisposeAsyncWhenDependentDisposeNullFactoryAsync()
    {
        var dependency = new Dependency();
        await dependency.DisposeAsyncWhenDependentDisposedAsync((Func<Dependency, Task<Dependent>>)null!);
    }
}
