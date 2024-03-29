namespace Epiforge.Extensions.Frameworks.WPF.NativeInterop.Types;

[Flags]
enum MenuStatus : uint
{
    BYCOMMAND = 0x0,
    BYPOSITION = 0x400,
    DISABLED = 0x2,
    ENABLED = BYCOMMAND,
    GRAYED = 0x1
}
