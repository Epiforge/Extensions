namespace Epiforge.Extensions.Frameworks.WPF.NativeInterop.Types;

[StructLayout(LayoutKind.Sequential)]
struct WindowCompositionAttribData
{
    public WindowCompositionAttribute Attribute;
    public IntPtr Data;
    public int SizeOfData;
}
