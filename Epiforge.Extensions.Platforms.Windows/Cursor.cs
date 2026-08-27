namespace Epiforge.Extensions.Platforms.Windows;

/// <summary>
/// Wraps Win32 API methods dealing with the cursor
/// </summary>
public sealed class Cursor
{
    /// <summary>
    /// Gets the current position of the cursor
    /// </summary>
    /// <exception cref="Win32Exception">The position of the cursor could not be retrieved (see: https://docs.microsoft.com/windows/win32/api/winuser/nf-winuser-getcursorpos)</exception>
    public static (int x, int y) GetPosition()
    {
        if (!NativeMethods.GetCursorPos(out var point))
            throw new Win32Exception("Invoking GetCursorPos did not succeed; the return value was zero (see: https://docs.microsoft.com/windows/win32/api/winuser/nf-winuser-getcursorpos)");
        return (point.X, point.Y);
    }

    /// <summary>
    /// Sets the current position of the cursor
    /// </summary>
    /// <param name="x">The x coordinate of the position to which to set the cursor</param>
    /// <param name="y">The y coordinate of the position to which to set the cursor</param>
    /// <exception cref="Win32Exception">The position of the cursor could not be set (see: https://docs.microsoft.com/windows/win32/api/winuser/nf-winuser-setcursorpos)</exception>
    public static void SetPosition(int x, int y)
    {
        if (!NativeMethods.SetCursorPos(x, y))
            throw new Win32Exception("Invoking SetCursorPos did not succeed; the return value was zero (see: https://docs.microsoft.com/windows/win32/api/winuser/nf-winuser-setcursorpos)");
    }
}
