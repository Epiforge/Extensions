namespace Epiforge.Extensions.Components;

/// <summary>
/// Represents the arguments for the <see cref="INotifyDisposalOverridden.DisposalOverridden"/>, <see cref="INotifyDisposed.Disposed"/>, and <see cref="INotifyDisposing.Disposing"/> events
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DisposalNotificationEventArgs"/> class
/// </remarks>
public sealed class DisposalNotificationEventArgs :
    EventArgs
{
    /// <summary>
    /// Gets a reusable instance of arguments for when disposal is ocurring because <see cref="IDisposable.Dispose"/> or <see cref="IAsyncDisposable.DisposeAsync"/> was called
    /// </summary>
    public static DisposalNotificationEventArgs ByCallingDispose { get; } = new DisposalNotificationEventArgs();
}
