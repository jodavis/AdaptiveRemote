using AdaptiveRemote.EndtoEndTests;
using AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class BroadlinkSteps : StepsBase
{
    [Then(@"I should see the Broadlink device recorded at least one inbound packet")]
    public void ThenIShouldSeeTheBroadlinkDeviceRecordedAtLeastOneInboundPacket()
    {
        ISimulatedBroadlinkDevice? device = Environment.Broadlink;
        if (device == null)
        {
            Assert.Fail("Broadlink device is not running");
        }

        // Poll for packets with a timeout of 10 seconds
        bool found = WaitHelpers.ExecuteWithRetries(
            () =>
            {
                IReadOnlyList<RecordedPacket> packets = device.GetRecordedPackets();
                return packets.Any(p => p.IsInbound && p.RawPayload != null && p.RawPayload.Length > 0);
            },
            timeoutInSeconds: 10);

        if (!found)
        {
            IReadOnlyList<RecordedPacket> packets = device.GetRecordedPackets();
            string recordedPackets = string.Join(", ", packets.Select(p =>
                $"[{p.ReceivedAt:HH:mm:ss.fff}] {(p.IsInbound ? "←" : "→")} {p.DebugDescription}"));
            Assert.Fail($"Expected Broadlink device to record at least one inbound packet with IR data, but none were found. Recorded packets: {recordedPackets}");
        }

        TestContext.WriteLine("Successfully verified Broadlink device recorded inbound packet with IR data");
    }

    [Then(@"the recorded Broadlink packet's raw payload should not be empty")]
    public void ThenTheRecordedBroadlinkPacketRawPayloadShouldNotBeEmpty()
    {
        ISimulatedBroadlinkDevice? device = Environment.Broadlink;
        if (device == null)
        {
            Assert.Fail("Broadlink device is not running");
        }

        IReadOnlyList<RecordedPacket> packets = device.GetRecordedPackets();
        RecordedPacket? irPacket = packets.FirstOrDefault(p => p.IsInbound && p.RawPayload != null);

        if (irPacket == null)
        {
            Assert.Fail("No packet with IR payload was recorded");
        }

        Assert.IsTrue(irPacket.RawPayload!.Length > 0, "IR payload should not be empty");
        TestContext.WriteLine($"IR payload size: {irPacket.RawPayload.Length} bytes");
    }

    [Then(@"no Broadlink packets should be marked as malformed")]
    public void ThenNoBroadlinkPacketsShouldBeMarkedAsMalformed()
    {
        ISimulatedBroadlinkDevice? device = Environment.Broadlink;
        if (device == null)
        {
            Assert.Fail("Broadlink device is not running");
        }

        IReadOnlyList<RecordedPacket> packets = device.GetRecordedPackets();
        RecordedPacket? malformedPacket = packets.FirstOrDefault(p => p.IsMalformed);

        if (malformedPacket != null)
        {
            Assert.Fail($"Found malformed packet: {malformedPacket.DebugDescription}");
        }

        TestContext.WriteLine("No malformed packets found");
    }
}
