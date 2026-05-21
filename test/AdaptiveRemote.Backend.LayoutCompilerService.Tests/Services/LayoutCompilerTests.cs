using AdaptiveRemote.Backend.LayoutCompilerService.Services;
using AdaptiveRemote.Contracts;
using FluentAssertions;

namespace AdaptiveRemote.Backend.LayoutCompilerService.Tests.Services;

[TestClass]
public class LayoutCompilerTests
{
    private MockLogger<LayoutCompiler> _mockLogger = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new MockLogger<LayoutCompiler>();
    }

    private LayoutCompiler CreateSut() => new(_mockLogger);

    // ─── Compile: single command element ──────────────────────────────────────

    [TestMethod]
    public void LayoutCompiler_Compile_SingleCommandElement_ReturnsCompiledLayoutWithElement()
    {
        RawLayout raw = CreateRawLayoutWithCommand("cmd-up", "Up", gridRow: 1, gridColumn: 2);

        CompiledLayout result = CreateSut().Compile(raw);

        result.RawLayoutId.Should().Be(raw.Id);
        result.UserId.Should().Be(raw.UserId);
        result.Version.Should().Be(raw.Version);
        result.IsActive.Should().BeFalse();
        result.Elements.Should().HaveCount(1);
        result.Elements[0].Should().BeOfType<CommandDefinitionDto>();
    }

    [TestMethod]
    public void LayoutCompiler_Compile_SingleCommandElement_StripsAuthoringProperties()
    {
        RawLayout raw = CreateRawLayoutWithCommand("cmd-up", "Up", gridRow: 1, gridColumn: 2,
            gridRowSpan: 2, gridColumnSpan: 3, additionalCss: "color: red;");

        CompiledLayout result = CreateSut().Compile(raw);

        CommandDefinitionDto cmd = result.Elements.OfType<CommandDefinitionDto>().Single();
        cmd.Name.Should().Be("Up");
        cmd.CssId.Should().Be("cmd-up");
        // CommandDefinitionDto has no grid/CSS authoring properties — confirmed by its type definition
    }

    [TestMethod]
    public void LayoutCompiler_Compile_SingleCommandElement_GeneratesCssRuleForGridPosition()
    {
        RawLayout raw = CreateRawLayoutWithCommand("cmd-up", "Up", gridRow: 3, gridColumn: 4);

        CompiledLayout result = CreateSut().Compile(raw);

        result.CssDefinitions.Should().Contain(".cmd-up {");
        result.CssDefinitions.Should().Contain("grid-row: 3 / span 1");
        result.CssDefinitions.Should().Contain("grid-column: 4 / span 1");
    }

    [TestMethod]
    public void LayoutCompiler_Compile_ElementWithSpan_IncludesSpanInCss()
    {
        RawLayout raw = CreateRawLayoutWithCommand("cmd-big", "Big", gridRow: 1, gridColumn: 1,
            gridRowSpan: 2, gridColumnSpan: 3);

        CompiledLayout result = CreateSut().Compile(raw);

        result.CssDefinitions.Should().Contain("grid-row: 1 / span 2");
        result.CssDefinitions.Should().Contain("grid-column: 1 / span 3");
    }

    [TestMethod]
    public void LayoutCompiler_Compile_ElementWithAdditionalCss_IncludesAdditionalCssInDefinitions()
    {
        RawLayout raw = CreateRawLayoutWithCommand("cmd-power", "Power", gridRow: 1, gridColumn: 1,
            additionalCss: "background-color: red;");

        CompiledLayout result = CreateSut().Compile(raw);

        result.CssDefinitions.Should().Contain("background-color: red");
    }

    [TestMethod]
    public void LayoutCompiler_Compile_GroupElement_GeneratesCssForGroupAndChildren()
    {
        RawLayoutGroupDefinitionDto group = new(
            CssId: "nav-group",
            Children: new List<RawLayoutElementDto>
            {
                new RawCommandDefinitionDto(CommandType.TiVo, "Up", "Up", null, "up", null,
                    "cmd-up", GridRow: 1, GridColumn: 1)
            },
            GridRow: 0, GridColumn: 0
        );

        RawLayout raw = CreateRawLayout(new List<RawLayoutElementDto> { group });

        CompiledLayout result = CreateSut().Compile(raw);

        result.CssDefinitions.Should().Contain(".nav-group {");
        result.CssDefinitions.Should().Contain(".cmd-up {");
        result.Elements.Should().HaveCount(1);
        result.Elements[0].Should().BeOfType<LayoutGroupDefinitionDto>();
        LayoutGroupDefinitionDto compiledGroup = (LayoutGroupDefinitionDto)result.Elements[0];
        compiledGroup.Children.Should().HaveCount(1);
        compiledGroup.Children[0].Should().BeOfType<CommandDefinitionDto>();
    }

    [TestMethod]
    public void LayoutCompiler_Compile_EmptyElements_ReturnsCssDefinitionsEmptyAndNoElements()
    {
        RawLayout raw = CreateRawLayout(Array.Empty<RawLayoutElementDto>());

        CompiledLayout result = CreateSut().Compile(raw);

        result.Elements.Should().BeEmpty();
        result.CssDefinitions.Should().BeEmpty();
    }

    [TestMethod]
    public void LayoutCompiler_Compile_LogsCompilationStartedAndCompleted()
    {
        RawLayout raw = CreateRawLayoutWithCommand("cmd-up", "Up", gridRow: 1, gridColumn: 1);

        CreateSut().Compile(raw);

        _mockLogger.VerifyMessages(
            $"Information[1800]: Compilation started; rawLayoutId={raw.Id}",
            $"Information[1801]: Compilation completed; rawLayoutId={raw.Id}");
    }

    // ─── CompilePreview ────────────────────────────────────────────────────────

    [TestMethod]
    public void LayoutCompiler_CompilePreview_SingleCommandElement_ReturnsNonEmptyHtmlAndCss()
    {
        IReadOnlyList<RawLayoutElementDto> elements = new List<RawLayoutElementDto>
        {
            new RawCommandDefinitionDto(CommandType.TiVo, "Up", "Up", null, "up", null,
                "cmd-up", GridRow: 1, GridColumn: 1)
        };

        PreviewLayout result = CreateSut().CompilePreview(elements);

        result.RenderedHtml.Should().NotBeNullOrEmpty();
        result.RenderedCss.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public void LayoutCompiler_CompilePreview_SingleCommandElement_HtmlContainsElementDiv()
    {
        IReadOnlyList<RawLayoutElementDto> elements = new List<RawLayoutElementDto>
        {
            new RawCommandDefinitionDto(CommandType.TiVo, "Up", "Up", null, "up", null,
                "cmd-up", GridRow: 1, GridColumn: 1)
        };

        PreviewLayout result = CreateSut().CompilePreview(elements);

        result.RenderedHtml.Should().Contain("class=\"cmd-up\"");
        result.RenderedHtml.Should().Contain("layout-preview-grid");
    }

    [TestMethod]
    public void LayoutCompiler_CompilePreview_SingleCommandElement_CssContainsGridRuleAndContainer()
    {
        IReadOnlyList<RawLayoutElementDto> elements = new List<RawLayoutElementDto>
        {
            new RawCommandDefinitionDto(CommandType.TiVo, "Up", "Up", null, "up", null,
                "cmd-up", GridRow: 2, GridColumn: 3)
        };

        PreviewLayout result = CreateSut().CompilePreview(elements);

        result.RenderedCss.Should().Contain(".layout-preview-grid");
        result.RenderedCss.Should().Contain(".cmd-up");
        result.RenderedCss.Should().Contain("grid-row: 2");
        result.RenderedCss.Should().Contain("grid-column: 3");
    }

    [TestMethod]
    public void LayoutCompiler_CompilePreview_ReturnsValidationResultWithIsValidTrue()
    {
        IReadOnlyList<RawLayoutElementDto> elements = new List<RawLayoutElementDto>
        {
            new RawCommandDefinitionDto(CommandType.TiVo, "Up", "Up", null, "up", null,
                "cmd-up", GridRow: 1, GridColumn: 1)
        };

        PreviewLayout result = CreateSut().CompilePreview(elements);

        result.ValidationResult.IsValid.Should().BeTrue();
        result.ValidationResult.Issues.Should().BeEmpty();
    }

    [TestMethod]
    public void LayoutCompiler_CompilePreview_ReturnsRawLayoutIdEmpty()
    {
        IReadOnlyList<RawLayoutElementDto> elements = new List<RawLayoutElementDto>
        {
            new RawCommandDefinitionDto(CommandType.TiVo, "Up", "Up", null, "up", null,
                "cmd-up", GridRow: 1, GridColumn: 1)
        };

        PreviewLayout result = CreateSut().CompilePreview(elements);

        result.RawLayoutId.Should().Be(Guid.Empty);
    }

    [TestMethod]
    public void LayoutCompiler_CompilePreview_LogsPreviewCompilationStartedAndCompleted()
    {
        IReadOnlyList<RawLayoutElementDto> elements = new List<RawLayoutElementDto>
        {
            new RawCommandDefinitionDto(CommandType.TiVo, "Up", "Up", null, "up", null,
                "cmd-up", GridRow: 1, GridColumn: 1)
        };

        CreateSut().CompilePreview(elements);

        _mockLogger.VerifyMessages(
            "Information[1802]: Preview compilation started",
            "Information[1803]: Preview compilation completed");
    }

    [TestMethod]
    public void LayoutCompiler_CompilePreview_EmptyElements_ReturnsNonEmptyContainerHtml()
    {
        PreviewLayout result = CreateSut().CompilePreview(Array.Empty<RawLayoutElementDto>());

        result.RenderedHtml.Should().Contain("layout-preview-grid");
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private static RawLayout CreateRawLayoutWithCommand(
        string cssId,
        string name,
        int gridRow,
        int gridColumn,
        int gridRowSpan = 1,
        int gridColumnSpan = 1,
        string? additionalCss = null)
    {
        return CreateRawLayout(new List<RawLayoutElementDto>
        {
            new RawCommandDefinitionDto(
                Type: CommandType.TiVo,
                Name: name,
                Label: name,
                Glyph: null,
                SpeakPhrase: name.ToLowerInvariant(),
                Reverse: null,
                CssId: cssId,
                GridRow: gridRow,
                GridColumn: gridColumn,
                GridRowSpan: gridRowSpan,
                GridColumnSpan: gridColumnSpan,
                AdditionalCss: additionalCss)
        });
    }

    private static RawLayout CreateRawLayout(IReadOnlyList<RawLayoutElementDto> elements)
    {
        return new RawLayout(
            Id: Guid.NewGuid(),
            UserId: "test-user",
            Name: "Test Layout",
            Elements: elements,
            Version: 1,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            ValidationResult: null
        );
    }
}
