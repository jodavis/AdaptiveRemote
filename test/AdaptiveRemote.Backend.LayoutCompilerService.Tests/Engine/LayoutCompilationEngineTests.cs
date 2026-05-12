using AdaptiveRemote.Backend.LayoutCompilerService;
using AdaptiveRemote.Contracts;
using FluentAssertions;

namespace AdaptiveRemote.Backend.LayoutCompilerService.Tests.Engine;

/// <summary>
/// Unit tests for LayoutCompilationEngine covering:
/// - Grid-to-CSS transformation
/// - Payload stripping (authoring props removed, behavioral props preserved)
/// - Group hierarchy conversion
/// - Preview HTML/CSS generation
/// </summary>
[TestClass]
public class LayoutCompilationEngineTests
{
    // ── CSS generation ─────────────────────────────────────────────────────────

    [TestMethod]
    public void LayoutCompilationEngine_BuildCssDefinitions_SingleElement_ProducesGridContainerAndElementRule()
    {
        // Arrange
        IReadOnlyList<RawLayoutElementDto> elements = new[]
        {
            new RawCommandDefinitionDto(
                Type: CommandType.TiVo,
                Name: "Up",
                Label: "Up",
                Glyph: null,
                SpeakPhrase: "up",
                Reverse: "Down",
                CssId: "up-btn",
                GridRow: 1,
                GridColumn: 1)
        };

        // Act
        string css = LayoutCompilationEngine.BuildCssDefinitions(elements);

        // Assert
        css.Should().Contain(".layout-grid {");
        css.Should().Contain("display: grid;");
        css.Should().Contain("grid-template-columns: repeat(1, 1fr);");
        css.Should().Contain("grid-template-rows: repeat(1, auto);");
        css.Should().Contain("#up-btn {");
        css.Should().Contain("grid-row: 1 / span 1;");
        css.Should().Contain("grid-column: 1 / span 1;");
    }

    [TestMethod]
    public void LayoutCompilationEngine_BuildCssDefinitions_ElementWithSpan_ProducesCorrectSpanRule()
    {
        // Arrange
        IReadOnlyList<RawLayoutElementDto> elements = new[]
        {
            new RawCommandDefinitionDto(
                Type: CommandType.TiVo,
                Name: "Wide",
                Label: "Wide",
                Glyph: null,
                SpeakPhrase: "wide",
                Reverse: null,
                CssId: "wide-btn",
                GridRow: 2,
                GridColumn: 1,
                GridRowSpan: 1,
                GridColumnSpan: 3)
        };

        // Act
        string css = LayoutCompilationEngine.BuildCssDefinitions(elements);

        // Assert
        css.Should().Contain("grid-column: 1 / span 3;");
        css.Should().Contain("grid-template-columns: repeat(3, 1fr);");
    }

    [TestMethod]
    public void LayoutCompilationEngine_BuildCssDefinitions_ElementWithAdditionalCss_MergesIntoRule()
    {
        // Arrange
        IReadOnlyList<RawLayoutElementDto> elements = new[]
        {
            new RawCommandDefinitionDto(
                Type: CommandType.TiVo,
                Name: "Power",
                Label: "Power",
                Glyph: null,
                SpeakPhrase: "power",
                Reverse: null,
                CssId: "power-btn",
                GridRow: 1,
                GridColumn: 1,
                AdditionalCss: "background-color: red;\ncolor: white;")
        };

        // Act
        string css = LayoutCompilationEngine.BuildCssDefinitions(elements);

        // Assert
        css.Should().Contain("#power-btn {");
        css.Should().Contain("background-color: red;");
        css.Should().Contain("color: white;");
    }

    [TestMethod]
    public void LayoutCompilationEngine_BuildCssDefinitions_MultipleElements_GridExtentsBasedOnMaxValues()
    {
        // Arrange
        IReadOnlyList<RawLayoutElementDto> elements = new[]
        {
            new RawCommandDefinitionDto(
                Type: CommandType.TiVo,
                Name: "A",
                Label: "A",
                Glyph: null,
                SpeakPhrase: "a",
                Reverse: null,
                CssId: "btn-a",
                GridRow: 1,
                GridColumn: 1),
            new RawCommandDefinitionDto(
                Type: CommandType.TiVo,
                Name: "B",
                Label: "B",
                Glyph: null,
                SpeakPhrase: "b",
                Reverse: null,
                CssId: "btn-b",
                GridRow: 3,
                GridColumn: 4)
        };

        // Act
        string css = LayoutCompilationEngine.BuildCssDefinitions(elements);

        // Assert
        css.Should().Contain("grid-template-columns: repeat(4, 1fr);");
        css.Should().Contain("grid-template-rows: repeat(3, auto);");
    }

    // ── Element conversion (payload stripping) ─────────────────────────────────

    [TestMethod]
    public void LayoutCompilationEngine_ConvertElements_Command_StripsAuthoringPropertiesPreservesBehavioral()
    {
        // Arrange
        IReadOnlyList<RawLayoutElementDto> raw = new[]
        {
            new RawCommandDefinitionDto(
                Type: CommandType.TiVo,
                Name: "Vol+",
                Label: "Volume Up",
                Glyph: "volume_up",
                SpeakPhrase: "volume up",
                Reverse: "Vol-",
                CssId: "vol-up",
                GridRow: 2,
                GridColumn: 3,
                GridRowSpan: 1,
                GridColumnSpan: 2,
                AdditionalCss: "font-size: 2rem;")
        };

        // Act
        System.Collections.ObjectModel.ReadOnlyCollection<LayoutElementDto> compiled = LayoutCompilationEngine.ConvertElements(raw);

        // Assert
        compiled.Should().HaveCount(1);
        CommandDefinitionDto cmd = compiled[0].Should().BeOfType<CommandDefinitionDto>().Subject;
        cmd.Type.Should().Be(CommandType.TiVo);
        cmd.Name.Should().Be("Vol+");
        cmd.Label.Should().Be("Volume Up");
        cmd.Glyph.Should().Be("volume_up");
        cmd.SpeakPhrase.Should().Be("volume up");
        cmd.Reverse.Should().Be("Vol-");
        cmd.CssId.Should().Be("vol-up");
    }

    [TestMethod]
    public void LayoutCompilationEngine_ConvertElements_Group_RecursivelyConvertsChildren()
    {
        // Arrange
        IReadOnlyList<RawLayoutElementDto> raw = new[]
        {
            new RawLayoutGroupDefinitionDto(
                CssId: "nav-group",
                Children: new[]
                {
                    new RawCommandDefinitionDto(
                        Type: CommandType.TiVo,
                        Name: "Up",
                        Label: "Up",
                        Glyph: null,
                        SpeakPhrase: "up",
                        Reverse: null,
                        CssId: "up-btn",
                        GridRow: 1,
                        GridColumn: 1)
                },
                GridRow: 1,
                GridColumn: 1)
        };

        // Act
        System.Collections.ObjectModel.ReadOnlyCollection<LayoutElementDto> compiled = LayoutCompilationEngine.ConvertElements(raw);

        // Assert
        compiled.Should().HaveCount(1);
        LayoutGroupDefinitionDto group = compiled[0].Should().BeOfType<LayoutGroupDefinitionDto>().Subject;
        group.CssId.Should().Be("nav-group");
        group.Children.Should().HaveCount(1);
        group.Children[0].Should().BeOfType<CommandDefinitionDto>();
    }

    // ── Full Compile ───────────────────────────────────────────────────────────

    [TestMethod]
    public void LayoutCompilationEngine_Compile_InheritsVersionFromRaw()
    {
        // Arrange
        RawLayout raw = CreateTestRawLayout(version: 7);

        // Act
        CompiledLayout compiled = LayoutCompilationEngine.Compile(raw);

        // Assert
        compiled.Version.Should().Be(7);
        compiled.RawLayoutId.Should().Be(raw.Id);
        compiled.UserId.Should().Be(raw.UserId);
    }

    [TestMethod]
    public void LayoutCompilationEngine_Compile_GeneratesNewId()
    {
        // Arrange
        RawLayout raw = CreateTestRawLayout();

        // Act
        CompiledLayout compiled = LayoutCompilationEngine.Compile(raw);

        // Assert
        compiled.Id.Should().NotBe(Guid.Empty);
        compiled.Id.Should().NotBe(raw.Id);
    }

    [TestMethod]
    public void LayoutCompilationEngine_Compile_IsActiveDefaultsFalse()
    {
        // Arrange
        RawLayout raw = CreateTestRawLayout();

        // Act
        CompiledLayout compiled = LayoutCompilationEngine.Compile(raw);

        // Assert
        compiled.IsActive.Should().BeFalse();
    }

    // ── Preview ────────────────────────────────────────────────────────────────

    [TestMethod]
    public void LayoutCompilationEngine_CompilePreview_RenderedHtmlContainsGridContainer()
    {
        // Arrange
        IReadOnlyList<RawLayoutElementDto> elements = new[]
        {
            new RawCommandDefinitionDto(
                Type: CommandType.TiVo,
                Name: "Menu",
                Label: "Menu",
                Glyph: null,
                SpeakPhrase: "menu",
                Reverse: null,
                CssId: "menu-btn",
                GridRow: 1,
                GridColumn: 1)
        };

        // Act
        PreviewLayout preview = LayoutCompilationEngine.CompilePreview(elements);

        // Assert
        preview.RenderedHtml.Should().Contain("class=\"layout-grid\"");
        preview.RenderedHtml.Should().Contain("id=\"menu-btn\"");
        preview.RenderedHtml.Should().Contain("Menu");
    }

    [TestMethod]
    public void LayoutCompilationEngine_CompilePreview_RenderedCssContainsGridDefinitions()
    {
        // Arrange
        IReadOnlyList<RawLayoutElementDto> elements = new[]
        {
            new RawCommandDefinitionDto(
                Type: CommandType.TiVo,
                Name: "Back",
                Label: "Back",
                Glyph: null,
                SpeakPhrase: "back",
                Reverse: null,
                CssId: "back-btn",
                GridRow: 1,
                GridColumn: 1)
        };

        // Act
        PreviewLayout preview = LayoutCompilationEngine.CompilePreview(elements);

        // Assert
        preview.RenderedCss.Should().Contain(".layout-grid");
        preview.RenderedCss.Should().Contain("#back-btn");
    }

    [TestMethod]
    public void LayoutCompilationEngine_CompilePreview_ValidationResultIsValid()
    {
        // Arrange
        IReadOnlyList<RawLayoutElementDto> elements = new[]
        {
            new RawCommandDefinitionDto(
                Type: CommandType.TiVo,
                Name: "OK",
                Label: "OK",
                Glyph: null,
                SpeakPhrase: "ok",
                Reverse: null,
                CssId: "ok-btn",
                GridRow: 1,
                GridColumn: 1)
        };

        // Act
        PreviewLayout preview = LayoutCompilationEngine.CompilePreview(elements);

        // Assert
        preview.ValidationResult.IsValid.Should().BeTrue();
        preview.ValidationResult.Issues.Should().BeEmpty();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static RawLayout CreateTestRawLayout(int version = 1)
    {
        return new RawLayout(
            Id: Guid.NewGuid(),
            UserId: "test-user",
            Name: "Test Layout",
            Elements: new[]
            {
                new RawCommandDefinitionDto(
                    Type: CommandType.TiVo,
                    Name: "Up",
                    Label: "Up",
                    Glyph: null,
                    SpeakPhrase: "up",
                    Reverse: "Down",
                    CssId: "up-btn",
                    GridRow: 1,
                    GridColumn: 1)
            },
            Version: version,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            ValidationResult: null);
    }
}
