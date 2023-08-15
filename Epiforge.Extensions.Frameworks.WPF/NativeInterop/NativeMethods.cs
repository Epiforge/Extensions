namespace Epiforge.Extensions.Frameworks.WPF.NativeInterop;

static partial class NativeMethods
{
#if IS_NET_7_0_OR_GREATER
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnableMenuItem(IntPtr hMenu, Types.SystemCommand uIDEnableItem, MenuStatus uEnable);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial IntPtr GetSystemMenu(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool bRevert);

    [LibraryImport("user32.dll")]
    public static partial IntPtr PostMessage(IntPtr hWnd, WindowMessage msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttribData data);

    [LibraryImport("user32.dll")]
    public static partial IntPtr SendMessage(IntPtr hwnd, WindowMessage msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll")]
    public static partial int TrackPopupMenuEx(IntPtr hmenu, TrackPopupMenuFlags fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);
#else
    [DllImport("user32.dll")]
    public static extern bool EnableMenuItem(IntPtr hMenu, Types.SystemCommand uIDEnableItem, MenuStatus uEnable);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

    [DllImport("user32.dll")]
    public static extern IntPtr PostMessage(IntPtr hWnd, WindowMessage msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttribData data);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hwnd, WindowMessage msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern int TrackPopupMenuEx(IntPtr hmenu, TrackPopupMenuFlags fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);
#endif
}
