namespace Epiforge.Extensions.Frameworks.WPF.NativeInterop.Types;

[StructLayout(LayoutKind.Sequential)]
struct AccentPolicy
{
    public AccentState AccentState;
    public AccentFlags AccentFlags;
    public uint GradientColor;
    public uint AnimationId;
}
