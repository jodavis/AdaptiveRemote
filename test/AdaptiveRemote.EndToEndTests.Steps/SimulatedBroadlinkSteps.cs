using AdaptiveRemote.EndtoEndTests;
using AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class SimulatedBroadlinkSteps : StepsBase
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
        bool found = WaitHelpers.ExecuteWithRetries(device.HasRecordedInboundPacketWithIrData, timeoutInSeconds: 10);

        if (!found)
        {
            string recordedPackets = device.GetRecordedPacketsDebugString();
            Assert.Fail($"Expected Broadlink device to record at least one inbound packet with IR data, but none were found. Recorded packets: {recordedPackets}");
        }

        Logger.LogInformation("Successfully verified Broadlink device recorded at least one inbound packet with IR data");
    }

    [Then(@"the recorded Broadlink packet's raw payload should not be empty")]
    public void ThenTheRecordedBroadlinkPacketRawPayloadShouldNotBeEmpty()
    {
        ISimulatedBroadlinkDevice? device = Environment.Broadlink;
        if (device == null)
        {
            Assert.Fail("Broadlink device is not running");
        }

        RecordedPacket? irPacket = device.GetFirstPacketWithIrData();

        if (irPacket == null)
        {
            Assert.Fail("No packet with IR payload was recorded");
        }

        Assert.IsTrue(irPacket.RawPayload!.Length > 0, "IR payload should not be empty");
        Logger.LogInformation("IR payload size: {PacketLength} bytes", irPacket.RawPayload.Length);
    }

    [Then(@"no Broadlink packets should be marked as malformed")]
    public void ThenNoBroadlinkPacketsShouldBeMarkedAsMalformed()
    {
        ISimulatedBroadlinkDevice? device = Environment.Broadlink;
        if (device == null)
        {
            Assert.Fail("Broadlink device is not running");
        }

        RecordedPacket? malformedPacket = device.GetFirstMalformedPacket();

        if (malformedPacket != null)
        {
            Assert.Fail($"Found malformed packet: {malformedPacket.DebugDescription}");
        }

        Logger.LogInformation("No malformed packets found");
    }
}
