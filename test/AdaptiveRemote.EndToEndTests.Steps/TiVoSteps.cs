using AdaptiveRemote.EndtoEndTests;
using AdaptiveRemote.EndtoEndTests.SimulatedTiVo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class TiVoSteps : StepsBase
{
    [Then(@"I should see the TiVo receives a {string} message")]
    public void ThenIShouldSeeTheTiVoReceivesAMessage(string expectedCommand)
    {
        ISimulatedTiVoDevice? device = Environment.TiVo;
        if (device == null)
        {
            Assert.Fail("TiVo device is not running");
        }

        // Poll for the message with a timeout of 5 seconds
        // Look for the full IRCODE command format for more precise matching
        bool found = WaitHelpers.ExecuteWithRetries(
            () =>
            {
                IReadOnlyList<RecordedMessage> messages = device.GetRecordedMessages();
                return messages.Any(m => m.Incoming && m.Payload.Equals($"IRCODE {expectedCommand}", StringComparison.OrdinalIgnoreCase));
            },
            timeoutInSeconds: 5);

        if (!found)
        {
            IReadOnlyList<RecordedMessage> messages = device.GetRecordedMessages();
            string recordedMessages = string.Join(", ", messages.Select(m => $"[{m.Timestamp:HH:mm:ss.fff}] {(m.Incoming ? "←" : "→")} {m.Payload}"));
            Assert.Fail($"Expected TiVo to receive message 'IRCODE {expectedCommand}', but it was not found. Recorded messages: {recordedMessages}");
        }

        TestContext.WriteLine($"Successfully verified TiVo received message: IRCODE {expectedCommand}");
    }
}
