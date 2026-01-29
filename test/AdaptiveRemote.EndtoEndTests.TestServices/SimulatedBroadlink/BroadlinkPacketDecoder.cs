using System.Net;
using System.Net.NetworkInformation;

namespace AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;

/// <summary>
/// Independent Broadlink packet decoder for the test simulator.
/// Does not reuse app runtime code to avoid masking encoding bugs.
/// </summary>
internal static class BroadlinkPacketDecoder
{
    private const int MinPacketSize = 0x38;
    private const int PreambleSize = 8;
    private static readonly byte[] ExpectedPreamble = [0x5a, 0xa5, 0xaa, 0x55, 0x5a, 0xa5, 0xaa, 0x55];

    public static bool TryDecodePacket(byte[] data, EndPoint remoteEndPoint, out DecodedPacket? packet, out string? error)
    {
        packet = null;
        error = null;

        if (data.Length < MinPacketSize)
        {
            error = $"Packet too small: {data.Length} bytes (minimum {MinPacketSize})";
            return false;
        }

        // Verify preamble
        for (int i = 0; i < PreambleSize; i++)
        {
            if (data[i] != ExpectedPreamble[i])
            {
                error = $"Invalid preamble at byte {i}: expected 0x{ExpectedPreamble[i]:X2}, got 0x{data[i]:X2}";
                return false;
            }
        }

        // Extract header fields
        short packetChecksum = ReadInt16(data, 0x20);
        short deviceType = ReadInt16(data, 0x24);
        short packetType = ReadInt16(data, 0x26);
        short messageCount = ReadInt16(data, 0x28);
        byte[] macBytes = new byte[6];
        Array.Copy(data, 0x2A, macBytes, 0, 6);
        PhysicalAddress hostAddress = new PhysicalAddress(macBytes);
        short deviceId = ReadInt16(data, 0x30);
        short payloadChecksum = ReadInt16(data, 0x34);

        // Extract payload (everything after header)
        byte[] payload = new byte[data.Length - MinPacketSize];
        if (payload.Length > 0)
        {
            Array.Copy(data, MinPacketSize, payload, 0, payload.Length);
        }

        // Verify packet checksum
        int calculatedChecksum = ComputeChecksum(data);
        if (calculatedChecksum != packetChecksum)
        {
            error = $"Checksum mismatch: expected 0x{packetChecksum:X4}, calculated 0x{calculatedChecksum:X4}";
            return false;
        }

        packet = new DecodedPacket
        {
            RemoteEndPoint = remoteEndPoint,
            DeviceType = deviceType,
            PacketType = packetType,
            MessageCount = messageCount,
            HostAddress = hostAddress,
            DeviceId = deviceId,
            PayloadChecksum = payloadChecksum,
            Payload = payload
        };

        return true;
    }

    public static bool TryDecodeScanRequest(byte[] data, out DecodedScanRequest? request, out string? error)
    {
        request = null;
        error = null;

        if (data.Length < 0x30)
        {
            error = $"Scan request too small: {data.Length} bytes (minimum 0x30)";
            return false;
        }

        // Verify byte at 0x26 is 6 (protocol requirement)
        if (data[0x26] != 6)
        {
            error = $"Invalid scan request marker at 0x26: expected 6, got {data[0x26]}";
            return false;
        }

        short localPort = ReadInt16(data, 0x1C);
        byte[] localIp = new byte[4];
        Array.Copy(data, 0x18, localIp, 0, 4);

        request = new DecodedScanRequest
        {
            LocalPort = localPort,
            LocalIpAddress = localIp
        };

        return true;
    }

    private static short ReadInt16(byte[] data, int offset)
    {
        return BitConverter.ToInt16(data, offset);
    }

    private static int ComputeChecksum(byte[] data)
    {
        int checksum = 0xBEAF; // Checksum seed
        for (int i = 0; i < data.Length; i++)
        {
            // Skip checksum field itself (at 0x20, 2 bytes)
            if (i == 0x20 || i == 0x21)
            {
                continue;
            }
            checksum += data[i];
        }
        return checksum & 0xFFFF;
    }
}

/// <summary>
/// Represents a decoded Broadlink packet.
/// </summary>
internal sealed record DecodedPacket
{
    public required EndPoint RemoteEndPoint { get; init; }
    public required short DeviceType { get; init; }
    public required short PacketType { get; init; }
    public required short MessageCount { get; init; }
    public required PhysicalAddress HostAddress { get; init; }
    public required short DeviceId { get; init; }
    public required short PayloadChecksum { get; init; }
    public required byte[] Payload { get; init; }
}

/// <summary>
/// Represents a decoded scan (discovery) request.
/// </summary>
internal sealed record DecodedScanRequest
{
    public required short LocalPort { get; init; }
    public required byte[] LocalIpAddress { get; init; }
}
