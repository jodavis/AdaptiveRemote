using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using AdaptiveRemote.Services.Configuration;
using AdaptiveRemote.TestUtilities;
using Moq;

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

    public TestContext? TestContext
    {
        get => MockLogger.OutputWriter;
        set => MockLogger.OutputWriter = value;
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        MockFileSystem.Verify();
    }

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

    [TestMethod]
    public async Task PersistSettings_Set_OnFailureToLoadExistingFile_LogsError()
    {
        // Arrange
        IPersistSettings sut = CreateSut();

        Exception expectedException = new ArgumentOutOfRangeException();

        MockFileSystem.AddFile(InputSettingsPath);
        MockFileSystem
            .Setup(x => x.OpenRead(InputSettingsPath))
            .Throws(expectedException)
            .Verifiable(Times.Once);

        // Act
        sut.Set("NewSetting", "abc");

        await MockLogger.WaitForMessage(ExpectMessage_Error("NewSetting", "abc", expectedException));

        // Assert
        MockLogger.VerifyMessages(
            ExpectMessage_LoadingExistingSettings(),
            ExpectMessage_Error("NewSetting", "abc", expectedException));
    }

    [TestMethod]
    public async Task PersistSettings_Set_OnFailureToSaveFile_LogsError()
    {
        // Arrange
        IPersistSettings sut = CreateSut();

        Exception expectedException = new ArgumentOutOfRangeException();

        MockFileSystem.AddFile(InputSettingsPath, "ExistingSetting=123");

        MockFileSystem.Expect_OpenRead_ForPath(InputSettingsPath);
        MockFileSystem
            .Setup(x => x.OpenWrite(InputSettingsPath))
            .Throws(expectedException)
            .Verifiable(Times.Once);

        // Act
        sut.Set("NewSetting", "abc");

        await MockLogger.WaitForMessage(ExpectMessage_Error("NewSetting", "abc", expectedException));

        // Assert
        MockLogger.VerifyMessages(
            ExpectMessage_LoadingExistingSettings(),
            ExpectMessage_LoadedExistingSettings(1),
            ExpectMessage_AddSetting("NewSetting", "abc"),
            ExpectMessage_SavingSettings(2),
            ExpectMessage_Error("NewSetting", "abc", expectedException));
    }

    [TestMethod]
    public async Task PersistSettings_Set_WhenFileNotFound_CreatesFile()
    {
        // Arrange
        IPersistSettings sut = CreateSut();

        MockFileSystem.AddDirectory(Path.GetDirectoryName(InputSettingsPath));

        MockFileSystem.Expect_OpenRead_IsNotCalled();
        MockFileSystem.Expect_OpenWrite_ForPath(InputSettingsPath);

        // Act
        sut.Set("NewSetting", "abc");

        await MockLogger.WaitForMessage(ExpectMessage_SavedSettings());

        // Assert
        MockFileSystem.VerifyFileContents(InputSettingsPath, "NewSetting=abc\r\n");

        MockLogger.VerifyMessages(
            ExpectMessage_AddSetting("NewSetting", "abc"),
            ExpectMessage_SavingSettings(1),
            ExpectMessage_SavedSettings());
    }

    [TestMethod]
    public async Task PersistSettings_Set_WhenFileNotFound_CreatesDirectory()
    {
        // Arrange
        IPersistSettings sut = CreateSut();

        MockFileSystem.AddDirectory(Path.GetPathRoot(InputSettingsPath));

        MockFileSystem.Expect_OpenRead_IsNotCalled();

        MockFileSystem.Expect_CreateDirectory_ForPath(@"C:\path");
        MockFileSystem.Expect_CreateDirectory_ForPath(@"C:\path\to");

        MockFileSystem.Expect_OpenWrite_ForPath(InputSettingsPath);

        // Act
        sut.Set("NewSetting", "abc");

        await MockLogger.WaitForMessage(ExpectMessage_SavedSettings());

        // Assert
        MockFileSystem.VerifyFileContents(InputSettingsPath, "NewSetting=abc\r\n");

        MockLogger.VerifyMessages(
            ExpectMessage_AddSetting("NewSetting", "abc"),
            ExpectMessage_SavingSettings(1),
            ExpectMessage_SavedSettings());
    }

    [DataTestMethod]
    [DataRow("Hello", true)] // Accepts letters
    [DataRow("this:Valid", true)] // Accepts colons as separators
    [DataRow("this::Invalid", false)] // Rejects double colon
    [DataRow(":Invalid", false)] // Rejects leading colon
    [DataRow("Invalid:", false)] // Rejects trailing colon
    [DataRow("", false)] // Rejects empty string
    [DataRow("$Hello", false)] // Rejects special characters
    [DataRow("@Hello", false)] // Rejects special characters
    [DataRow("Hello=", false)] // Rejects special characters
    [DataRow("%", false)] // Rejects special characters
    [DataRow("Hello?", false)] // Rejects special characters
    [DataRow("Hello!", false)] // Rejects special characters
    [DataRow("Hi There", false)] // Rejects spaces
    [DataRow("Hello\n", false)] // Rejects newline
    public async Task PersistSettings_Set_ValidatesKeyName(string input, bool expectedResult)
    {
        // Arrange
        IPersistSettings sut = CreateSut();

        MockFileSystem.AddFile(InputSettingsPath, "");

        try
        {
            // Act
            sut.Set(input, "def");

            // Assert
            Assert.IsTrue(expectedResult, "Expected ArgumentException was not thrown for input:'{0}'.", input);

            await MockLogger.WaitForMessage(ExpectMessage_SavedSettings());

            MockLogger.VerifyMessages(
                ExpectMessage_LoadingExistingSettings(),
                ExpectMessage_LoadedExistingSettings(0),
                ExpectMessage_AddSetting(input, "def"),
                ExpectMessage_SavingSettings(1),
                ExpectMessage_SavedSettings());
        }
        catch (ArgumentException result) when (!expectedResult)
        {
            ArgumentException expectedException = (ArgumentException)Errors.PersistSettings_InvalidName("name", input);

            Assert.AreEqual(expectedException.ParamName, result.ParamName, nameof(result.ParamName) + " for input:'{0}'", input);
            Assert.AreEqual(expectedException.Message, result.Message, nameof(result.Message) + " for input:'{0}'", input);

            MockLogger.VerifyMessages(
                ExpectMessage_Rejected(input, "def", expectedException.Message));
        }
    }

    [DataTestMethod]
    [DataRow("Valid", true)]
    [DataRow("Valid with spaces", true)]
    [DataRow("", true)]
    [DataRow("Invalid\n", false)] // Rejects newline
    [DataRow("Invalid\r", false)] // Rejects carriage return
    public async Task PersistSettings_Set_ValidatesValue(string input, bool expectedResult)
    {
        // Arrange
        IPersistSettings sut = CreateSut();

        MockFileSystem.AddFile(InputSettingsPath, "");

        try
        {
            // Act
            sut.Set("TestSetting", input);

            // Assert
            Assert.IsTrue(expectedResult, "Expected ArgumentException was not thrown for input:'{0}'.", input);

            await MockLogger.WaitForMessage(ExpectMessage_SavedSettings());

            MockLogger.VerifyMessages(
                ExpectMessage_LoadingExistingSettings(),
                ExpectMessage_LoadedExistingSettings(0),
                ExpectMessage_AddSetting("TestSetting", input),
                ExpectMessage_SavingSettings(1),
                ExpectMessage_SavedSettings());
        }
        catch (ArgumentException result) when (!expectedResult)
        {
            ArgumentException expectedException = (ArgumentException)Errors.PersistSettings_InvalidValue("value", input);

            Assert.AreEqual(expectedException.ParamName, result.ParamName, nameof(result.ParamName) + " for input:'{0}'", input);
            Assert.AreEqual(expectedException.Message, result.Message, nameof(result.Message) + " for input:'{0}'", input);

            MockLogger.VerifyMessages(
                ExpectMessage_Rejected("TestSetting", input, expectedException.Message));
        }
    }

    private static string ExpectMessage_LoadingExistingSettings()
        => $"Information[1101]: {string.Format(LoggingMessages.ProgrammaticSettings_LoadingExistingSettings, InputSettingsPath)}";
    private static string ExpectMessage_LoadedExistingSettings(int expectedCount)
        => $"Information[1102]: {string.Format(LoggingMessages.ProgrammaticSettings_LoadedExistingSettings, expectedCount, InputSettingsPath)}";
    private static string ExpectMessage_SavingSettings(int expectedCount)
        => $"Information[1103]: {string.Format(LoggingMessages.ProgrammaticSettings_SavingSettings, expectedCount, InputSettingsPath)}";
    private static string ExpectMessage_SavedSettings()
        => $"Information[1104]: {string.Format(LoggingMessages.ProgrammaticSettings_SavedSettings, InputSettingsPath)}";
    private static string ExpectMessage_AddSetting(string expectedKey, string expectedValue)
        => $"Information[1105]: {string.Format(LoggingMessages.ProgrammaticSettings_AddSetting, expectedKey, expectedValue)}";
    private static string ExpectMessage_Rejected(string key, string value, string reason)
        => $"Error[1107]: {LoggingMessages.ProgrammaticSettings_Rejected.AsMessageTemplate(key, value, reason)}";
    private static string ExpectMessage_Error(string key, string value, Exception expected)
        => $"Error[1108]: {LoggingMessages.ProgrammaticSettings_Error.AsMessageTemplate(key, value, $"{expected.GetType().FullName}: {expected.Message}")}";
}
