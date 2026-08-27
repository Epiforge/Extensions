namespace Epiforge.Extensions.Platforms.Windows.NativeInterop.Types;

[StructLayout(LayoutKind.Sequential)]
unsafe struct WtsInfoExLevel1
{
    public uint SessionId;
    public int SessionState;
    public int SessionFlags;
    public fixed char WinStationName[33];
    public fixed char UserName[21];
    public fixed char DomainName[18];
    public long LogonTime;
    public long ConnectTime;
    public long DisconnectTime;
    public long LastInputTime;
    public long CurrentTime;
    public uint IncomingBytes;
    public uint OutgoingBytes;
    public uint IncomingCompressedBytes;
    public uint OutgoingCompressedBytes;
}
