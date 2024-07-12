using System.Net;
using System.Net.NetworkInformation;

namespace AdaptiveRemote.Services.Broadlink;

internal class ScanResponsePacket : Payload
{
    public ScanResponsePacket(IPEndPoint hostEndPoint, Memory<byte> bytes)
        : base(bytes)
    {
        HostEndPoint = hostEndPoint;
    }

    // For unit testing
    internal ScanResponsePacket(string hostEndPoint, short deviceType, string physicalAddress, bool isLocked)
        : base(0x80)
    {
        HostEndPoint = IPEndPoint.Parse(hostEndPoint);
        DeviceType = deviceType;
        HostAddress = PhysicalAddress.Parse(physicalAddress);
        IsLocked = isLocked;
    }

    public IPEndPoint HostEndPoint { get; }

    public short DeviceType
    {
        get => GetShort(0x34);
        private set => Set(0x34, value);
    }

    public PhysicalAddress HostAddress
    {
        get => new PhysicalAddress(GetBytes(0x3A, 6));
        private set => Set(0x3A, value.GetAddressBytes());
    }

    public bool IsLocked
    {
        get => GetShort(0x7E) != 0;
        private set => Set(0x7E, (short)(value ? 1 : 0));
    }
}
