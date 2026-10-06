namespace Epiforge.Extensions.Blazor;

/// <summary>
/// Provides the base class for components which re-render when what they observe changes, with the lifecycle of <see cref="ComponentBase"/>
/// </summary>
/// <remarks>
/// This class implements <see cref="IComponent"/>, <see cref="IHandleEvent"/> and <see cref="IHandleAfterRender"/> itself rather than deriving from <see cref="ComponentBase"/>, so that it knows where each render begins and ends without reaching into the framework's internals
/// </remarks>
public abstract class ReactiveComponentBase :
    IComponent,
    IDisposable,
    IHandleAfterRender,
    IHandleEvent,
    IReactiveComponent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReactiveComponentBase"/> class
    /// </summary>
    public ReactiveComponentBase()
    {
        reactivityManager = new();
        reactivityManager.Initialize(this);
        renderFragment = builder =>
        {
            hasPendingQueuedRender = false;
            hasNeverRendered = false;
            isBuilding = true;
            try
            {
                if (notificationQueue is { IsEmpty: false } queue)
                    queue.Drain();
                BuildRenderTree(builder);
            }
            finally
            {
                isBuilding = false;
            }
            reactivityManager.NotifyCycleEnded();
        };
    }

    bool hasCalledOnAfterRender;
    bool hasNeverRendered = true;
    bool hasPendingQueuedRender;
    bool initialized;
    bool isBuilding;
    int notificationPending;
    NotificationQueue? notificationQueue;
    readonly ReactivityManager reactivityManager;
    readonly RenderFragment renderFragment;
    RenderHandle renderHandle;
#if IS_NET_9_0_OR_GREATER
    (IComponentRenderMode? mode, bool cached) renderMode;
#endif

#if IS_NET_9_0_OR_GREATER
    /// <summary>
    /// Gets the <see cref="ResourceAssetCollection"/> for the application
    /// </summary>
    protected ResourceAssetCollection Assets =>
        renderHandle.Assets;

    /// <summary>
    /// Gets the <see cref="IComponentRenderMode"/> assigned to this component
    /// </summary>
    protected IComponentRenderMode? AssignedRenderMode
    {
        get
        {
            if (!renderMode.cached)
                renderMode = (renderHandle.RenderMode, true);
            return renderMode.mode;
        }
    }
#endif

    /// <summary>
    /// Gets the reactivity manager of the component
    /// </summary>
    protected IReactivityManager ReactivityManager =>
        reactivityManager;

#if IS_NET_9_0_OR_GREATER
    /// <summary>
    /// Gets the <see cref="Microsoft.AspNetCore.Components.RendererInfo"/> the component is running on
    /// </summary>
    protected RendererInfo RendererInfo =>
        renderHandle.RendererInfo;
#endif

    /// <summary>
    /// Observes changes to the value represented by an expression and returns an <see cref="IObservedBinding{T}"/> through which the value can be read and written
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="valueAccessor">An expression reading a chain of members from a constant</param>
    /// <exception cref="ArgumentException"><paramref name="valueAccessor"/> is not a chain of members read from a constant, or some part of it is null</exception>
    protected IObservedBinding<T> Binding<T>(Expression<Func<T>> valueAccessor) =>
        reactivityManager.Binding(valueAccessor);

    /// <summary>
    /// Observes changes to the value represented by an expression and returns an <see cref="IObservedBinding{T}"/> which converts the value when it is read and converts it back when it is written
    /// </summary>
    /// <typeparam name="TSource">The type of the value</typeparam>
    /// <typeparam name="TTarget">The type to which the value is converted</typeparam>
    /// <param name="valueAccessor">An expression reading a chain of members from a constant</param>
    /// <param name="converter">A function converting the value</param>
    /// <param name="reverseConverter">A function converting back to the value</param>
    /// <exception cref="ArgumentException"><paramref name="valueAccessor"/> is not a chain of members read from a constant, or some part of it is null</exception>
    protected IObservedBinding<TTarget> Binding<TSource, TTarget>(Expression<Func<TSource>> valueAccessor, Func<TSource, TTarget> converter, Func<TTarget, TSource> reverseConverter) =>
        reactivityManager.Binding(valueAccessor, converter, reverseConverter);

    /// <summary>
    /// Renders the component to the supplied <see cref="RenderTreeBuilder"/>
    /// </summary>
    /// <param name="builder">A <see cref="RenderTreeBuilder"/> that will receive the render output</param>
    protected virtual void BuildRenderTree(RenderTreeBuilder builder)
    {
    }

    Task CallOnParametersSetAsync()
    {
        OnParametersSet();
        var task = OnParametersSetAsync();
        var shouldAwaitTask = task.Status != TaskStatus.RanToCompletion && task.Status != TaskStatus.Canceled;
#if IS_NET_10_0_OR_GREATER
        if (task.Status != TaskStatus.Faulted)
            StateHasChanged();
#else
        StateHasChanged();
#endif
        return shouldAwaitTask ? CallStateHasChangedOnAsyncCompletion(task) : Task.CompletedTask;
    }

    async Task CallStateHasChangedOnAsyncCompletion(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
            if (task.IsCanceled)
                return;
            throw;
        }
        StateHasChanged();
    }

    /// <summary>
    /// Configures bindings at the end of each render cycle
    /// </summary>
    protected virtual void ConfigureBindings()
    {
    }

#if IS_NET_8_0_OR_GREATER
    /// <summary>
    /// Treats the supplied <paramref name="exception"/> as being thrown by this component, so that it is handled by the nearest error boundary or by the renderer
    /// </summary>
    /// <param name="exception">The <see cref="Exception"/> that will be dispatched to the renderer</param>
    /// <returns>A <see cref="Task"/> that will be completed when the exception has finished dispatching</returns>
    protected Task DispatchExceptionAsync(Exception exception) =>
        renderHandle.DispatchExceptionAsync(exception);
#endif

    /// <summary>
    /// Releases what the component observes
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases what the component observes
    /// </summary>
    /// <param name="disposing">true when called from <see cref="Dispose()"/></param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
            reactivityManager.Dispose();
    }

    internal void FlushNotifications()
    {
        Volatile.Write(ref notificationPending, 0);
        if (!isBuilding)
            StateHasChanged();
    }

    /// <summary>
    /// Executes the supplied work item on the associated renderer's synchronization context
    /// </summary>
    /// <param name="workItem">The work item to execute</param>
    protected Task InvokeAsync(Action workItem) =>
        renderHandle.Dispatcher.InvokeAsync(workItem);

    /// <summary>
    /// Executes the supplied work item on the associated renderer's synchronization context
    /// </summary>
    /// <param name="workItem">The work item to execute</param>
    protected Task InvokeAsync(Func<Task> workItem) =>
        renderHandle.Dispatcher.InvokeAsync(workItem);

    /// <summary>
    /// Observes changes to the value represented by an expression and returns it
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="valueAccessor">An expression reading a chain of members from a constant</param>
    /// <returns>The value</returns>
    /// <exception cref="ArgumentException"><paramref name="valueAccessor"/> is not a chain of members read from a constant, or some part of it is null</exception>
    protected T Observed<T>(Expression<Func<T>> valueAccessor) =>
        reactivityManager.Observed(valueAccessor);

    /// <summary>
    /// Observes changes to the value represented by an expression and, when the value is an <see cref="INotifyCollectionChanged"/>, changes to its contents, and returns it
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="valueAccessor">An expression reading a chain of members from a constant</param>
    /// <returns>The value</returns>
    /// <exception cref="ArgumentException"><paramref name="valueAccessor"/> is not a chain of members read from a constant, or some part of it is null</exception>
    protected T ObservedCollection<T>(Expression<Func<T>> valueAccessor) =>
        reactivityManager.ObservedCollection(valueAccessor);

    /// <summary>
    /// Method invoked after each time the component has rendered interactively and the UI has finished updating
    /// </summary>
    /// <param name="firstRender">true if this is the first time <see cref="OnAfterRender(bool)"/> has been invoked on this component instance; otherwise false</param>
    protected virtual void OnAfterRender(bool firstRender)
    {
    }

    /// <summary>
    /// Method invoked after each time the component has rendered interactively and the UI has finished updating
    /// </summary>
    /// <param name="firstRender">true if this is the first time <see cref="OnAfterRenderAsync(bool)"/> has been invoked on this component instance; otherwise false</param>
    /// <returns>A <see cref="Task"/> representing any asynchronous operation</returns>
    protected virtual Task OnAfterRenderAsync(bool firstRender) =>
        Task.CompletedTask;

    /// <summary>
    /// Method invoked when the component is ready to start, having received its initial parameters from its parent in the render tree
    /// </summary>
    protected virtual void OnInitialized()
    {
    }

    /// <summary>
    /// Method invoked when the component is ready to start, having received its initial parameters from its parent in the render tree
    /// </summary>
    /// <returns>A <see cref="Task"/> representing any asynchronous operation</returns>
    protected virtual Task OnInitializedAsync() =>
        Task.CompletedTask;

    /// <summary>
    /// Method invoked when the component has received parameters from its parent in the render tree, and the incoming values have been assigned to properties
    /// </summary>
    protected virtual void OnParametersSet()
    {
    }

    /// <summary>
    /// Method invoked when the component has received parameters from its parent in the render tree, and the incoming values have been assigned to properties
    /// </summary>
    /// <returns>A <see cref="Task"/> representing any asynchronous operation</returns>
    protected virtual Task OnParametersSetAsync() =>
        Task.CompletedTask;

    async Task RunInitAndSetParametersAsync()
    {
        OnInitialized();
        var task = OnInitializedAsync();
        if (task.Status != TaskStatus.RanToCompletion && task.Status != TaskStatus.Canceled)
        {
#if IS_NET_10_0_OR_GREATER
            if (task.Status != TaskStatus.Faulted)
                StateHasChanged();
#else
            StateHasChanged();
#endif
            try
            {
                await task;
            }
            catch
            {
                if (!task.IsCanceled)
                    throw;
            }
        }
        await CallOnParametersSetAsync();
    }

    /// <summary>
    /// Sets parameters supplied by the component's parent in the render tree
    /// </summary>
    /// <param name="parameters">The parameters</param>
    /// <returns>A <see cref="Task"/> that completes when the component has finished updating and rendering itself</returns>
    public virtual Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        if (!initialized)
        {
            initialized = true;
            return RunInitAndSetParametersAsync();
        }
        return CallOnParametersSetAsync();
    }

    /// <summary>
    /// Returns a flag to indicate whether the component should render
    /// </summary>
    protected virtual bool ShouldRender() =>
        true;

    /// <summary>
    /// Notifies the component that its state has changed, causing it to be re-rendered when applicable
    /// </summary>
    protected void StateHasChanged()
    {
        if (hasPendingQueuedRender)
            return;
        if (hasNeverRendered || ShouldRender() || renderHandle.IsRenderingOnMetadataUpdate)
        {
            hasPendingQueuedRender = true;
            try
            {
                renderHandle.Render(renderFragment);
            }
            catch
            {
                hasPendingQueuedRender = false;
                throw;
            }
        }
    }

    void IComponent.Attach(RenderHandle renderHandle)
    {
        if (this.renderHandle.IsInitialized)
            throw new InvalidOperationException($"The render handle is already set. Cannot initialize a {nameof(ReactiveComponentBase)} more than once.");
        this.renderHandle = renderHandle;
    }

    Task IHandleAfterRender.OnAfterRenderAsync()
    {
        var firstRender = !hasCalledOnAfterRender;
        hasCalledOnAfterRender = true;
        OnAfterRender(firstRender);
        return OnAfterRenderAsync(firstRender);
    }

    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
    {
        var task = callback.InvokeAsync(arg);
        var shouldAwaitTask = task.Status != TaskStatus.RanToCompletion && task.Status != TaskStatus.Canceled;
        StateHasChanged();
        return shouldAwaitTask ? CallStateHasChangedOnAsyncCompletion(task) : Task.CompletedTask;
    }

    void IReactiveComponent.ConfigureBindings() =>
        ConfigureBindings();

    void IReactiveComponent.StateHasChanged()
    {
        if (Interlocked.Exchange(ref notificationPending, 1) != 0)
            return;
        (notificationQueue ??= NotificationQueue.For(renderHandle.Dispatcher)).Enqueue(this);
    }
}
