using System.Net;
using System.Net.NetworkInformation;
using FluentAssertions;
using Moq;

namespace AdaptiveRemote.Services.Broadlink;

[TestClass]
public class DeviceConnectionTests
{
    private readonly Mock<IUdpService> MockUdpService = new();
    private readonly Mock<IEncryption.Factory> MockEncryptionFactory = new();
    private readonly Mock<IEncryption> MockEncryption = new();

    private static readonly IPEndPoint HostEndPoint = IPEndPoint.Parse("10.20.30.40:5566");
    private static readonly PhysicalAddress HostAddress = PhysicalAddress.Parse("AA:BB:CC:DD:EE:FF");
    private const short DeviceType = 0x1234;

    private DeviceConnection CreateSut()
    {
        MockEncryptionFactory
            .SetupGet(x => x.Default)
            .Returns(MockEncryption.Object);

        return new(HostEndPoint, HostAddress, DeviceType, MockUdpService.Object, MockEncryptionFactory.Object);
    }

    [TestInitialize]
    public void SetupMocks()
    {
        // Default mock: encryption is an identity (no-op)
        MockEncryption
            .Setup(x => x.Encrypt(It.IsAny<Memory<byte>>()))
            .Returns((Memory<byte> m) => m);
        MockEncryption
            .Setup(x => x.Decrypt(It.IsAny<Memory<byte>>()))
            .Returns((Memory<byte> m) => m);
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        MockUdpService.Verify();
        MockEncryptionFactory.Verify();
        MockEncryption.Verify();
    }

    [TestMethod]
    public void DeviceConnection_EnterLearningModeAsync_SendsEnterLearningCommand()
    {
        // Arrange
        IDeviceConnection sut = CreateSut();

        ResponsePacket successResponse = BuildSuccessResponsePacket(errorCode: 0);
        Expect_SendAsync_WithCommandCode(EnterLearningModeCommandCode, successResponse);

        // Act
        Task result = sut.EnterLearningModeAsync(default);

        // Assert
        result.Should().BeComplete(because: "EnterLearningModeAsync should complete when device acknowledges");
    }

    [TestMethod]
    public void DeviceConnection_EnterLearningModeAsync_WhenDeviceReturnsError_ThrowsBroadlinkException()
    {
        // Arrange
        IDeviceConnection sut = CreateSut();

        ResponsePacket errorResponse = BuildSuccessResponsePacket(errorCode: -1); // AuthenticationFailed
        Expect_SendAsync_WithCommandCode(EnterLearningModeCommandCode, errorResponse);

        // Act
        Task result = sut.EnterLearningModeAsync(default);

        // Assert
        result.Should().BeFaultedWith(Errors.Broadlink_AuthenticationFailed(),
            because: "a device error code should be translated to a BroadlinkException");
    }

    [TestMethod]
    public void DeviceConnection_CheckLearnedDataAsync_WhenNoDataYet_ReturnsNull()
    {
        // Arrange
        IDeviceConnection sut = CreateSut();

        ResponsePacket noDataResponse = BuildSuccessResponsePacket(errorCode: -12); // NoDataYet
        Expect_SendAsync_WithCommandCode(CheckLearnedDataCommandCode, noDataResponse);

        // Act
        Task<byte[]?> result = sut.CheckLearnedDataAsync(default);

        // Assert
        result.Should().BeComplete(because: "CheckLearnedDataAsync should complete even when no data is ready");
        result.Should().HaveResult(null, because: "error code -12 means no IR signal has been captured yet");
    }

    [TestMethod]
    public void DeviceConnection_CheckLearnedDataAsync_WhenDataAvailable_ReturnsIRData()
    {
        // Arrange
        IDeviceConnection sut = CreateSut();

        byte[] expectedIrData = [0x26, 0x00, 0x11, 0x22, 0x33, 0x44];
        ResponsePacket dataResponse = BuildLearnedDataResponsePacket(expectedIrData);
        Expect_SendAsync_WithCommandCode(CheckLearnedDataCommandCode, dataResponse);

        // Act
        Task<byte[]?> result = sut.CheckLearnedDataAsync(default);

        // Assert
        result.Should().BeComplete(because: "CheckLearnedDataAsync should complete when IR data is available");
        MemoryAssert.AreEqual(expectedIrData, result.Result!, nameof(result.Result));
    }

    [TestMethod]
    public void DeviceConnection_CheckLearnedDataAsync_WhenDeviceReturnsError_ThrowsBroadlinkException()
    {
        // Arrange
        IDeviceConnection sut = CreateSut();

        ResponsePacket errorResponse = BuildSuccessResponsePacket(errorCode: -8); // SendError
        Expect_SendAsync_WithCommandCode(CheckLearnedDataCommandCode, errorResponse);

        // Act
        Task<byte[]?> result = sut.CheckLearnedDataAsync(default);

        // Assert
        result.Should().BeFaultedWith(Errors.Broadlink_SendError(),
            because: "a device error code should be translated to a BroadlinkException");
    }

    [TestMethod]
    public void DeviceConnection_EnterLearningModeAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        IDeviceConnection sut = CreateSut();

        CancellationTokenSource cts = new();

        MockUdpService
            .Setup(x => x.SendAsync(It.IsAny<SendPacket>(), It.IsAny<CancellationToken>()))
            .WithExpectedCancellation(throwWhenCancelled: true);

        // Act
        Task result = sut.EnterLearningModeAsync(cts.Token);
        cts.Cancel();

        // Assert
        result.Should().BeCanceledWithin(TimeSpan.FromMilliseconds(500),
            because: "EnterLearningModeAsync should be cancelled when the cancellation token fires");
    }

    [TestMethod]
    public void DeviceConnection_CheckLearnedDataAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        IDeviceConnection sut = CreateSut();

        CancellationTokenSource cts = new();

        MockUdpService
            .Setup(x => x.SendAsync(It.IsAny<SendPacket>(), It.IsAny<CancellationToken>()))
            .WithExpectedCancellation(throwWhenCancelled: true);

        // Act
        Task<byte[]?> result = sut.CheckLearnedDataAsync(cts.Token);
        cts.Cancel();

        // Assert
        result.Should().BeCanceledWithin(TimeSpan.FromMilliseconds(500),
            because: "CheckLearnedDataAsync should be cancelled when the cancellation token fires");
    }

    // Command codes matching DeviceConnection constants
    private const int EnterLearningModeCommandCode = 3;
    private const int CheckLearnedDataCommandCode = 4;

    private void Expect_SendAsync_WithCommandCode(int expectedCommandCode, ResponsePacket response)
        => MockUdpService
            .Setup(x => x.SendAsync(It.IsAny<SendPacket>(), It.IsAny<CancellationToken>()))
            .WithArgumentValidation("packet", delegate (SendPacket packet)
            {
                // The encrypted payload bytes (with identity encryption) match the raw CommandPayload bytes.
                // CommandPayload layout: [commandAndDataLength (2 bytes)][command (4 bytes)][data...]
                // Command code is at offset 2 in the payload
                Memory<byte> payloadBuffer = packet.Payload.GetBuffer();
                Assert.IsTrue(payloadBuffer.Length >= 6, "Payload should be at least 6 bytes");
                int actualCommandCode = BitConverter.ToInt32(payloadBuffer.Slice(2, 4).Span);
                Assert.AreEqual(expectedCommandCode, actualCommandCode,
                    "Expected command code {0} but got {1}", expectedCommandCode, actualCommandCode);
            })
            .WithStandardTaskBehavior(response)
            .Verifiable(Times.Once);

    /// <summary>Builds a ResponsePacket with a specific error code and empty payload.</summary>
    private static ResponsePacket BuildSuccessResponsePacket(short errorCode)
    {
        byte[] bytes = new byte[0x38]; // Header only, no extra payload
        // ErrorCode is at offset 0x22 in the full response (within the 0x38-byte header)
        BitConverter.GetBytes(errorCode).CopyTo(bytes, 0x22);
        return new ResponsePacket(HostEndPoint, bytes);
    }

    /// <summary>Builds a ResponsePacket containing LearnedDataResponsePayload.</summary>
    private static ResponsePacket BuildLearnedDataResponsePacket(byte[] irData)
    {
        // LearnedDataResponsePayload: [commandAndDataLength (2)][command=4 (4)][irData...]
        byte[] payloadBytes = new byte[2 + 4 + irData.Length];
        short commandAndDataLength = (short)(4 + irData.Length);
        BitConverter.GetBytes(commandAndDataLength).CopyTo(payloadBytes, 0);
        BitConverter.GetBytes(4).CopyTo(payloadBytes, 2);
        irData.CopyTo(payloadBytes, 6);

        byte[] responseBytes = new byte[0x38 + payloadBytes.Length];
        payloadBytes.CopyTo(responseBytes, 0x38);

        return new ResponsePacket(HostEndPoint, responseBytes);
    }
}
