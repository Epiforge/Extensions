This library includes utilities for interoperation with Microsoft Windows, including:

* `Activation` - provides information relating to Windows Activation
* `ConsoleAssist` - provides methods for interacting with consoles
* `Cursor` - wraps Win32 API methods dealing with the cursor
* `Shell` - wraps methods of the WScript.Shell COM object (specifically useful for invoking its `CreateShortcut` function)
* `Theme` - represents the current Windows theme (its `Color` and `IsDark` properties report what Windows says and are not settable)
* `User` - provides properties concerning the user, including `IdleTime`; `GetIdleTime` returns the same figure along with whether it is exact, which it is except when the session could not supply an absolute last-input timestamp and the system tick count has already wrapped
* `WindowingSystem` - provides methods for dealing with the windowing system, including reading and setting the foreground window, reading its position, and flashing windows

Also provides extension methods for dealing with processes, including:

* `CloseMainWindowAsync` - close the main window of the specified process
* `GetParentProcess` - gets the parent process of the specified process