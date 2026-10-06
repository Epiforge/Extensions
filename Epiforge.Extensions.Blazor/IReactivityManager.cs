namespace Epiforge.Extensions.Blazor;

/// <summary>
/// Represents what observes values on behalf of a reactive component and tells it when they change
/// </summary>
public interface IReactivityManager :
    IDisposable
{
    /// <summary>
    /// Observes changes to the value represented by an expression and returns an <see cref="IObservedBinding{T}"/> through which the value can be read and written
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="valueAccessor">An expression reading a chain of members from a constant</param>
    /// <exception cref="ArgumentException"><paramref name="valueAccessor"/> is not a chain of members read from a constant, or some part of it is null</exception>
    /// <exception cref="InvalidOperationException">The reactivity manager is not initialized</exception>
    /// <exception cref="ObjectDisposedException">The reactivity manager is disposed</exception>
    IObservedBinding<T> Binding<T>(Expression<Func<T>> valueAccessor);

    /// <summary>
    /// Observes changes to the value represented by an expression and returns an <see cref="IObservedBinding{T}"/> which converts the value when it is read and converts it back when it is written
    /// </summary>
    /// <typeparam name="TSource">The type of the value</typeparam>
    /// <typeparam name="TTarget">The type to which the value is converted</typeparam>
    /// <param name="valueAccessor">An expression reading a chain of members from a constant</param>
    /// <param name="converter">A function converting the value</param>
    /// <param name="reverseConverter">A function converting back to the value</param>
    /// <exception cref="ArgumentException"><paramref name="valueAccessor"/> is not a chain of members read from a constant, or some part of it is null</exception>
    /// <exception cref="InvalidOperationException">The reactivity manager is not initialized</exception>
    /// <exception cref="ObjectDisposedException">The reactivity manager is disposed</exception>
    IObservedBinding<TTarget> Binding<TSource, TTarget>(Expression<Func<TSource>> valueAccessor, Func<TSource, TTarget> converter, Func<TTarget, TSource> reverseConverter);

    /// <summary>
    /// Initializes the reactivity manager with the component it serves
    /// </summary>
    /// <typeparam name="TComponent">The type of the component</typeparam>
    /// <param name="component">The component</param>
    /// <exception cref="InvalidOperationException">The reactivity manager is already initialized</exception>
    /// <exception cref="ObjectDisposedException">The reactivity manager is disposed</exception>
    void Initialize<TComponent>(TComponent component)
        where TComponent : IReactiveComponent;

    /// <summary>
    /// Notifies the reactivity manager that a render cycle has ended, so that it stops observing what the cycle did not use
    /// </summary>
    /// <remarks><see cref="ReactiveComponentBase"/> calls this at the end of each render; a component implementing <see cref="IReactiveComponent"/> directly must call it each time it renders</remarks>
    /// <exception cref="InvalidOperationException">The reactivity manager is not initialized</exception>
    /// <exception cref="ObjectDisposedException">The reactivity manager is disposed</exception>
    void NotifyCycleEnded();

    /// <summary>
    /// Observes changes to the value represented by an expression and returns it
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="valueAccessor">An expression reading a chain of members from a constant</param>
    /// <returns>The value</returns>
    /// <exception cref="ArgumentException"><paramref name="valueAccessor"/> is not a chain of members read from a constant, or some part of it is null</exception>
    /// <exception cref="InvalidOperationException">The reactivity manager is not initialized</exception>
    /// <exception cref="ObjectDisposedException">The reactivity manager is disposed</exception>
    T Observed<T>(Expression<Func<T>> valueAccessor);

    /// <summary>
    /// Observes changes to the value represented by an expression and, when the value is an <see cref="INotifyCollectionChanged"/>, changes to its contents, and returns it
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="valueAccessor">An expression reading a chain of members from a constant</param>
    /// <returns>The value</returns>
    /// <exception cref="ArgumentException"><paramref name="valueAccessor"/> is not a chain of members read from a constant, or some part of it is null</exception>
    /// <exception cref="InvalidOperationException">The reactivity manager is not initialized</exception>
    /// <exception cref="ObjectDisposedException">The reactivity manager is disposed</exception>
    T ObservedCollection<T>(Expression<Func<T>> valueAccessor);

    /// <summary>
    /// Does nothing, and is kept for compatibility
    /// </summary>
    [Obsolete("Components no longer notify their reactivity manager of being rendered; a component implementing IReactiveComponent directly calls NotifyCycleEnded each time it renders")]
    void OnAfterRender();
}
