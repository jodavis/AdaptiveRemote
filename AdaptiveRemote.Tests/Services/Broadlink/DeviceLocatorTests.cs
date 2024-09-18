using System.Net;
using AdaptiveRemote.TestUtilities;
using AdaptiveRemote.Utilities;
using Moq;

namespace AdaptiveRemote.Services.Broadlink;

[TestClass]
public class DeviceLocatorTests
{
    private static readonly Task IncompleteTask = new TaskCompletionSource().Task;

    private readonly Mock<IUdpService> MockUdpService = new();

    private DeviceLocator CreateSut() => new(MockUdpService.Object);

    [TestInitialize]
    public void SetupMocks()
    {
        MockUdpService
            .Setup(x => x.SendAsync(It.IsAny<SendPacket>(), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        MockUdpService
            .Setup(x => x.BroadcastAsync(It.IsAny<ScanRequestPacket>(), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        MockUdpService.Verify();
    }

    [TestMethod]
    public void DeviceLocator_FindDevice_ScansForDevicesAndReturnsFirstDevice()
    {
        // Arrange
        IDeviceLocator sut = CreateSut();

        ScanResponsePacket expectedPayload = new ScanResponsePacket(IPEndPoint.Parse("1.2.3.4:5"), new byte[0x100]);

        Expect_UdpService_BroadcastAsync(expectedPayload);

        // Act
        Task<ScanResponsePacket> resultTask = sut.FindDeviceAsync(default);

        // Assert
        TaskAssert.IsComplete(resultTask, nameof(resultTask));

        ScanResponsePacket result = resultTask.Result;
        Assert.IsNotNull(result, nameof(result));
        Assert.AreSame(expectedPayload, result, nameof(result));
    }

    [TestMethod]
    public void DeviceLocator_FindDevice_ThrowsExceptionIfDeviceNotFound()
    {
        // Arrange
        IDeviceLocator sut = CreateSut();

        ScanResponsePacket expectedPayload = new ScanResponsePacket(IPEndPoint.Parse("1.2.3.4:5"), new byte[0x100]);

        Expect_UdpService_BroadcastAsync();

        Exception expectedException = Errors.DeviceLocator_DeviceNotFound();

        // Act
        Task<ScanResponsePacket> resultTask = sut.FindDeviceAsync(default);

        // Assert
        TaskAssert.IsFaulted(resultTask, expectedException, nameof(resultTask));
    }

    [TestMethod]
    public void DeviceLocator_FindDevice_CancelsBroadcastAsyncWhenCancelled()
    {
        // Arrange
        IDeviceLocator sut = CreateSut();

        CancellationToken result = Expect_UdpService_BroadcastAsync_WaitForCancelled();

        CancellationTokenSource cts = new();
        Task<ScanResponsePacket> resultTask = sut.FindDeviceAsync(cts.Token);

        // Act
        cts.Cancel();

        // Assert
        TaskAssert.IsCanceled(resultTask, nameof(resultTask));
        Assert.IsTrue(result.IsCancellationRequested, nameof(result) + ".IsCancellationRequested");
    }

    [TestMethod]
    public void DeviceLocator_FindDevice_ReturnsCancelledWhenBroadcastAsyncCompletes()
    {
        // Arrange
        IDeviceLocator sut = CreateSut();

        CancellationToken result = Expect_UdpService_BroadcastAsync_WaitForCancelledAndComplete();

        CancellationTokenSource cts = new();
        Task<ScanResponsePacket> resultTask = sut.FindDeviceAsync(cts.Token);

        // Act
        cts.Cancel();

        // Assert
        TaskAssert.IsCanceled(resultTask, nameof(resultTask));
        Assert.IsTrue(result.IsCancellationRequested, nameof(result) + ".IsCancellationRequested");
    }

    [TestMethod]
    public void DeviceLocator_FindDevice_CancelsBroadcastAsyncWhenFirstDeviceFound()
    {
        // Arrange
        IDeviceLocator sut = CreateSut();

        ScanResponsePacket expectedPayload = new ScanResponsePacket(IPEndPoint.Parse("1.2.3.4:5"), new byte[0x100]);

        CancellationToken result = Expect_UdpService_BroadcastAsync_WaitForCancelled(expectedPayload);

        CancellationTokenSource cts = new();

        // Act
        Task<ScanResponsePacket> resultTask = sut.FindDeviceAsync(cts.Token);

        // Assert
        TaskAssert.IsComplete(resultTask, nameof(resultTask));
        Assert.IsTrue(result.IsCancellationRequested, nameof(result) + ".IsCancellationRequested");
    }

    private void Expect_UdpService_BroadcastAsync(params ScanResponsePacket[] responsePackets)
        => MockUdpService
            .Setup(x => x.BroadcastAsync(It.IsAny<ScanRequestPacket>(), It.IsAny<CancellationToken>()))
            .WithArgumentValidation("packet", delegate (ScanRequestPacket packet)
            {
                Assert.IsNotNull(packet, nameof(packet));
                Assert.AreEqual(0, packet.LocalPort, nameof(packet.LocalPort));
                MemoryAssert.AreEqual(new byte[4], packet.LocalIPAddress, nameof(packet.LocalIPAddress));
                Assert.AreEqual(0x30, packet.Size, nameof(packet.Size));
            })
            .Returns(EnumerateAsync(responsePackets, Task.CompletedTask))
            .Verifiable(Times.Once);

    private CancellationToken Expect_UdpService_BroadcastAsync_WaitForCancelled(params ScanResponsePacket[] responsePackets)
    {
        CancellationTokenSource result = new();
        MockUdpService
            .Setup(x => x.BroadcastAsync(It.IsAny<ScanRequestPacket>(), It.IsAny<CancellationToken>()))
            .Returns(delegate (ScanRequestPacket packet, CancellationToken cancellationToken)
            {
                TaskCompletionSource tcs = new();
                cancellationToken.Register(() =>
                {
                    tcs.SetCanceled();
                    result.Cancel();
                });
                return EnumerateAsync(responsePackets, tcs.Task);
            })
            .Verifiable(Times.Once);
        return result.Token;
    }

    private CancellationToken Expect_UdpService_BroadcastAsync_WaitForCancelledAndComplete(params ScanResponsePacket[] responsePackets)
    {
        CancellationTokenSource result = new();
        MockUdpService
            .Setup(x => x.BroadcastAsync(It.IsAny<ScanRequestPacket>(), It.IsAny<CancellationToken>()))
            .Returns(delegate (ScanRequestPacket packet, CancellationToken cancellationToken)
            {
                TaskCompletionSource tcs = new();
                cancellationToken.Register(() =>
                {
                    tcs.SetResult();
                    result.Cancel();
                });
                return EnumerateAsync(responsePackets, tcs.Task);
            })
            .Verifiable(Times.Once);
        return result.Token;
    }

    private static async IAsyncEnumerable<ElementType> EnumerateAsync<ElementType>(IEnumerable<ElementType> elements, Task finalTask)
    {
        foreach (ElementType element in elements)
        {
            yield return element;
        }

        await finalTask;
    }
}
