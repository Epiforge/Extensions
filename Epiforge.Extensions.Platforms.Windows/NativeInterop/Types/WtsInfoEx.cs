namespace Epiforge.Extensions.Platforms.Windows.NativeInterop.Types;

[StructLayout(LayoutKind.Sequential)]
struct WtsInfoEx
{
    public uint Level;
    public WtsInfoExLevel1 Data;
    public static readonly int SizeOf = Marshal.SizeOf(typeof(WtsInfoEx));
}
