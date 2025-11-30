using System.Net;
using System.Net.NetworkInformation;

namespace AdaptiveRemote.Services.Broadlink;

/// <summary>
/// <see cref="https://github.com/mjg59/python-broadlink/blob/master/protocol.md#network-discovery"/>
/// </summary>
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

    /// <summary>
    /// The EndPoint from which this scan response was sent
    /// </summary>
    public IPEndPoint HostEndPoint { get; }

    private const int DeviceTypePosition = 0x34;
    /// <summary>
    /// A code representing what type of Broadlink Device this is
    /// </summary>
    public short DeviceType
    {
        get => GetShort(DeviceTypePosition);
        private set => Set(DeviceTypePosition, value);
    }

    private const int HostAddressPosition = 0x3A;
    /// <summary>
    /// The MAC address of the Broadlink Device
    /// </summary>
    public PhysicalAddress HostAddress
    {
        get => new PhysicalAddress(GetBytes(HostAddressPosition, 6));
        private set => Set(HostAddressPosition, value.GetAddressBytes());
    }

    private const int IsLockedPosition = 0x7E;
    /// <summary>
    /// Whether or not the device is Locked. Whatever that means. It better
    /// not be, but I don't know why.
    /// </summary>
    public bool IsLocked
    {
        get => GetShort(IsLockedPosition) != 0;
        private set => Set(IsLockedPosition, (short)(value ? 1 : 0));
    }
}
