namespace Epiforge.Extensions.Platforms.Windows;

/// <summary>
/// Provides methods for interacting with consoles
/// </summary>
public static class ConsoleAssist
{
    /// <summary>
    /// Attaches to the console of the specified process as a client application
    /// </summary>
    /// <param name="process">The process the console of which to use</param>
    public static bool AttachTo(Process process)
    {
#if IS_NET_6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(process);
#else
        if (process is null)
            throw new ArgumentNullException(nameof(process));
#endif
#if IS_NET_7_0_OR_GREATER
        return NativeMethods.AttachConsole((uint)process.Id);
#else
        return NativeMethods.AttachConsole(process.Id);
#endif
    }

    /// <summary>
    /// Attaches to the console of the parent process as a client application
    /// </summary>
    public static bool AttachToParentProcess() =>
#if IS_NET_7_0_OR_GREATER
        NativeMethods.AttachConsole(unchecked((uint)-1));
#else
        NativeMethods.AttachConsole(-1);
#endif
}
