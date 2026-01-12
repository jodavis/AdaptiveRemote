using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using AdaptiveRemote.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AdaptiveRemote.Services.Broadlink;

[TestClass]
public class UdpServiceTests
{
    private readonly Mock<ISocket.Factory> MockSocketFactory = new();
    private readonly Mock<ISocket> MockSocket = new();
    private readonly BroadlinkSettings Settings = new();
    private readonly MockLogger<UdpService> MockLogger = new();

    public TestContext? TestContext { get; set; }

    [TestInitialize]
    public void SetupMocks()
    {
        MockSocketFactory
            .Setup(x => x.Create())
            .Verifiable(Times.Never);
        MockSocketFactory
            .Setup(x => x.CreateForBroadcast())
            .Verifiable(Times.Never);
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        MockSocketFactory.Verify();
        MockSocket.Verify();
    }

    private UdpService CreateSut()
    {
        Mock<IOptions<BroadlinkSettings>> mockOptions = new();
        mockOptions
            .SetupGet(x => x.Value)
            .Returns(Settings);

        return new(MockSocketFactory.Object, mockOptions.Object, MockLogger);
    }

    [TestMethod]
    public void UdpService_SendAsync_CreatesSocketSendsPacketAndReturnsResponse()
    {
        // Arrange
        IUdpService sut = CreateSut();

        EndPoint inputEndPoint = IPEndPoint.Parse("192.168.10.20:4321");
        SendPacket inputPacket = new(
            inputEndPoint,
            new()
            {
                DeviceID = 1,
                DeviceType = 2,
                HostAddress = new PhysicalAddress([0x12, 0x23, 0x34, 0x45, 0x56, 0x67]),
                MessageCount = 3,
                PacketChecksum = 4,
                PacketType = 5,
                PayloadChecksum = 6,
            },
            new(new byte[] { 0x01, 0x23, 0x45, 0x67 }));
        ResponsePacket expectedResponse = new(
            inputEndPoint,
            new byte[]
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x33, 0xC5, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0xAB, 0xBC, 0xCD, 0xDE, 0xEF, 0xED, 0xDC, 0xBA,
            });

        Expect_SocketFactory_Create();
        Expect_Socket_SendTo(inputPacket.GetBuffer());
        Expect_Socket_ReadFrom(expectedResponse.GetBuffer(), inputEndPoint);

        // Act
        Task<ResponsePacket> resultTask = sut.SendAsync(inputPacket, default);

        // Assert
        resultTask.Should().BeComplete(because: "SendAsync should complete and return the response packet");

        ResponsePacket result = resultTask.Result;
        Assert.IsNotNull(result, nameof(result));

        MemoryAssert.WriteTo(TestContext, nameof(result) + ".GetBuffer()", expectedResponse.GetBuffer(), result.GetBuffer());
        MemoryAssert.AreEqual(expectedResponse.GetBuffer(), result.GetBuffer(), nameof(result) + ".GetBuffer()");

        Assert.AreEqual(inputEndPoint, result.RemoteEndPoint, nameof(result.RemoteEndPoint));

        MockLogger.VerifyMessages(
            ExpectMessage_Sending(inputPacket, inputPacket.Size, inputEndPoint),
            ExpectMessage_Sent(inputPacket),
            ExpectMessage_ReceivedResponse(inputPacket, expectedResponse.Size, inputEndPoint));
    }

    private void Expect_SocketFactory_Create()
        => MockSocketFactory
            .Setup(x => x.Create())
            .Returns(MockSocket.Object)
            .Verifiable(Times.Once);

    private void Expect_Socket_SendTo(ReadOnlyMemory<byte> expectedBytes)
        => MockSocket
            .Setup(x => x.SendToAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<EndPoint>(), It.IsAny<CancellationToken>()))
            .WithArgumentValidation("packet", delegate (ReadOnlyMemory<byte> actualBytes)
            {
                MemoryAssert.WriteTo(TestContext, "packet for SendToAsync", expectedBytes, actualBytes);
                MemoryAssert.AreEqual(expectedBytes, actualBytes, "packet for SendToAsync");

                Assert.IsTrue(actualBytes.Span.SequenceEqual(expectedBytes.Span));
            })
            .WithStandardTaskBehavior(returnValue: expectedBytes.Length)
            .Verifiable(Times.Once);

    private void Expect_Socket_ReadFrom(ReadOnlyMemory<byte> responseBytes, EndPoint responseEndPoint)
        => MockSocket
            .Setup(x => x.ReceiveFromAsync(It.IsAny<Memory<byte>>(), It.IsAny<EndPoint>(), It.IsAny<CancellationToken>()))
            .WithArgumentValidation("buffer", delegate (Memory<byte> responseBuffer)
            {
                Assert.IsNotNull(responseBuffer, nameof(responseBuffer));
                Assert.IsTrue(responseBuffer.Length >= responseBytes.Length, "Response buffer for {0} must be at least {1} bytes, but was only {2} bytes",
                    nameof(ISocket.ReceiveFromAsync),
                    responseBytes.Length,
                    responseBuffer.Length);

                responseBytes.CopyTo(responseBuffer);
            })
            .WithStandardTaskBehavior(new SocketReceiveFromResult() { ReceivedBytes = responseBytes.Length, RemoteEndPoint = responseEndPoint })
            .Verifiable(Times.Once);

    private static string ExpectMessage_Sending(Payload packet, int bytesInPacket, EndPoint remoteEndPoint)
        => $"Information[901]: {string.Format(LoggingMessages.UdpService_Sending, packet, bytesInPacket, remoteEndPoint)}";
    private static string ExpectMessage_Sent(Payload packet)
        => $"Information[902]: {string.Format(LoggingMessages.UdpService_Sent, packet)}";
    private static string ExpectMessage_ReceivedResponse(Payload packet, int bytesInResponse, EndPoint remoteEndPoint)
        => $"Information[903]: {string.Format(LoggingMessages.UdpService_ReceivedResponse, packet, bytesInResponse, remoteEndPoint)}";
}
