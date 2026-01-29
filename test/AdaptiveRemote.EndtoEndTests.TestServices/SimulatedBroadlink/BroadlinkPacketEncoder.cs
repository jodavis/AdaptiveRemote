using System.Net.NetworkInformation;

namespace AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;

/// <summary>
/// Independent Broadlink packet encoder for the test simulator.
/// Builds response packets that the app can decode.
/// </summary>
internal static class BroadlinkPacketEncoder
{
    private const int HeaderSize = 0x38;
    private static readonly byte[] Preamble = [0x5a, 0xa5, 0xaa, 0x55, 0x5a, 0xa5, 0xaa, 0x55];

    public static byte[] EncodeScanResponse(short deviceType, PhysicalAddress macAddress, bool isLocked)
    {
        byte[] response = new byte[0x80];

        // Copy MAC address at 0x3A
        byte[] macBytes = macAddress.GetAddressBytes();
        Array.Copy(macBytes, 0, response, 0x3A, 6);

        // Set device type at 0x34
        WriteInt16(response, 0x34, deviceType);

        // Set locked flag at 0x7E
        WriteInt16(response, 0x7E, (short)(isLocked ? 1 : 0));

        return response;
    }

    public static byte[] EncodeResponse(short deviceType, short packetType, short messageCount, PhysicalAddress hostAddress, short deviceId, byte[] payload, short errorCode = 0)
    {
        byte[] packet = new byte[HeaderSize + payload.Length];

        // Write preamble
        Array.Copy(Preamble, 0, packet, 0, Preamble.Length);

        // Write header fields
        WriteInt16(packet, 0x24, deviceType);
        WriteInt16(packet, 0x26, packetType);
        WriteInt16(packet, 0x28, messageCount);

        byte[] macBytes = hostAddress.GetAddressBytes();
        Array.Copy(macBytes, 0, packet, 0x2A, 6);

        WriteInt16(packet, 0x30, deviceId);

        // Write error code at 0x22
        WriteInt16(packet, 0x22, errorCode);

        // Write payload checksum at 0x34
        short payloadChecksum = ComputePayloadChecksum(payload);
        WriteInt16(packet, 0x34, payloadChecksum);

        // Copy payload
        if (payload.Length > 0)
        {
            Array.Copy(payload, 0, packet, HeaderSize, payload.Length);
        }

        // Compute and write packet checksum
        short packetChecksum = ComputePacketChecksum(packet);
        WriteInt16(packet, 0x20, packetChecksum);

        return packet;
    }

    private static void WriteInt16(byte[] data, int offset, short value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Copy(bytes, 0, data, offset, 2);
    }

    private static short ComputePayloadChecksum(byte[] payload)
    {
        int checksum = 0xBEAF; // Checksum seed
        for (int i = 0; i < payload.Length; i++)
        {
            checksum += payload[i];
        }
        return (short)(checksum & 0xFFFF);
    }

    private static short ComputePacketChecksum(byte[] packet)
    {
        int checksum = 0xBEAF; // Checksum seed
        for (int i = 0; i < packet.Length; i++)
        {
            // Skip checksum field itself (at 0x20, 2 bytes)
            if (i == 0x20 || i == 0x21)
            {
                continue;
            }
            checksum += packet[i];
        }
        return (short)(checksum & 0xFFFF);
    }
}
