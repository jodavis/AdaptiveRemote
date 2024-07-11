using System.Net;
using System.Net.NetworkInformation;

namespace AdaptiveRemote.Services.Broadlink;

internal class ScanResponsePacket : Payload
{
    public ScanResponsePacket(Memory<byte> bytes)
        : base(bytes)
    { }

    public IPEndPoint? HostEndPoint { get; set; }

    public short DeviceType
    {
        get => GetShort(0x34);
    }

    public PhysicalAddress HostAddress
    {
        get => new PhysicalAddress(GetBytes(0x3A, 6));
    }

    public bool IsLocked
    {
        get => GetShort(0x7E) != 0;
    }
}
