using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using AdaptiveRemote.TestUtilities;

namespace AdaptiveRemote.Services.Broadlink;

[TestClass]
public class PayloadTests
{
    public TestContext? TestContext { get; set; }

    [TestMethod]
    public void Payload_Constructor_FromMemory_CreatesPayload()
    {
        // Arrange
        byte[] input = [1, 2, 3, 4];

        // Act
        Payload sut = new(input);

        // Assert
        MemoryAssert.WriteTo(TestContext, input, sut.GetBuffer());

        MemoryAssert.AreEqual(input, sut.GetBuffer(), nameof(sut) + ".GetBuffer()");
        Assert.AreEqual(input.Length, sut.Size, nameof(sut.Size));
    }

    [TestMethod]
    public void Payload_Constructor_FromSize_CreatesPayload()
    {
        // Arrange
        int input = 6;
        byte[] expected = new byte[input];

        // Act
        Payload sut = new(input);

        // Assert
        MemoryAssert.WriteTo(TestContext, expected, sut.GetBuffer());

        MemoryAssert.AreEqual(expected, sut.GetBuffer(), nameof(sut) + ".GetBuffer()");
        Assert.AreEqual(expected.Length, sut.Size, nameof(sut.Size));
    }

    [TestMethod]
    public void Payload_Constructor_FromMultiplePayloads()
    {
        // Arrange
        Payload input1 = new(new byte[] { 1, 2, 3, 4 });
        Payload input2 = new(new byte[] { 5, 6, 7, 8 });

        byte[] expected = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        // Act
        Payload sut = new(input1, input2);

        // Assert
        MemoryAssert.WriteTo(TestContext, expected, sut.GetBuffer());

        MemoryAssert.AreEqual(expected, sut.GetBuffer(), nameof(sut) + ".GetBuffer()");
        Assert.AreEqual(expected.Length, sut.Size, nameof(sut.Size));

        // TODO: Verify that changes to parent buffer are reflected in child, and vice versa
    }

    [TestMethod]
    public void Payload_GetsAndSetsIntValues()
    {
        // Arrange
        TestPayload sut = new();

        byte[] expected = new byte[sut.Size];
        expected[0x02] = 0x78;
        expected[0x03] = 0x56;
        expected[0x04] = 0x34;
        expected[0x05] = 0x12;

        // Act
        sut.IntValueAt0x02 = 0x12345678;

        // Assert
        MemoryAssert.WriteTo(TestContext, expected, sut.GetBuffer());

        Assert.AreEqual(0x12345678, sut.IntValueAt0x02, nameof(sut.IntValueAt0x02));
        MemoryAssert.AreEqual(expected, sut.GetBuffer(), nameof(sut) + ".GetBuffer()");
    }

    [TestMethod]
    public void Payload_GetsAndSetsShortValues()
    {
        // Arrange
        TestPayload sut = new();

        byte[] expected = new byte[sut.Size];
        expected[0x06] = 0x78;
        expected[0x07] = 0x56;

        // Act
        sut.ShortValueAt0x06 = 0x5678;

        // Assert
        MemoryAssert.WriteTo(TestContext, expected, sut.GetBuffer());

        Assert.AreEqual(0x5678, sut.ShortValueAt0x06, nameof(sut.ShortValueAt0x06));
        MemoryAssert.AreEqual(expected, sut.GetBuffer(), nameof(sut) + ".GetBuffer()");
    }

    [TestMethod]
    public void Payload_GetsAndSetsLongValues()
    {
        // Arrange
        TestPayload sut = new();

        byte[] expected = new byte[sut.Size];
        expected[0x08] = 0xF0;
        expected[0x09] = 0xDE;
        expected[0x0A] = 0xBC;
        expected[0x0B] = 0x9A;
        expected[0x0C] = 0x78;
        expected[0x0D] = 0x56;
        expected[0x0E] = 0x34;
        expected[0x0F] = 0x12;

        // Act
        sut.LongValueAt0x08 = 0x123456789ABCDEF0;

        // Assert
        MemoryAssert.WriteTo(TestContext, expected, sut.GetBuffer());

        Assert.AreEqual(0x123456789ABCDEF0, sut.LongValueAt0x08, nameof(sut.LongValueAt0x08));
        MemoryAssert.AreEqual(expected, sut.GetBuffer(), nameof(sut) + ".GetBuffer()");
    }

    [TestMethod]
    public void Payload_GetsAndSetsStringValues()
    {
        // Arrange
        TestPayload sut = new();

        byte[] expected = new byte[sut.Size];
        expected[0x10] = 0x48;
        expected[0x11] = 0x65;
        expected[0x12] = 0x6C;
        expected[0x13] = 0x6C;
        expected[0x14] = 0x6F;

        // Act
        sut.StringValueAt0x10 = "Hello";

        // Assert
        MemoryAssert.WriteTo(TestContext, expected, sut.GetBuffer());

        Assert.AreEqual("Hello", sut.StringValueAt0x10, nameof(sut.StringValueAt0x10));
        MemoryAssert.AreEqual(expected, sut.GetBuffer(), nameof(sut) + ".GetBuffer()");
    }

    [TestMethod]
    public void Payload_GetsAndSetsBytes()
    {
        // Arrange
        TestPayload sut = new();

        byte[] input = [0x56, 0x34, 0x12, 0xAB];
        byte[] expected = new byte[sut.Size];
        expected[0x20] = 0x56;
        expected[0x21] = 0x34;
        expected[0x22] = 0x12;
        expected[0x23] = 0xAB;

        // Act
        sut.FourBytesAt0x20 = input;

        // Assert
        MemoryAssert.WriteTo(TestContext, expected, sut.GetBuffer());

        MemoryAssert.AreEqual(input, sut.FourBytesAt0x20, nameof(sut.FourBytesAt0x20));
        MemoryAssert.AreEqual(expected, sut.GetBuffer(), nameof(sut) + ".GetBuffer()");
    }

    [TestMethod]
    public void Payload_GetsAndSetsBytesToEndOfBuffer()
    {
        // Arrange
        TestPayload sut = new();

        byte[] input = [0x56, 0x34, 0x12, 0xAB];
        byte[] expectedBuffer = new byte[sut.Size];
        expectedBuffer[0x20] = 0x56;
        expectedBuffer[0x21] = 0x34;
        expectedBuffer[0x22] = 0x12;
        expectedBuffer[0x23] = 0xAB;

        byte[] expected = expectedBuffer.AsSpan(0x20).ToArray();

        // Act
        sut.AllBytesAt0x20 = input;

        // Assert
        MemoryAssert.WriteTo(TestContext, expectedBuffer, sut.GetBuffer());

        MemoryAssert.AreEqual(expected, sut.AllBytesAt0x20, nameof(sut.AllBytesAt0x20));
        MemoryAssert.AreEqual(expectedBuffer, sut.GetBuffer(), nameof(sut) + ".GetBuffer()");
    }

    private class TestPayload : Payload
    {
        public TestPayload()
            : base(0x30)
        { }

        public int IntValueAt0x02
        {
            get => GetInt(0x02);
            set => Set(0x02, value);
        }

        public short ShortValueAt0x06
        {
            get => GetShort(0x06);
            set => Set(0x06, value);
        }

        public long LongValueAt0x08
        {
            get => GetLong(0x08);
            set => Set(0x08, value);
        }

        public string StringValueAt0x10
        {
            get => GetString(0x10);
            set => Set(0x10, value);
        }

        public byte[] FourBytesAt0x20
        {
            get => GetBytes(0x20, 4);
            set => Set(0x20, value);
        }

        public byte[] AllBytesAt0x20
        {
            get => GetBytes(0x20);
            set => Set(0x20, value);
        }
    }

    [TestMethod]
    public void AuthenticateRequestPayload_GetBuffer()
    {
        // Arrange
        AuthenticateRequestPayload sut = new();

        byte[] expected =
        [
            0x00, 0x00, 0x00, 0x00,
            0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x01,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x01,
            0x00, 0x00,
            0x54, 0x65, 0x73, 0x74, 0x20, 0x31,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        ];

        // Act
        Memory<byte> result = sut.GetBuffer();

        // Assert
        MemoryAssert.WriteTo(TestContext, expected, result);
        MemoryAssert.AreEqual(expected, result, nameof(result));

        Assert.AreEqual(0x50, sut.Size, nameof(sut.Size));
        Assert.AreEqual(-15438, sut.ComputeChecksum(), nameof(sut.ComputeChecksum));
    }

    [TestMethod]
    public void AuthenticateResponsePayload_GetBuffer()
    {
        // Arrange
        byte[] input =
        [
            0xAD, 0x2B, // DeviceID
            0x00, 0x00,
            0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF,
            0xED, 0xCB, 0xA9, 0x87, 0x65, 0x43, 0x21, 0x0F,
        ];

        // Act
        AuthenticateResponsePayload sut = new(input);

        // Assert
        Assert.AreEqual(0x2BAD, sut.DeviceID, nameof(sut.DeviceID));
        MemoryAssert.AreEqual(input.AsSpan(0x04, 16).ToArray(), sut.EncryptionKey, nameof(sut.EncryptionKey));

        Assert.AreEqual(input.Length, sut.Size, nameof(sut.Size));
        Assert.AreEqual(-14585, sut.ComputeChecksum(), nameof(sut.ComputeChecksum));
    }

    [TestMethod]
    public void CommandPayload_GetBuffer()
    {
        // Arrange
        byte[] inputData = [0x55, 0x44, 0x33, 0x22, 0x11];
        const int inputCommand = 0x45;

        CommandPayload sut = new(inputCommand, inputData);

        byte[] expected =
        [
            0x09, 0x00,
            0x45, 0x00, 0x00, 0x00,
            0x55, 0x44, 0x33, 0x22, 0x11
        ];

        // Act
        Memory<byte> result = sut.GetBuffer();

        // Assert
        MemoryAssert.WriteTo(TestContext, expected, result);
        MemoryAssert.AreEqual(expected, result, nameof(result));

        Assert.AreEqual(inputCommand, sut.Command, nameof(sut.Command));
        Assert.AreEqual(0x09, sut.CommandAndDataLength, nameof(sut.CommandAndDataLength));
        MemoryAssert.AreEqual(inputData, sut.Data, nameof(sut.Data));
        Assert.AreEqual(11, sut.Size, nameof(sut.Size));
    }

    [TestMethod]
    public void SendPacketHeader_GetBuffer()
    {
        // Arrange
        short inputDeviceType = 0x7ABC;
        short inputPacketType = 0x1221;
        short inputMessageCount = 0x1234;
        PhysicalAddress inputHostAddress = new([0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA]);
        short inputDeviceID = 0x2BAD;
        short inputPayloadChecksum = 0x3344;
        short inputPacketChecksum = 0x5566;

        SendPacketHeader sut = new()
        {
            DeviceType = inputDeviceType,
            PacketType = inputPacketType,
            MessageCount = inputMessageCount,
            HostAddress = inputHostAddress,
            DeviceID = inputDeviceID,
            PayloadChecksum = inputPayloadChecksum,
            PacketChecksum = inputPacketChecksum,
        };

        byte[] expected =
        [
            0x5a, 0xa5, 0xaa, 0x55, 0x5a, 0xa5, 0xaa, 0x55,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x66, 0x55, // PacketChecksum
            0x00, 0x00,
            0xBC, 0x7A, // DeviceType
            0x21, 0x12, // PacketType
            0x34, 0x12, // MessageCount
            0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, // HostAddress
            0xAD, 0x2B, // DeviceID
            0x00, 0x00,
            0x44, 0x33, // PayloadChecksum
            0x00, 0x00,
        ];

        // Act
        Memory<byte> result = sut.GetBuffer();

        // Assert
        MemoryAssert.WriteTo(TestContext, expected, result);
        MemoryAssert.AreEqual(expected, result, nameof(result));

        Assert.AreEqual(inputDeviceType, sut.DeviceType, nameof(sut.DeviceType));
        Assert.AreEqual(inputPacketType, sut.PacketType, nameof(sut.PacketType));
        Assert.AreEqual(inputMessageCount, sut.MessageCount, nameof(sut.MessageCount));
        Assert.AreEqual(inputHostAddress, sut.HostAddress, nameof(sut.HostAddress));
        Assert.AreEqual(inputDeviceID, sut.DeviceID, nameof(sut.DeviceID));
        Assert.AreEqual(inputPacketChecksum, sut.PacketChecksum, nameof(sut.PacketChecksum));
        Assert.AreEqual(inputPayloadChecksum, sut.PayloadChecksum, nameof(sut.PayloadChecksum));
        Assert.AreEqual(0x38, sut.Size, nameof(sut.Size));

        MemoryAssert.AreEqual(expected.AsSpan(0, 8).ToArray(), sut.Preamble, nameof(sut.Preamble));

        Assert.AreEqual(-13473, sut.ComputeChecksum(), nameof(sut.ComputeChecksum));
    }

    [TestMethod]
    public void ScanRequestPayload_GetBuffer()
    {
        // Arrange
        DateTime inputRequestTime = new(DateTime.Now.Year, 10, 30, 11, 55, 0);
        byte[] inputIPAddress = [192, 168, 200, 145];
        short inputPort = 0x0050;
        short inputChecksum = 0x5432;

        // Offset difference for Daylight Savings Time
        byte odst = DateTime.Now.IsDaylightSavingTime() ? (byte)0x03 : (byte)0x04;

        // Checksum affected by odst
        short expectedChecksum = (short)(-14834 + odst);

        ScanRequestPacket sut = new()
        {
            RequestTime = inputRequestTime,
            LocalIPAddress = inputIPAddress,
            LocalPort = inputPort,
            Checksum = inputChecksum,
        };

        byte[] expected =
        [
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x5C, 0xFE, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x37, // RequestTime
            0x0B, odst, 0x1E, 0x0A, 0x00, 0x00, 0x00, 0x00,
            192, 168, 200, 145, // LocalIPAddress
            0x50, 0x00, // LocalPort
            0x00, 0x00,
            0x32, 0x54, // Checksum
                        0x00, 0x00, 0x00, 0x00, 0x06, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        ];

        // Act
        Memory<byte> result = sut.GetBuffer();

        // Assert
        MemoryAssert.WriteTo(TestContext, expected, result);
        MemoryAssert.AreEqual(expected, result, nameof(result));

        Assert.AreEqual(inputRequestTime, sut.RequestTime, nameof(sut.RequestTime));
        MemoryAssert.AreEqual(inputIPAddress, sut.LocalIPAddress, nameof(sut.LocalIPAddress));
        Assert.AreEqual(inputPort, sut.LocalPort, nameof(sut.LocalPort));
        Assert.AreEqual(inputChecksum, sut.Checksum, nameof(sut.Checksum));
        Assert.AreEqual(0x30, sut.Size, nameof(sut.Size));

        Assert.AreEqual(expectedChecksum, sut.ComputeChecksum(), nameof(sut.ComputeChecksum));
    }

    [TestMethod]
    public void ScanResponsePayload_GetBuffer()
    {
        // Arrange
        byte[] input =
        [
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
                                    0x65, 0x43, // DeviceType
                                                0x00, 0x00,
            0x00, 0x00,
            0xAB, 0xBC, 0xCD, 0xDE, 0xEF, 0xFA, // HostAddress
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                                0x01, 0x00, // IsLocked
        ];

        // Act
        ScanResponsePacket sut = new(null!, input);

        // Assert
        Assert.AreEqual(0x4365, sut.DeviceType, nameof(sut.DeviceType));
        Assert.AreEqual(new PhysicalAddress([0xAB, 0xBC, 0xCD, 0xDE, 0xEF, 0xFA]), sut.HostAddress, nameof(sut.HostAddress));
        Assert.IsTrue(sut.IsLocked, nameof(sut.IsLocked));

        Assert.AreEqual(0x80, sut.Size, nameof(sut.Size));
        Assert.AreEqual(-15277, sut.ComputeChecksum(), nameof(sut.ComputeChecksum));
    }

    [TestMethod]
    public void SendPacket_GetBuffer()
    {
        // Arrange
        short inputDeviceType = 0x7ABC;
        short inputPacketType = 0x1221;
        short inputMessageCount = 0x1234;
        PhysicalAddress inputHostAddress = new([0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA]);
        short inputDeviceID = 0x2BAD;
        short inputPayloadChecksum = 0x3344;
        short inputPacketChecksum = 0x5566;
        byte[] inputPayload = [0x12, 0x34, 0x54, 0x32, 0x1F, 0xED, 0xCD, 0xEF];

        SendPacketHeader header = new()
        {
            DeviceType = inputDeviceType,
            PacketType = inputPacketType,
            MessageCount = inputMessageCount,
            HostAddress = inputHostAddress,
            DeviceID = inputDeviceID,
            PayloadChecksum = inputPayloadChecksum,
            PacketChecksum = inputPacketChecksum,
        };

        Payload payload = new(inputPayload);

        byte[] expected =
        [
            0x5a, 0xa5, 0xaa, 0x55, 0x5a, 0xa5, 0xaa, 0x55,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x66, 0x55, // PacketChecksum
            0x00, 0x00,
            0xBC, 0x7A, // DeviceType
            0x21, 0x12, // PacketType
            0x34, 0x12, // MessageCount
            0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, // HostAddress
            0xAD, 0x2B, // DeviceID
            0x00, 0x00,
            0x44, 0x33, // PayloadChecksum
            0x00, 0x00,
            // Payload
            0x12, 0x34, 0x54, 0x32, 0x1F, 0xED, 0xCD, 0xEF
        ];

        SendPacket sut = new(new MockEndPoint(), header, payload);

        // Act
        Memory<byte> result = sut.GetBuffer();

        // Assert
        MemoryAssert.WriteTo(TestContext, expected, result);
        MemoryAssert.AreEqual(expected, result, nameof(result));

        Assert.AreEqual(inputDeviceType, sut.Header.DeviceType, nameof(sut.Header.DeviceType));
        Assert.AreEqual(inputPacketType, sut.Header.PacketType, nameof(sut.Header.PacketType));
        Assert.AreEqual(inputMessageCount, sut.Header.MessageCount, nameof(sut.Header.MessageCount));
        Assert.AreEqual(inputHostAddress, sut.Header.HostAddress, nameof(sut.Header.HostAddress));
        Assert.AreEqual(inputDeviceID, sut.Header.DeviceID, nameof(sut.Header.DeviceID));
        Assert.AreEqual(inputPacketChecksum, sut.Header.PacketChecksum, nameof(sut.Header.PacketChecksum));
        Assert.AreEqual(inputPayloadChecksum, sut.Header.PayloadChecksum, nameof(sut.Header.PayloadChecksum));
        Assert.AreEqual(0x38 + 8, sut.Size, nameof(sut.Size));

        MemoryAssert.AreEqual(expected.AsSpan(0, 8).ToArray(), sut.Header.Preamble, nameof(sut.Header.Preamble));
        MemoryAssert.AreEqual(inputPayload, sut.Payload.GetBuffer(), nameof(sut.Payload));

        Assert.AreEqual(-12557, sut.ComputeChecksum(), nameof(sut.ComputeChecksum));
    }

    [TestMethod]
    public void ResponsePacket_GetBuffer()
    {
        // Arrange
        byte[] input =
        [
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x78, 0x56, // NominalChecksum
            0xE0, 0xFF, // ErrorCode
                                    0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            // Payload
            0x12, 0x34, 0x54, 0x32, 0x1F, 0xED, 0xCD, 0xEF
        ];

        EndPoint inputEndPoint = IPEndPoint.Parse("10.20.30.40");

        // Act
        ResponsePacket sut = new(inputEndPoint, input);

        // Assert
        Assert.AreEqual(0x5678, sut.Header.NominalChecksum, nameof(sut.Header.NominalChecksum));
        Assert.AreEqual(-32, sut.Header.ErrorCode, nameof(sut.Header.ErrorCode));

        Assert.AreEqual(0x48, sut.Size, nameof(sut.Size));
        Assert.AreEqual(-15326, sut.ComputeChecksum(), nameof(sut.ComputeChecksum));
    }
}
