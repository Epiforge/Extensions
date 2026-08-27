namespace Epiforge.Extensions.Platforms.Windows;

/// <summary>
/// Provides properties concerning the user
/// </summary>
public static class User
{
    /// <summary>
    /// Gets the amount of time the user has been idle (see <see cref="GetIdleTime"/> to also learn whether the amount is exact)
    /// </summary>
    /// <exception cref="InvalidOperationException">The time of the last input could not be retrieved</exception>
    public static TimeSpan IdleTime =>
        GetIdleTime().idleTime;

    /// <summary>
    /// Gets the amount of time the user has been idle, and whether that amount is exact
    /// </summary>
    /// <returns><c>idleTime</c>, the amount of time the user has been idle; and <c>isExact</c>, <c>false</c> when the amount was measured against the system tick count after it had wrapped at least once, in which case the amount may understate the truth by a multiple of 49.7 days</returns>
    /// <exception cref="InvalidOperationException">The time of the last input could not be retrieved</exception>
    public static (TimeSpan idleTime, bool isExact) GetIdleTime()
    {
        if (TryGetSessionIdleTime(out var sessionIdleTime))
            return (sessionIdleTime, true);
        var lastInput = new LastInputInfo
        {
            Size = LastInputInfo.SizeOf,
            Time = 0
        };
        if (!NativeMethods.GetLastInputInfo(ref lastInput))
            throw new InvalidOperationException("Neither WTSQuerySessionInformation nor GetLastInputInfo could supply the time of the last input (see: https://docs.microsoft.com/windows/win32/api/winuser/nf-winuser-getlastinputinfo)");
        var systemTicks = NativeMethods.GetTickCount64();
        return (TimeSpan.FromMilliseconds(unchecked((uint)systemTicks - lastInput.Time)), systemTicks <= uint.MaxValue);
    }

    static bool TryGetSessionIdleTime(out TimeSpan idleTime)
    {
        idleTime = default;
        var buffer = IntPtr.Zero;
        try
        {
            if (!NativeMethods.WTSQuerySessionInformationW(IntPtr.Zero, NativeMethods.WtsCurrentSession, NativeMethods.WtsSessionInfoEx, out buffer, out var bytesReturned) || buffer == IntPtr.Zero || bytesReturned < WtsInfoEx.SizeOf)
                return false;
            var info = Marshal.PtrToStructure<WtsInfoEx>(buffer);
            if (info.Level != 1 || info.Data.LastInputTime <= 0 || info.Data.CurrentTime < info.Data.LastInputTime)
                return false;
            idleTime = TimeSpan.FromTicks(info.Data.CurrentTime - info.Data.LastInputTime);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or MissingMethodException or EntryPointNotFoundException or DllNotFoundException)
        {
            return false;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
                NativeMethods.WTSFreeMemory(buffer);
        }
    }
}
