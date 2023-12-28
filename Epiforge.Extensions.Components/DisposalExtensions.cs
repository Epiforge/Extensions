namespace Epiforge.Extensions.Components;

/// <summary>
/// Provides extension methods for disposable types
/// </summary>
public static class DisposalExtensions
{
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
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(dependentFactory);
#else
        if (dependency is null)
            throw new ArgumentNullException(nameof(dependency));
        if (dependentFactory is null)
            throw new ArgumentNullException(nameof(dependentFactory));
#endif
        var dependent = dependentFactory();
        dependent.Disposed += (_, e) =>
        {
            if (!e.IsFinalizer)
                dependency.Dispose();
        };
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
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(dependentFactory);
#else
        if (dependency is null)
            throw new ArgumentNullException(nameof(dependency));
        if (dependentFactory is null)
            throw new ArgumentNullException(nameof(dependentFactory));
#endif
        var dependent = dependentFactory(dependency);
        dependent.Disposed += (_, e) =>
        {
            if (!e.IsFinalizer)
                dependency.Dispose();
        };
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
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(asyncDependentFactory);
#else
        if (dependency is null)
            throw new ArgumentNullException(nameof(dependency));
        if (asyncDependentFactory is null)
            throw new ArgumentNullException(nameof(asyncDependentFactory));
#endif
        var dependent = await asyncDependentFactory().ConfigureAwait(false);
        dependent.Disposed += (_, e) =>
        {
            if (!e.IsFinalizer)
                dependency.Dispose();
        };
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
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(asyncDependentFactory);
#else
        if (dependency is null)
            throw new ArgumentNullException(nameof(dependency));
        if (asyncDependentFactory is null)
            throw new ArgumentNullException(nameof(asyncDependentFactory));
#endif
        var dependent = await asyncDependentFactory(dependency).ConfigureAwait(false);
        dependent.Disposed += (_, e) =>
        {
            if (!e.IsFinalizer)
                dependency.Dispose();
        };
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
    public static TDependent DisposeAsyncWhenDependentDisposed<TDependency, TDependent>(this TDependency dependency, Func<TDependent> dependentFactory)
        where TDependency : IAsyncDisposable
        where TDependent : INotifyDisposed
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(dependentFactory);
#else
        if (dependency is null)
            throw new ArgumentNullException(nameof(dependency));
        if (dependentFactory is null)
            throw new ArgumentNullException(nameof(dependentFactory));
#endif
        var dependent = dependentFactory();
        dependent.Disposed += (_, e) => Task.Run(async () =>
        {
            if (!e.IsFinalizer)
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
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(dependentFactory);
#else
        if (dependency is null)
            throw new ArgumentNullException(nameof(dependency));
        if (dependentFactory is null)
            throw new ArgumentNullException(nameof(dependentFactory));
#endif
        var dependent = dependentFactory(dependency);
        dependent.Disposed += (_, e) => Task.Run(async () =>
        {
            if (!e.IsFinalizer)
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
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(asyncDependentFactory);
#else
        if (dependency is null)
            throw new ArgumentNullException(nameof(dependency));
        if (asyncDependentFactory is null)
            throw new ArgumentNullException(nameof(asyncDependentFactory));
#endif
        var dependent = await asyncDependentFactory().ConfigureAwait(false);
        dependent.Disposed += (_, e) => Task.Run(async () =>
        {
            if (!e.IsFinalizer)
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
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(asyncDependentFactory);
#else
        if (dependency is null)
            throw new ArgumentNullException(nameof(dependency));
        if (asyncDependentFactory is null)
            throw new ArgumentNullException(nameof(asyncDependentFactory));
#endif
        var dependent = await asyncDependentFactory(dependency).ConfigureAwait(false);
        dependent.Disposed += (_, e) => Task.Run(async () =>
        {
            if (!e.IsFinalizer)
                await dependency.DisposeAsync().ConfigureAwait(false);
        });
        return dependent;
    }
}
