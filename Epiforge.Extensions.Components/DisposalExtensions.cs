namespace Epiforge.Extensions.Components;

/// <summary>
/// Provides extension methods for disposable types
/// </summary>
public static class DisposalExtensions
{
    /// <summary>
    /// Causes the <paramref name="dependent"/> to asynchronously dispose the <paramref name="dependencies"/> in reverse order when the <paramref name="dependent"/> is disposed
    /// </summary>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependent">The dependent which will asynchronously dispose the <paramref name="dependencies"/> in reverse order when it is disposed</param>
    /// <param name="dependencies">The dependencies which will be asynchronously disposed in reverse order by the <paramref name="dependent"/> when it is disposed</param>
    /// <returns>The <paramref name="dependent"/></returns>
    public static TDependent DisposeAsyncDependenciesWhenDisposed<TDependent>(this TDependent dependent, IEnumerable<IAsyncDisposable> dependencies)
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependent);
        ArgumentNullException.ThrowIfNull(dependencies);
        dependent.Disposed += (_, e) => Task.Run(async () =>
        {
            foreach (var dependency in dependencies.Reverse())
                await dependency.DisposeAsync().ConfigureAwait(false);
        });
        return dependent;
    }

    /// <summary>
    /// Causes the <paramref name="dependent"/> to asynchronously dispose the <paramref name="dependencies"/> in reverse order when the <paramref name="dependent"/> is disposed
    /// </summary>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependent">The dependent which will asynchronously dispose the <paramref name="dependencies"/> in reverse order when it is disposed</param>
    /// <param name="disposalDelay">The amount of time to wait after the dependent is disposed before <see cref="IAsyncDisposable.DisposeAsync"/> is called on <paramref name="dependencies"/></param>
    /// <param name="dependencies">The dependencies which will be asynchronously disposed in reverse order by the <paramref name="dependent"/> when it is disposed</param>
    /// <returns>The <paramref name="dependent"/></returns>
    public static TDependent DisposeAsyncDependenciesWhenDisposed<TDependent>(this TDependent dependent, TimeSpan disposalDelay, IEnumerable<IAsyncDisposable> dependencies)
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependent);
        ArgumentNullException.ThrowIfNull(dependencies);
#if IS_NET_8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(disposalDelay, TimeSpan.Zero);
#else
        if (disposalDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(disposalDelay), disposalDelay, $"{nameof(disposalDelay)} must be greater than zero");
#endif
        dependent.Disposed += (_, e) => Task.Run(async () =>
        {
            await Task.Delay(disposalDelay).ConfigureAwait(false);
            foreach (var dependency in dependencies.Reverse())
                await dependency.DisposeAsync().ConfigureAwait(false);
        });
        return dependent;
    }

    /// <summary>
    /// Causes the <paramref name="dependent"/> to asynchronously dispose the <paramref name="dependencies"/> in reverse order when the <paramref name="dependent"/> is disposed
    /// </summary>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependent">The dependent which will asynchronously dispose the <paramref name="dependencies"/> in reverse order when it is disposed</param>
    /// <param name="dependencies">The dependencies which will be asynchronously disposed in reverse order by the <paramref name="dependent"/> when it is disposed</param>
    /// <returns>The <paramref name="dependent"/></returns>
    public static TDependent DisposeAsyncDependenciesWhenDisposed<TDependent>(this TDependent dependent, params IAsyncDisposable[] dependencies)
        where TDependent : INotifyDisposed =>
        DisposeAsyncDependenciesWhenDisposed(dependent, (IEnumerable<IAsyncDisposable>)dependencies);

    /// <summary>
    /// Causes the <paramref name="dependent"/> to asynchronously dispose the <paramref name="dependencies"/> in reverse order when the <paramref name="dependent"/> is disposed
    /// </summary>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependent">The dependent which will asynchronously dispose the <paramref name="dependencies"/> in reverse order when it is disposed</param>
    /// <param name="disposalDelay">The amount of time to wait after the dependent is disposed before <see cref="IAsyncDisposable.DisposeAsync"/> is called on <paramref name="dependencies"/></param>
    /// <param name="dependencies">The dependencies which will be asynchronously disposed in reverse order by the <paramref name="dependent"/> when it is disposed</param>
    /// <returns>The <paramref name="dependent"/></returns>
    public static TDependent DisposeAsyncDependenciesWhenDisposed<TDependent>(this TDependent dependent, TimeSpan disposalDelay, params IAsyncDisposable[] dependencies)
        where TDependent : INotifyDisposed =>
        DisposeAsyncDependenciesWhenDisposed(dependent, disposalDelay, (IEnumerable<IAsyncDisposable>)dependencies);

    /// <summary>
    /// Returns a <typeparamref name="TDependent"/> (which was the result of calling <paramref name="dependentFactory"/>) which will cause <paramref name="dependency"/> to be disposed asynchrononously when the <typeparamref name="TDependent"/> is disposed
    /// Note: Dependents which are finalized but not disposed will not cause the dependency to be disposed
    /// </summary>
    /// <typeparam name="TDependency">The type of the dependency</typeparam>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependency">The dependency which will be disposed when the result of calling <paramref name="dependentFactory"/> is disposed</param>
    /// <param name="dependentFactory">A function the result of which, when disposed, will cause <paramref name="dependency"/> to be disposed</param>
    /// <returns>The result of calling <paramref name="dependentFactory"/></returns>
    public static TDependent DisposeAsyncWhenDependentDisposed<TDependency, TDependent>(this TDependency dependency, Func<TDependent> dependentFactory)
        where TDependency : IAsyncDisposable
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(dependentFactory);
        var dependent = dependentFactory();
        dependent.Disposed += (_, e) => Task.Run(async () => await dependency.DisposeAsync().ConfigureAwait(false));
        return dependent;
    }

    /// <summary>
    /// Returns a <typeparamref name="TDependent"/> (which was the result of calling <paramref name="dependentFactory"/>) which will cause <paramref name="dependency"/> to be disposed asynchrononously when the <typeparamref name="TDependent"/> is disposed
    /// Note: Dependents which are finalized but not disposed will not cause the dependency to be disposed
    /// </summary>
    /// <typeparam name="TDependency">The type of the dependency</typeparam>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependency">The dependency which will be disposed when the result of calling <paramref name="dependentFactory"/> is disposed</param>
    /// <param name="dependentFactory">A function the result of which, when disposed, will cause <paramref name="dependency"/> to be disposed</param>
    /// <param name="disposalDelay">The amount of time to wait after the dependent is disposed before <see cref="IAsyncDisposable.DisposeAsync"/> is called on the <paramref name="dependency"/></param>
    /// <returns>The result of calling <paramref name="dependentFactory"/></returns>
    public static TDependent DisposeAsyncWhenDependentDisposed<TDependency, TDependent>(this TDependency dependency, Func<TDependent> dependentFactory, TimeSpan disposalDelay)
        where TDependency : IAsyncDisposable
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(dependentFactory);
#if IS_NET_8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(disposalDelay, TimeSpan.Zero);
#else
        if (disposalDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(disposalDelay), disposalDelay, $"{nameof(disposalDelay)} must be greater than zero");
#endif
        var dependent = dependentFactory();
        dependent.Disposed += (_, e) => Task.Run(async () =>
        {
            await Task.Delay(disposalDelay).ConfigureAwait(false);
            await dependency.DisposeAsync().ConfigureAwait(false);
        });
        return dependent;
    }

    /// <summary>
    /// Returns a <typeparamref name="TDependent"/> (which was the result of calling <paramref name="dependentFactory"/>) which will cause <paramref name="dependency"/> to be disposed asynchrononously when the <typeparamref name="TDependent"/> is disposed
    /// Note: Dependents which are finalized but not disposed will not cause the dependency to be disposed
    /// </summary>
    /// <typeparam name="TDependency">The type of the dependency</typeparam>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependency">The dependency which will be disposed when the result of calling <paramref name="dependentFactory"/> is disposed</param>
    /// <param name="dependentFactory">A function the result of which, when disposed, will cause <paramref name="dependency"/> to be disposed</param>
    /// <returns>The result of calling <paramref name="dependentFactory"/></returns>
    public static TDependent DisposeAsyncWhenDependentDisposed<TDependency, TDependent>(this TDependency dependency, Func<TDependency, TDependent> dependentFactory)
        where TDependency : IAsyncDisposable
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(dependentFactory);
        var dependent = dependentFactory(dependency);
        dependent.Disposed += (_, e) => Task.Run(async () => await dependency.DisposeAsync().ConfigureAwait(false));
        return dependent;
    }

    /// <summary>
    /// Returns a <typeparamref name="TDependent"/> (which was the result of calling <paramref name="dependentFactory"/>) which will cause <paramref name="dependency"/> to be disposed asynchrononously when the <typeparamref name="TDependent"/> is disposed
    /// Note: Dependents which are finalized but not disposed will not cause the dependency to be disposed
    /// </summary>
    /// <typeparam name="TDependency">The type of the dependency</typeparam>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependency">The dependency which will be disposed when the result of calling <paramref name="dependentFactory"/> is disposed</param>
    /// <param name="dependentFactory">A function the result of which, when disposed, will cause <paramref name="dependency"/> to be disposed</param>
    /// <param name="disposalDelay">The amount of time to wait after the dependent is disposed before <see cref="IAsyncDisposable.DisposeAsync"/> is called on the <paramref name="dependency"/></param>
    /// <returns>The result of calling <paramref name="dependentFactory"/></returns>
    public static TDependent DisposeAsyncWhenDependentDisposed<TDependency, TDependent>(this TDependency dependency, Func<TDependency, TDependent> dependentFactory, TimeSpan disposalDelay)
        where TDependency : IAsyncDisposable
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(dependentFactory);
#if IS_NET_8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(disposalDelay, TimeSpan.Zero);
#else
        if (disposalDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(disposalDelay), disposalDelay, $"{nameof(disposalDelay)} must be greater than zero");
#endif
        var dependent = dependentFactory(dependency);
        dependent.Disposed += (_, e) => Task.Run(async () =>
        {
            await Task.Delay(disposalDelay).ConfigureAwait(false);
            await dependency.DisposeAsync().ConfigureAwait(false);
        });
        return dependent;
    }

    /// <summary>
    /// Returns a <typeparamref name="TDependent"/> (which was the result of awaiting <paramref name="asyncDependentFactory"/>) which will cause <paramref name="dependency"/> to be disposed asynchrononously when the <typeparamref name="TDependent"/> is disposed
    /// Note: Dependents which are finalized but not disposed will not cause the dependency to be disposed
    /// </summary>
    /// <typeparam name="TDependency">The type of the dependency</typeparam>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependency">The dependency which will be disposed asynchrononously when the result of awaiting <paramref name="asyncDependentFactory"/> is disposed</param>
    /// <param name="asyncDependentFactory">An asynchronous function the awaited result of which, when disposed, will cause <paramref name="dependency"/> to be disposed asynchrononously</param>
    /// <returns>The result of awaiting <paramref name="asyncDependentFactory"/></returns>
    public static async Task<TDependent> DisposeAsyncWhenDependentDisposedAsync<TDependency, TDependent>(this TDependency dependency, Func<Task<TDependent>> asyncDependentFactory)
        where TDependency : IAsyncDisposable
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(asyncDependentFactory);
        var dependent = await asyncDependentFactory().ConfigureAwait(false);
        dependent.Disposed += (_, e) => Task.Run(async () => await dependency.DisposeAsync().ConfigureAwait(false));
        return dependent;
    }

    /// <summary>
    /// Returns a <typeparamref name="TDependent"/> (which was the result of awaiting <paramref name="asyncDependentFactory"/>) which will cause <paramref name="dependency"/> to be disposed asynchrononously when the <typeparamref name="TDependent"/> is disposed
    /// Note: Dependents which are finalized but not disposed will not cause the dependency to be disposed
    /// </summary>
    /// <typeparam name="TDependency">The type of the dependency</typeparam>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependency">The dependency which will be disposed asynchrononously when the result of awaiting <paramref name="asyncDependentFactory"/> is disposed</param>
    /// <param name="asyncDependentFactory">An asynchronous function the awaited result of which, when disposed, will cause <paramref name="dependency"/> to be disposed asynchrononously</param>
    /// <param name="disposalDelay">The amount of time to wait after the dependent is disposed before <see cref="IAsyncDisposable.DisposeAsync"/> is called on the <paramref name="dependency"/></param>
    /// <returns>The result of awaiting <paramref name="asyncDependentFactory"/></returns>
    public static async Task<TDependent> DisposeAsyncWhenDependentDisposedAsync<TDependency, TDependent>(this TDependency dependency, Func<Task<TDependent>> asyncDependentFactory, TimeSpan disposalDelay)
        where TDependency : IAsyncDisposable
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(asyncDependentFactory);
#if IS_NET_8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(disposalDelay, TimeSpan.Zero);
#else
        if (disposalDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(disposalDelay), disposalDelay, $"{nameof(disposalDelay)} must be greater than zero");
#endif
        var dependent = await asyncDependentFactory().ConfigureAwait(false);
        dependent.Disposed += (_, e) => Task.Run(async () =>
        {
            await Task.Delay(disposalDelay).ConfigureAwait(false);
            await dependency.DisposeAsync().ConfigureAwait(false);
        });
        return dependent;
    }

    /// <summary>
    /// Returns a <typeparamref name="TDependent"/> (which was the result of awaiting <paramref name="asyncDependentFactory"/>) which will cause <paramref name="dependency"/> to be disposed asynchrononously when the <typeparamref name="TDependent"/> is disposed
    /// Note: Dependents which are finalized but not disposed will not cause the dependency to be disposed
    /// </summary>
    /// <typeparam name="TDependency">The type of the dependency</typeparam>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependency">The dependency which will be disposed asynchrononously when the result of awaiting <paramref name="asyncDependentFactory"/> is disposed</param>
    /// <param name="asyncDependentFactory">An asynchronous function the awaited result of which, when disposed, will cause <paramref name="dependency"/> to be disposed asynchrononously</param>
    /// <returns>The result of awaiting <paramref name="asyncDependentFactory"/></returns>
    public static async Task<TDependent> DisposeAsyncWhenDependentDisposedAsync<TDependency, TDependent>(this TDependency dependency, Func<TDependency, Task<TDependent>> asyncDependentFactory)
        where TDependency : IAsyncDisposable
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(asyncDependentFactory);
        var dependent = await asyncDependentFactory(dependency).ConfigureAwait(false);
        dependent.Disposed += (_, e) => Task.Run(async () => await dependency.DisposeAsync().ConfigureAwait(false));
        return dependent;
    }

    /// <summary>
    /// Returns a <typeparamref name="TDependent"/> (which was the result of awaiting <paramref name="asyncDependentFactory"/>) which will cause <paramref name="dependency"/> to be disposed asynchrononously when the <typeparamref name="TDependent"/> is disposed
    /// Note: Dependents which are finalized but not disposed will not cause the dependency to be disposed
    /// </summary>
    /// <typeparam name="TDependency">The type of the dependency</typeparam>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependency">The dependency which will be disposed asynchrononously when the result of awaiting <paramref name="asyncDependentFactory"/> is disposed</param>
    /// <param name="asyncDependentFactory">An asynchronous function the awaited result of which, when disposed, will cause <paramref name="dependency"/> to be disposed asynchrononously</param>
    /// <param name="disposalDelay">The amount of time to wait after the dependent is disposed before <see cref="IAsyncDisposable.DisposeAsync"/> is called on the <paramref name="dependency"/></param>
    /// <returns>The result of awaiting <paramref name="asyncDependentFactory"/></returns>
    public static async Task<TDependent> DisposeAsyncWhenDependentDisposedAsync<TDependency, TDependent>(this TDependency dependency, Func<TDependency, Task<TDependent>> asyncDependentFactory, TimeSpan disposalDelay)
        where TDependency : IAsyncDisposable
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(asyncDependentFactory);
#if IS_NET_8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(disposalDelay, TimeSpan.Zero);
#else
        if (disposalDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(disposalDelay), disposalDelay, $"{nameof(disposalDelay)} must be greater than zero");
#endif
        var dependent = await asyncDependentFactory(dependency).ConfigureAwait(false);
        dependent.Disposed += (_, e) => Task.Run(async () =>
        {
            await Task.Delay(disposalDelay).ConfigureAwait(false);
            await dependency.DisposeAsync().ConfigureAwait(false);
        });
        return dependent;
    }

    /// <summary>
    /// Causes the <paramref name="dependent"/> to dispose the <paramref name="dependencies"/> in reverse order when the <paramref name="dependent"/> is disposed
    /// </summary>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependent">The dependent which will dispose the <paramref name="dependencies"/> in reverse order when it is disposed</param>
    /// <param name="dependencies">The dependencies which will be disposed in reverse order by the <paramref name="dependent"/> when it is disposed</param>
    /// <returns>The <paramref name="dependent"/></returns>
    public static TDependent DisposeDependenciesWhenDisposed<TDependent>(this TDependent dependent, IEnumerable<IDisposable> dependencies)
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependent);
        ArgumentNullException.ThrowIfNull(dependencies);
        dependent.Disposed += (_, e) =>
        {
            foreach (var dependency in dependencies.Reverse())
                dependency.Dispose();
        };
        return dependent;
    }

    /// <summary>
    /// Causes the <paramref name="dependent"/> to dispose the <paramref name="dependencies"/> in reverse order when the <paramref name="dependent"/> is disposed
    /// </summary>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependent">The dependent which will dispose the <paramref name="dependencies"/> in reverse order when it is disposed</param>
    /// <param name="disposalDelay">The amount of time to wait after the dependent is disposed before <see cref="IDisposable.Dispose"/> is called on the <paramref name="dependencies"/></param>
    /// <param name="dependencies">The dependencies which will be disposed in reverse order by the <paramref name="dependent"/> when it is disposed</param>
    /// <returns>The <paramref name="dependent"/></returns>
    public static TDependent DisposeDependenciesWhenDisposed<TDependent>(this TDependent dependent, TimeSpan disposalDelay, IEnumerable<IDisposable> dependencies)
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependent);
        ArgumentNullException.ThrowIfNull(dependencies);
#if IS_NET_8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(disposalDelay, TimeSpan.Zero);
#else
        if (disposalDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(disposalDelay), disposalDelay, $"{nameof(disposalDelay)} must be greater than zero");
#endif
        dependent.Disposed += async (_, e) =>
        {
            await Task.Delay(disposalDelay).ConfigureAwait(false);
            foreach (var dependency in dependencies.Reverse())
                dependency.Dispose();
        };
        return dependent;
    }

    /// <summary>
    /// Causes the <paramref name="dependent"/> to dispose the <paramref name="dependencies"/> in reverse order when the <paramref name="dependent"/> is disposed
    /// </summary>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependent">The dependent which will dispose the <paramref name="dependencies"/> in reverse order when it is disposed</param>
    /// <param name="dependencies">The dependencies which will be disposed in reverse order by the <paramref name="dependent"/> when it is disposed</param>
    /// <returns>The <paramref name="dependent"/></returns>
    public static TDependent DisposeDependenciesWhenDisposed<TDependent>(this TDependent dependent, params IDisposable[] dependencies)
        where TDependent : INotifyDisposed =>
        DisposeDependenciesWhenDisposed(dependent, (IEnumerable<IDisposable>)dependencies);

    /// <summary>
    /// Causes the <paramref name="dependent"/> to dispose the <paramref name="dependencies"/> in reverse order when the <paramref name="dependent"/> is disposed
    /// </summary>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependent">The dependent which will dispose the <paramref name="dependencies"/> in reverse order when it is disposed</param>
    /// <param name="disposalDelay">The amount of time to wait after the dependent is disposed before <see cref="IDisposable.Dispose"/> is called on the <paramref name="dependencies"/></param>
    /// <param name="dependencies">The dependencies which will be disposed in reverse order by the <paramref name="dependent"/> when it is disposed</param>
    /// <returns>The <paramref name="dependent"/></returns>
    public static TDependent DisposeDependenciesWhenDisposed<TDependent>(this TDependent dependent, TimeSpan disposalDelay, params IDisposable[] dependencies)
        where TDependent : INotifyDisposed =>
        DisposeDependenciesWhenDisposed(dependent, disposalDelay, (IEnumerable<IDisposable>)dependencies);

    /// <summary>
    /// Returns a <typeparamref name="TDependent"/> (which was the result of calling <paramref name="dependentFactory"/>) which will cause <paramref name="dependency"/> to be disposed when the <typeparamref name="TDependent"/> is disposed<br />
    /// Note: Dependents which are finalized but not disposed will not cause the dependency to be disposed
    /// </summary>
    /// <typeparam name="TDependency">The type of the dependency</typeparam>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependency">The dependency which will be disposed when the result of calling <paramref name="dependentFactory"/> is disposed</param>
    /// <param name="dependentFactory">A function the result of which, when disposed, will cause <paramref name="dependency"/> to be disposed</param>
    /// <returns>The result of calling <paramref name="dependentFactory"/></returns>
    public static TDependent DisposeWhenDependentDisposed<TDependency, TDependent>(this TDependency dependency, Func<TDependent> dependentFactory)
        where TDependency : IDisposable
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(dependentFactory);
        var dependent = dependentFactory();
        dependent.Disposed += (_, e) => dependency.Dispose();
        return dependent;
    }

    /// <summary>
    /// Returns a <typeparamref name="TDependent"/> (which was the result of calling <paramref name="dependentFactory"/>) which will cause <paramref name="dependency"/> to be disposed when the <typeparamref name="TDependent"/> is disposed<br />
    /// Note: Dependents which are finalized but not disposed will not cause the dependency to be disposed
    /// </summary>
    /// <typeparam name="TDependency">The type of the dependency</typeparam>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependency">The dependency which will be disposed when the result of calling <paramref name="dependentFactory"/> is disposed</param>
    /// <param name="dependentFactory">A function the result of which, when disposed, will cause <paramref name="dependency"/> to be disposed</param>
    /// <param name="disposalDelay">The amount of time to wait after the dependent is disposed before <see cref="IDisposable.Dispose"/> is called on <paramref name="dependency"/></param>
    /// <returns>The result of calling <paramref name="dependentFactory"/></returns>
    public static TDependent DisposeWhenDependentDisposed<TDependency, TDependent>(this TDependency dependency, Func<TDependent> dependentFactory, TimeSpan disposalDelay)
        where TDependency : IDisposable
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(dependentFactory);
#if IS_NET_8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(disposalDelay, TimeSpan.Zero);
#else
        if (disposalDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(disposalDelay), disposalDelay, $"{nameof(disposalDelay)} must be greater than zero");
#endif
        var dependent = dependentFactory();
        dependent.Disposed += (_, e) => Task.Run(async () =>
        {
            await Task.Delay(disposalDelay).ConfigureAwait(false);
            dependency.Dispose();
        });
        return dependent;
    }

    /// <summary>
    /// Returns a <typeparamref name="TDependent"/> (which was the result of calling <paramref name="dependentFactory"/>) which will cause <paramref name="dependency"/> to be disposed when the <typeparamref name="TDependent"/> is disposed
    /// Note: Dependents which are finalized but not disposed will not cause the dependency to be disposed
    /// </summary>
    /// <typeparam name="TDependency">The type of the dependency</typeparam>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependency">The dependency which will be disposed when the result of calling <paramref name="dependentFactory"/> is disposed</param>
    /// <param name="dependentFactory">A function the result of which, when disposed, will cause <paramref name="dependency"/> to be disposed</param>
    /// <returns>The result of calling <paramref name="dependentFactory"/></returns>
    public static TDependent DisposeWhenDependentDisposed<TDependency, TDependent>(this TDependency dependency, Func<TDependency, TDependent> dependentFactory)
        where TDependency : IDisposable
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(dependentFactory);
        var dependent = dependentFactory(dependency);
        dependent.Disposed += (_, e) => dependency.Dispose();
        return dependent;
    }

    /// <summary>
    /// Returns a <typeparamref name="TDependent"/> (which was the result of calling <paramref name="dependentFactory"/>) which will cause <paramref name="dependency"/> to be disposed when the <typeparamref name="TDependent"/> is disposed
    /// Note: Dependents which are finalized but not disposed will not cause the dependency to be disposed
    /// </summary>
    /// <typeparam name="TDependency">The type of the dependency</typeparam>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependency">The dependency which will be disposed when the result of calling <paramref name="dependentFactory"/> is disposed</param>
    /// <param name="dependentFactory">A function the result of which, when disposed, will cause <paramref name="dependency"/> to be disposed</param>
    /// <param name="disposalDelay">The amount of time to wait after the dependent is disposed before <see cref="IDisposable.Dispose"/> is called on <paramref name="dependency"/></param>
    /// <returns>The result of calling <paramref name="dependentFactory"/></returns>
    public static TDependent DisposeWhenDependentDisposed<TDependency, TDependent>(this TDependency dependency, Func<TDependency, TDependent> dependentFactory, TimeSpan disposalDelay)
        where TDependency : IDisposable
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(dependentFactory);
#if IS_NET_8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(disposalDelay, TimeSpan.Zero);
#else
        if (disposalDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(disposalDelay), disposalDelay, $"{nameof(disposalDelay)} must be greater than zero");
#endif
        var dependent = dependentFactory(dependency);
        dependent.Disposed += (_, e) => Task.Run(async () =>
        {
            await Task.Delay(disposalDelay).ConfigureAwait(false);
            dependency.Dispose();
        });
        return dependent;
    }

    /// <summary>
    /// Returns a <typeparamref name="TDependent"/> (which was the result of awaiting <paramref name="asyncDependentFactory"/>) which will cause <paramref name="dependency"/> to be disposed when the <typeparamref name="TDependent"/> is disposed
    /// Note: Dependents which are finalized but not disposed will not cause the dependency to be disposed
    /// </summary>
    /// <typeparam name="TDependency">The type of the dependency</typeparam>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependency">The dependency which will be disposed when the result of awaiting <paramref name="asyncDependentFactory"/> is disposed</param>
    /// <param name="asyncDependentFactory">An asynchronous function the result of which, when disposed, will cause <paramref name="dependency"/> to be disposed</param>
    /// <returns>The result of awaiting <paramref name="asyncDependentFactory"/></returns>
    public static async Task<TDependent> DisposeWhenDependentDisposedAsync<TDependency, TDependent>(this TDependency dependency, Func<Task<TDependent>> asyncDependentFactory)
        where TDependency : IDisposable
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(asyncDependentFactory);
        var dependent = await asyncDependentFactory().ConfigureAwait(false);
        dependent.Disposed += (_, e) => dependency.Dispose();
        return dependent;
    }

    /// <summary>
    /// Returns a <typeparamref name="TDependent"/> (which was the result of awaiting <paramref name="asyncDependentFactory"/>) which will cause <paramref name="dependency"/> to be disposed when the <typeparamref name="TDependent"/> is disposed
    /// Note: Dependents which are finalized but not disposed will not cause the dependency to be disposed
    /// </summary>
    /// <typeparam name="TDependency">The type of the dependency</typeparam>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependency">The dependency which will be disposed when the result of awaiting <paramref name="asyncDependentFactory"/> is disposed</param>
    /// <param name="asyncDependentFactory">An asynchronous function the result of which, when disposed, will cause <paramref name="dependency"/> to be disposed</param>
    /// <param name="disposalDelay">The amount of time to wait after the dependent is disposed before <see cref="IDisposable.Dispose"/> is called on <paramref name="dependency"/></param>
    /// <returns>The result of awaiting <paramref name="asyncDependentFactory"/></returns>
    public static async Task<TDependent> DisposeWhenDependentDisposedAsync<TDependency, TDependent>(this TDependency dependency, Func<Task<TDependent>> asyncDependentFactory, TimeSpan disposalDelay)
        where TDependency : IDisposable
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(asyncDependentFactory);
#if IS_NET_8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(disposalDelay, TimeSpan.Zero);
#else
        if (disposalDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(disposalDelay), disposalDelay, $"{nameof(disposalDelay)} must be greater than zero");
#endif
        var dependent = await asyncDependentFactory().ConfigureAwait(false);
        dependent.Disposed += (_, e) => Task.Run(async () =>
        {
            await Task.Delay(disposalDelay).ConfigureAwait(false);
            dependency.Dispose();
        });
        return dependent;
    }

    /// <summary>
    /// Returns a <typeparamref name="TDependent"/> (which was the result of awaiting <paramref name="asyncDependentFactory"/>) which will cause <paramref name="dependency"/> to be disposed when the <typeparamref name="TDependent"/> is disposed
    /// Note: Dependents which are finalized but not disposed will not cause the dependency to be disposed
    /// </summary>
    /// <typeparam name="TDependency">The type of the dependency</typeparam>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependency">The dependency which will be disposed when the result of awaiting <paramref name="asyncDependentFactory"/> is disposed</param>
    /// <param name="asyncDependentFactory">An asynchronous function the result of which, when disposed, will cause <paramref name="dependency"/> to be disposed</param>
    /// <returns>The result of awaiting <paramref name="asyncDependentFactory"/></returns>
    public static async Task<TDependent> DisposeWhenDependentDisposedAsync<TDependency, TDependent>(this TDependency dependency, Func<TDependency, Task<TDependent>> asyncDependentFactory)
        where TDependency : IDisposable
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(asyncDependentFactory);
        var dependent = await asyncDependentFactory(dependency).ConfigureAwait(false);
        dependent.Disposed += (_, e) => dependency.Dispose();
        return dependent;
    }

    /// <summary>
    /// Returns a <typeparamref name="TDependent"/> (which was the result of awaiting <paramref name="asyncDependentFactory"/>) which will cause <paramref name="dependency"/> to be disposed when the <typeparamref name="TDependent"/> is disposed
    /// Note: Dependents which are finalized but not disposed will not cause the dependency to be disposed
    /// </summary>
    /// <typeparam name="TDependency">The type of the dependency</typeparam>
    /// <typeparam name="TDependent">The type of the dependent</typeparam>
    /// <param name="dependency">The dependency which will be disposed when the result of awaiting <paramref name="asyncDependentFactory"/> is disposed</param>
    /// <param name="asyncDependentFactory">An asynchronous function the result of which, when disposed, will cause <paramref name="dependency"/> to be disposed</param>
    /// <param name="disposalDelay">The amount of time to wait after the dependent is disposed before <see cref="IDisposable.Dispose"/> is called on <paramref name="dependency"/></param>
    /// <returns>The result of awaiting <paramref name="asyncDependentFactory"/></returns>
    public static async Task<TDependent> DisposeWhenDependentDisposedAsync<TDependency, TDependent>(this TDependency dependency, Func<TDependency, Task<TDependent>> asyncDependentFactory, TimeSpan disposalDelay)
        where TDependency : IDisposable
        where TDependent : INotifyDisposed
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(asyncDependentFactory);
#if IS_NET_8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(disposalDelay, TimeSpan.Zero);
#else
        if (disposalDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(disposalDelay), disposalDelay, $"{nameof(disposalDelay)} must be greater than zero");
#endif
        var dependent = await asyncDependentFactory(dependency).ConfigureAwait(false);
        dependent.Disposed += (_, e) => Task.Run(async () =>
        {
            await Task.Delay(disposalDelay).ConfigureAwait(false);
            dependency.Dispose();
        });
        return dependent;
    }
}
