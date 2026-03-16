using AdaptiveRemote.EndtoEndTests;
using AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;
using FluentAssertions;
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

    [Then(@"the recorded Broadlink packet's raw payload should match the configured payload for '(.*)'")]
    public void ThenTheRecordedBroadlinkPacketPayloadShouldMatchConfiguredPayload(string commandName)
    {
        ISimulatedBroadlinkDevice? device = Environment.Broadlink;
        Assert.IsNotNull(device, "Broadlink device is not running");

        bool hasPayload = Environment.TestIrPayloads.TryGetValue(commandName, out byte[]? expectedPayload);
        Assert.IsTrue(hasPayload, "No test IR payload configured for command '{0}'", commandName);

        RecordedPacket? irPacket = device.GetFirstPacketWithIrData();
        Assert.IsNotNull(irPacket, "No packet with IR payload was recorded");

        CollectionAssert.AreEqual(
            expectedPayload,
            irPacket.RawPayload,
            $"IR payload for command '{commandName}' does not match configured test payload");

        Logger.LogInformation("IR payload for '{CommandName}' matches configured test payload ({PayloadLength} bytes)", commandName, expectedPayload!.Length);
    }

    [Then(@"the Broadlink device should be in learning mode")]
    public void ThenTheBroadlinkDeviceShouldBeInLearningMode()
    {
        ISimulatedBroadlinkDevice? device = Environment.Broadlink;
        Assert.IsNotNull(device, "Broadlink device is not running");

        bool entered = device.WaitForLearningMode(timeoutInSeconds: 10);

        Assert.IsTrue(entered, "Broadlink device did not enter learning mode within 10 seconds");
        Logger.LogInformation("Broadlink device is in learning mode");
    }

    [When(@"I send an IR signal to the Broadlink device")]
    public void WhenISendAnIRSignalToTheBroadlinkDevice()
    {
        ISimulatedBroadlinkDevice? device = Environment.Broadlink;
        Assert.IsNotNull(device, "Broadlink device is not running");

        // Use a fixed test payload for the simulated IR signal
        byte[] learnedData = Environment.NewlyLearnedIrData;
        device.ProvideLearnedData(learnedData);

        Logger.LogInformation("Simulated IR signal sent to Broadlink device ({Length} bytes)", learnedData.Length);
    }

    [Then(@"the recorded Broadlink packet's raw payload should match the newly learned data")]
    public void ThenTheRecordedBroadlinkPacketPayloadShouldMatchNewlyLearnedData()
    {
        ISimulatedBroadlinkDevice? device = Environment.Broadlink;
        Assert.IsNotNull(device, "Broadlink device is not running");

        RecordedPacket? irPacket = device.GetFirstPacketWithIrData();
        Assert.IsNotNull(irPacket, "No packet with IR payload was recorded");

        irPacket.RawPayload.Should().BeEquivalentTo(Environment.NewlyLearnedIrData,
            because: "the application should have learned the new data");

        Logger.LogInformation("IR payload matches newly learned data ({Length} bytes)", Environment.NewlyLearnedIrData.Length);
    }

    [When(@"the Broadlink device simulates a device error")]
    public void WhenTheBroadlinkDeviceSimulatesADeviceError()
    {
        ISimulatedBroadlinkDevice? device = Environment.Broadlink;
        Assert.IsNotNull(device, "Broadlink device is not running");

        // Simulate device offline error code
        device.SimulateNextCheckError(-3);

        Logger.LogInformation("Broadlink device configured to return error on next CheckLearnedData poll");
    }

    [When(@"I clear the Broadlink recorded packets")]
    public void WhenIClearTheBroadlinkRecordedPackets()
    {
        ISimulatedBroadlinkDevice? device = Environment.Broadlink;
        Assert.IsNotNull(device, "Broadlink device is not running");

        device.ClearRecordedPackets();
        Logger.LogInformation("Cleared Broadlink recorded packets");
    }
}
