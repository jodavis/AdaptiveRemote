using AdaptiveRemote.EndtoEndTests;
using AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;
using AdaptiveRemote.TestUtilities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps.Application;

[Binding]
public class SimulatedBroadlinkSteps : StepsBase
{
    /// <summary>
    /// Converts the phrase "the IR signal for {commandName}" in step arguments to a test IR payload.
    /// </summary>
    [StepArgumentTransformation("the IR signal for (.*)")]
    public static byte[] TransformIrSignalPhrase(string commandName)
        => commandName switch
        {
            "Volume Down" => [0xAA, 0xBB, 0xCC, 0xDD],
            "Power" => [0x01, 0x02, 0x03, 0x04],
            _ => throw new ArgumentException($"No test IR payload configured for command '{commandName}'")
        };

    [Then(@"I should see the Broadlink device recorded at least one inbound packet")]
    public void ThenIShouldSeeTheBroadlinkDeviceRecordedAtLeastOneInboundPacket()
    {
        ISimulatedBroadlinkDevice device = Environment.Broadlink;

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
        RecordedPacket? irPacket = Environment.Broadlink.GetFirstPacketWithIrData();

        Assert.IsNotNull(irPacket, "No packet with IR payload was recorded");
        Assert.IsTrue(irPacket.RawPayload!.Length > 0, "IR payload should not be empty");
        Logger.LogInformation("IR payload size: {PacketLength} bytes", irPacket.RawPayload.Length);
    }

    [Then(@"no Broadlink packets should be marked as malformed")]
    public void ThenNoBroadlinkPacketsShouldBeMarkedAsMalformed()
    {
        RecordedPacket? malformedPacket = Environment.Broadlink.GetFirstMalformedPacket();

        Assert.IsNull(malformedPacket, $"Found malformed packet: {malformedPacket?.DebugDescription}");
        Logger.LogInformation("No malformed packets found");
    }

    [Then(@"the recorded Broadlink packet's raw payload should match the configured payload for '(.*)'")]
    public void ThenTheRecordedBroadlinkPacketPayloadShouldMatchConfiguredPayload(string commandName)
    {
        bool hasPayload = Environment.TestIrPayloads.TryGetValue(commandName, out byte[]? expectedPayload);
        Assert.IsTrue(hasPayload, "No test IR payload configured for command '{0}'", commandName);

        RecordedPacket? irPacket = Environment.Broadlink.GetFirstPacketWithIrData();
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
        bool entered = Environment.Broadlink.WaitForLearningMode(timeoutInSeconds: 10);

        Assert.IsTrue(entered, "Broadlink device did not enter learning mode within 10 seconds");
        Logger.LogInformation("Broadlink device is in learning mode");
    }

    [When(@"I send (.*) to the Broadlink device")]
    public void WhenISendIRSignalToTheBroadlinkDevice(byte[] irData)
    {
        Environment.Broadlink.ProvideLearnedData(irData);

        Logger.LogInformation("Simulated IR signal sent to Broadlink device ({Length} bytes)", irData.Length);
    }

    [Then(@"I should see the Broadlink device sent (.*)")]
    public void ThenIShouldSeeBroadlinkDeviceSentSignal(byte[] expectedData)
    {
        // Wait for at least one inbound IR packet
        bool found = WaitHelpers.ExecuteWithRetries(Environment.Broadlink.HasRecordedInboundPacketWithIrData, timeoutInSeconds: 10);
        Assert.IsTrue(found, "Broadlink device did not receive any inbound IR packets");

        // Validate no malformed packets
        RecordedPacket? malformedPacket = Environment.Broadlink.GetFirstMalformedPacket();
        Assert.IsNull(malformedPacket, $"Found malformed packet: {malformedPacket?.DebugDescription}");

        // Validate packet payload matches expected signal
        RecordedPacket? irPacket = Environment.Broadlink.GetFirstPacketWithIrData();
        Assert.IsNotNull(irPacket, "No packet with IR payload was recorded");

        irPacket.RawPayload.Should().BeEquivalentTo(expectedData,
            because: "the Broadlink device should have sent the correct IR signal");

        Logger.LogInformation("Broadlink device sent correct IR signal ({Length} bytes)", expectedData.Length);
    }

    [When(@"the Broadlink device simulates a device error")]
    public void WhenTheBroadlinkDeviceSimulatesADeviceError()
    {
        // Simulate device offline error code
        Environment.Broadlink.SimulateNextCheckError(-3);

        Logger.LogInformation("Broadlink device configured to return error on next CheckLearnedData poll");
    }
}
