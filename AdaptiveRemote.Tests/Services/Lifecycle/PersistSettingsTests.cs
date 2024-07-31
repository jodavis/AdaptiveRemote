using AdaptiveRemote.Logging;
using AdaptiveRemote.Services.Configuration;
using AdaptiveRemote.TestUtilities;

namespace AdaptiveRemote.Services.Lifecycle;

[TestClass]
public class PersistSettingsTests
{
    private const string InputSettingsPath = @"C:\path\to\settings.ini";

    private readonly MockLogger<PersistSettings> MockLogger = new();
    private readonly MockFileSystem MockFileSystem = new();
    private readonly MockOptions<HostSettings> MockOptions = new(new()
    {
        ProgrammaticSettingsPath = InputSettingsPath
    });

    private PersistSettings CreateSut() => new(MockFileSystem.Object, MockOptions, MockLogger);

    [TestMethod]
    public async Task PersistSettings_Set_SavesSettingsToFile()
    {
        // Arrange
        IPersistSettings sut = CreateSut();

        MockFileSystem.AddFile(InputSettingsPath, "ExistingSetting=123");

        MockFileSystem.Expect_OpenRead_ForPath(InputSettingsPath);
        MockFileSystem.Expect_OpenWrite_ForPath(InputSettingsPath);

        // Act
        sut.Set("NewSetting", "abc");

        await MockLogger.WaitForMessage(ExpectMessage_SavedSettings());

        // Assert
        MockFileSystem.VerifyFileContents(InputSettingsPath, "ExistingSetting=123\r\nNewSetting=abc\r\n");

        MockLogger.VerifyMessages(
            ExpectMessage_LoadingExistingSettings(),
            ExpectMessage_LoadedExistingSettings(1),
            ExpectMessage_AddSetting("NewSetting", "abc"),
            ExpectMessage_SavingSettings(2),
            ExpectMessage_SavedSettings());
    }

    private static string ExpectMessage_LoadingExistingSettings()
        => $"Information[1101]: {string.Format(LoggingMessages.ProgrammaticSettings_LoadingExistingSettings, InputSettingsPath)}";
    private static string ExpectMessage_LoadedExistingSettings(int expectedCount)
        => $"Information[1102]: {string.Format(LoggingMessages.ProgrammaticSettings_LoadedExistingSettings, expectedCount, InputSettingsPath)}";
    private static string ExpectMessage_SavingSettings(int expectedCount)
        => $"Information[1103]: {string.Format(LoggingMessages.ProgrammaticSettings_SavingSettings, 2, InputSettingsPath)}";
    private static string ExpectMessage_SavedSettings()
        => $"Information[1104]: {string.Format(LoggingMessages.ProgrammaticSettings_SavedSettings, InputSettingsPath)}";
    private static string ExpectMessage_AddSetting(string expectedKey, string expectedValue)
        => $"Information[1105]: {string.Format(LoggingMessages.ProgrammaticSettings_AddSetting, expectedKey, expectedValue)}";
}
