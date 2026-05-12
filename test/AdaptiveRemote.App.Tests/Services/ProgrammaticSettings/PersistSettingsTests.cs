using Moq;

namespace AdaptiveRemote.Services.ProgrammaticSettings;

[TestClass]
public class PersistSettingsTests
{
    private static readonly string InputSettingsPath = Path.Combine("%LocalAppData%", "path", "to", "settings.ini");
    private static readonly string ResolvedSettingsPath = Environment.ExpandEnvironmentVariables(InputSettingsPath);

    private readonly MockLogger<PersistSettings> MockLogger = new();
    private readonly MockFileSystem MockFileSystem = new();
    private readonly MockOptions<ProgrammaticSettings> MockOptions = new(new()
    {
        ProgrammaticSettingsPath = InputSettingsPath
    });

    private PersistSettings CreateSut() => new(MockFileSystem.Object, MockOptions, MockLogger);

    public TestContext? TestContext
    {
        get => MockLogger.OutputWriter;
        set => MockLogger.OutputWriter = value;
    }

    private static readonly char[] LineSeparators = ['\r', '\n'];

    [TestCleanup]
    public void VerifyMocks()
    {
        MockFileSystem.Verify();
    }

    [TestMethod]
    public async Task PersistSettings_Set_SavesSettingsToFileAsync()
    {
        // Arrange
        IPersistSettings sut = CreateSut();

        MockFileSystem.AddFile(ResolvedSettingsPath, "ExistingSetting=123");

        MockFileSystem.Expect_OpenRead_ForPath(ResolvedSettingsPath);
        MockFileSystem.Expect_OpenWrite_ForPath(ResolvedSettingsPath);
        if (InputSettingsPath != ResolvedSettingsPath)
        {
            MockFileSystem.Expect_OpenRead_IsNotCalledForPath(InputSettingsPath);
            MockFileSystem.Expect_OpenWrite_IsNotCalledForPath(InputSettingsPath);
        }

        // Act
        sut.Set("NewSetting", "abc");

        await MockLogger.WaitForMessageAsync(msg => msg.ProgrammaticSettings_SavedSettings(ResolvedSettingsPath));

        // Assert
        MockFileSystem.VerifyFileContents(ResolvedSettingsPath, $"ExistingSetting=123{Environment.NewLine}NewSetting=abc{Environment.NewLine}");

        MockLogger.VerifyMessages(log =>
        {
            log.ProgrammaticSettings_LoadingExistingSettings(ResolvedSettingsPath);
            log.ProgrammaticSettings_LoadedExistingSettings(1, ResolvedSettingsPath);
            log.ProgrammaticSettings_AddSetting("NewSetting", "abc");
            log.ProgrammaticSettings_SavingSettings(2, ResolvedSettingsPath);
            log.ProgrammaticSettings_SavedSettings(ResolvedSettingsPath);
        });
    }

    [TestMethod]
    public async Task PersistSettings_Set_CalledMultipleTimes_SavesSettingsToFileAsync()
    {
        // Arrange
        IPersistSettings sut = CreateSut();

        MockFileSystem.AddFile(ResolvedSettingsPath, "ExistingSetting=123");

        MockFileSystem.Expect_OpenRead_ForPath(ResolvedSettingsPath);
        MockFileSystem.Expect_OpenWrite_ForPath(ResolvedSettingsPath, Times.Between(1, 3, Moq.Range.Inclusive));

        // Act
        sut.Set("NewSetting1", "abc");
        sut.Set("NewSetting2", "def");
        sut.Set("NewSetting3", "ghi");

        await MockLogger.WaitForMessageAsync(msg => msg.ProgrammaticSettings_SavingSettings(4, ResolvedSettingsPath));

        // Assert
        DateTime startTime = DateTime.Now;
        const int timeout = 1; // second
        while (true)
        {
            if (MockLogger.Messages.LastOrDefault()?.Contains($"Saved settings to {ResolvedSettingsPath}") == true)
            {
                break;
            }
            Assert.IsTrue(DateTime.Now - startTime < TimeSpan.FromSeconds(timeout), $"Timed out waiting for final message: ProgrammaticSettings_SavedSettings({ResolvedSettingsPath})");
            await Task.Delay(100);
        }

        string[] expectedSettings = ["ExistingSetting=123", "NewSetting3=ghi", "NewSetting2=def", "NewSetting1=abc"];
        string[] actualSettings = MockFileSystem.GetFileContents(ResolvedSettingsPath).Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries);

        Assert.IsFalse(expectedSettings.Except(actualSettings).Any(), "Did not find expected settings in {0}: {1}", ResolvedSettingsPath, string.Join(", ", expectedSettings.Except(actualSettings)));
        Assert.IsFalse(actualSettings.Except(expectedSettings).Any(), "Did not find expected settings in {0}: {1}", ResolvedSettingsPath, string.Join(", ", expectedSettings.Except(actualSettings)));
    }

    [TestMethod]
    public async Task PersistSettings_Set_ChangesExistingSettingInFileAsync()
    {
        // Arrange
        IPersistSettings sut = CreateSut();

        MockFileSystem.AddFile(ResolvedSettingsPath, "ExistingSetting=123");

        MockFileSystem.Expect_OpenRead_ForPath(ResolvedSettingsPath);
        MockFileSystem.Expect_OpenWrite_ForPath(ResolvedSettingsPath);

        // Act
        sut.Set("ExistingSetting", "ghi");

        await MockLogger.WaitForMessageAsync(msg => msg.ProgrammaticSettings_SavedSettings(ResolvedSettingsPath));

        // Assert
        MockFileSystem.VerifyFileContents(ResolvedSettingsPath, $"ExistingSetting=ghi{Environment.NewLine}");

        MockLogger.VerifyMessages(log =>
        {
            log.ProgrammaticSettings_LoadingExistingSettings(ResolvedSettingsPath);
            log.ProgrammaticSettings_LoadedExistingSettings(1, ResolvedSettingsPath);
            log.ProgrammaticSettings_ReplaceSetting("ExistingSetting", "123", "ghi");
            log.ProgrammaticSettings_SavingSettings(1, ResolvedSettingsPath);
            log.ProgrammaticSettings_SavedSettings(ResolvedSettingsPath);
        });
    }

    [TestMethod]
    public async Task PersistSettings_Set_OnFailureToLoadExistingFile_LogsErrorAsync()
    {
        // Arrange
        IPersistSettings sut = CreateSut();

        Exception expectedException = new ArgumentOutOfRangeException();

        MockFileSystem.AddFile(ResolvedSettingsPath);
        MockFileSystem
            .Setup(x => x.OpenRead(ResolvedSettingsPath))
            .Throws(expectedException)
            .Verifiable(Times.Once);

        // Act
        sut.Set("NewSetting", "abc");

        await MockLogger.WaitForMessageAsync(msg => msg.ProgrammaticSettings_Error("NewSetting", "abc", expectedException));

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.ProgrammaticSettings_LoadingExistingSettings(ResolvedSettingsPath);
            log.ProgrammaticSettings_Error("NewSetting", "abc", expectedException);
        });
    }

    [TestMethod]
    public async Task PersistSettings_Set_OnFailureToSaveFile_LogsErrorAsync()
    {
        // Arrange
        IPersistSettings sut = CreateSut();

        Exception expectedException = new ArgumentOutOfRangeException();

        MockFileSystem.AddFile(ResolvedSettingsPath, "ExistingSetting=123");

        MockFileSystem.Expect_OpenRead_ForPath(ResolvedSettingsPath);
        MockFileSystem
            .Setup(x => x.OpenWrite(ResolvedSettingsPath))
            .Throws(expectedException)
            .Verifiable(Times.Once);

        // Act
        sut.Set("NewSetting", "abc");

        await MockLogger.WaitForMessageAsync(msg => msg.ProgrammaticSettings_Error("NewSetting", "abc", expectedException));

        // Assert
        MockLogger.VerifyMessages(log =>
        {
            log.ProgrammaticSettings_LoadingExistingSettings(ResolvedSettingsPath);
            log.ProgrammaticSettings_LoadedExistingSettings(1, ResolvedSettingsPath);
            log.ProgrammaticSettings_AddSetting("NewSetting", "abc");
            log.ProgrammaticSettings_SavingSettings(2, ResolvedSettingsPath);
            log.ProgrammaticSettings_Error("NewSetting", "abc", expectedException);
        });
    }

    [TestMethod]
    public async Task PersistSettings_Set_WhenFileNotFound_CreatesFileAsync()
    {
        // Arrange
        IPersistSettings sut = CreateSut();

        MockFileSystem.AddDirectory(Path.GetDirectoryName(ResolvedSettingsPath));

        MockFileSystem.Expect_OpenRead_IsNotCalled();
        MockFileSystem.Expect_OpenWrite_ForPath(ResolvedSettingsPath);

        // Act
        sut.Set("NewSetting", "abc");

        await MockLogger.WaitForMessageAsync(msg => msg.ProgrammaticSettings_SavedSettings(ResolvedSettingsPath));

        // Assert
        MockFileSystem.VerifyFileContents(ResolvedSettingsPath, $"NewSetting=abc{Environment.NewLine}");

        MockLogger.VerifyMessages(log =>
        {
            log.ProgrammaticSettings_AddSetting("NewSetting", "abc");
            log.ProgrammaticSettings_SavingSettings(1, ResolvedSettingsPath);
            log.ProgrammaticSettings_SavedSettings(ResolvedSettingsPath);
        });
    }

    [TestMethod]
    public async Task PersistSettings_Set_WhenFileNotFound_CreatesDirectoryAsync()
    {
        // Arrange
        IPersistSettings sut = CreateSut();

        string rootPath = Path.GetDirectoryName(Path.GetDirectoryName(ResolvedSettingsPath)!) ?? "path";
        MockFileSystem.AddDirectory(rootPath);

        MockFileSystem.Expect_OpenRead_IsNotCalled();

        MockFileSystem.Expect_CreateDirectory_ForPath(Path.Combine(rootPath, "to"));

        MockFileSystem.Expect_OpenWrite_ForPath(ResolvedSettingsPath);

        // Act
        sut.Set("NewSetting", "abc");

        await MockLogger.WaitForMessageAsync(msg => msg.ProgrammaticSettings_SavedSettings(ResolvedSettingsPath));

        // Assert
        MockFileSystem.VerifyFileContents(ResolvedSettingsPath, $"NewSetting=abc{Environment.NewLine}");

        MockLogger.VerifyMessages(log =>
        {
            log.ProgrammaticSettings_AddSetting("NewSetting", "abc");
            log.ProgrammaticSettings_SavingSettings(1, ResolvedSettingsPath);
            log.ProgrammaticSettings_SavedSettings(ResolvedSettingsPath);
        });
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
    public async Task PersistSettings_Set_ValidatesKeyNameAsync(string input, bool expectedResult)
    {
        // Arrange
        IPersistSettings sut = CreateSut();

        MockFileSystem.AddFile(ResolvedSettingsPath, "");

        try
        {
            // Act
            sut.Set(input, "def");

            // Assert
            Assert.IsTrue(expectedResult, "Expected ArgumentException was not thrown for input:'{0}'.", input);

            await MockLogger.WaitForMessageAsync(msg => msg.ProgrammaticSettings_SavedSettings(ResolvedSettingsPath));

            MockLogger.VerifyMessages(log =>
            {
                log.ProgrammaticSettings_LoadingExistingSettings(ResolvedSettingsPath);
                log.ProgrammaticSettings_LoadedExistingSettings(0, ResolvedSettingsPath);
                log.ProgrammaticSettings_AddSetting(input, "def");
                log.ProgrammaticSettings_SavingSettings(1, ResolvedSettingsPath);
                log.ProgrammaticSettings_SavedSettings(ResolvedSettingsPath);
            });
        }
        catch (ArgumentException result) when (!expectedResult)
        {
            ArgumentException expectedException = (ArgumentException)Errors.PersistSettings_InvalidName("name", input);

            Assert.AreEqual(expectedException.ParamName, result.ParamName, nameof(result.ParamName) + " for input:'{0}'", input);
            Assert.AreEqual(expectedException.Message, result.Message, nameof(result.Message) + " for input:'{0}'", input);

            MockLogger.VerifyMessages(log =>
            {
                log.ProgrammaticSettings_Rejected(input, "def", expectedException.Message);
            });
        }
    }

    [DataTestMethod]
    [DataRow("Valid", true)]
    [DataRow("Valid with spaces", true)]
    [DataRow("", true)]
    [DataRow("Invalid\n", false)] // Rejects newline
    [DataRow("Invalid\r", false)] // Rejects carriage return
    public async Task PersistSettings_Set_ValidatesValueAsync(string input, bool expectedResult)
    {
        // Arrange
        IPersistSettings sut = CreateSut();

        MockFileSystem.AddFile(ResolvedSettingsPath, "");

        try
        {
            // Act
            sut.Set("TestSetting", input);

            // Assert
            Assert.IsTrue(expectedResult, "Expected ArgumentException was not thrown for input:'{0}'.", input);

            await MockLogger.WaitForMessageAsync(msg => msg.ProgrammaticSettings_SavedSettings(ResolvedSettingsPath));

            MockLogger.VerifyMessages(log =>
            {
                log.ProgrammaticSettings_LoadingExistingSettings(ResolvedSettingsPath);
                log.ProgrammaticSettings_LoadedExistingSettings(0, ResolvedSettingsPath);
                log.ProgrammaticSettings_AddSetting("TestSetting", input);
                log.ProgrammaticSettings_SavingSettings(1, ResolvedSettingsPath);
                log.ProgrammaticSettings_SavedSettings(ResolvedSettingsPath);
            });
        }
        catch (ArgumentException result) when (!expectedResult)
        {
            ArgumentException expectedException = (ArgumentException)Errors.PersistSettings_InvalidValue("value", input);

            Assert.AreEqual(expectedException.ParamName, result.ParamName, nameof(result.ParamName) + " for input:'{0}'", input);
            Assert.AreEqual(expectedException.Message, result.Message, nameof(result.Message) + " for input:'{0}'", input);

            MockLogger.VerifyMessages(log =>
            {
                log.ProgrammaticSettings_Rejected("TestSetting", input, expectedException.Message);
            });
        }
    }

    [TestMethod]
    public async Task PersistSettings_Set_SectionKey_WritesSectionFormatAsync()
    {
        // Arrange
        IPersistSettings sut = CreateSut();

        MockFileSystem.AddDirectory(Path.GetDirectoryName(ResolvedSettingsPath));
        MockFileSystem.Expect_OpenRead_IsNotCalled();
        MockFileSystem.Expect_OpenWrite_ForPath(ResolvedSettingsPath);

        // Act
        sut.Set("IRData:Power", "AQIDBA==");

        await MockLogger.WaitForMessageAsync(msg => msg.ProgrammaticSettings_SavedSettings(ResolvedSettingsPath));

        // Assert
        MockFileSystem.VerifyFileContents(ResolvedSettingsPath, $"[IRData]{Environment.NewLine}Power=AQIDBA=={Environment.NewLine}");

        MockLogger.VerifyMessages(log =>
        {
            log.ProgrammaticSettings_AddSetting("IRData:Power", "AQIDBA==");
            log.ProgrammaticSettings_SavingSettings(1, ResolvedSettingsPath);
            log.ProgrammaticSettings_SavedSettings(ResolvedSettingsPath);
        });
    }

    [TestMethod]
    public async Task PersistSettings_Set_ReadsExistingIniSectionFormatAsync()
    {
        // Arrange
        IPersistSettings sut = CreateSut();

        MockFileSystem.AddFile(ResolvedSettingsPath, $"[IRData]{Environment.NewLine}Power=AQIDBA=={Environment.NewLine}");
        MockFileSystem.Expect_OpenRead_ForPath(ResolvedSettingsPath);
        MockFileSystem.Expect_OpenWrite_ForPath(ResolvedSettingsPath);

        // Act
        sut.Set("IRData:VolumeUp", "BQYHCA==");

        await MockLogger.WaitForMessageAsync(msg => msg.ProgrammaticSettings_SavedSettings(ResolvedSettingsPath));

        // Assert
        string contents = MockFileSystem.GetFileContents(ResolvedSettingsPath);
        Assert.IsTrue(contents.Contains("[IRData]"), "Output should contain [IRData] section header");
        Assert.IsTrue(contents.Contains($"Power=AQIDBA=="), "Output should contain existing Power entry");
        Assert.IsTrue(contents.Contains($"VolumeUp=BQYHCA=="), "Output should contain new VolumeUp entry");

        MockLogger.VerifyMessages(log =>
        {
            log.ProgrammaticSettings_LoadingExistingSettings(ResolvedSettingsPath);
            log.ProgrammaticSettings_LoadedExistingSettings(1, ResolvedSettingsPath);
            log.ProgrammaticSettings_AddSetting("IRData:VolumeUp", "BQYHCA==");
            log.ProgrammaticSettings_SavingSettings(2, ResolvedSettingsPath);
            log.ProgrammaticSettings_SavedSettings(ResolvedSettingsPath);
        });
    }

}
