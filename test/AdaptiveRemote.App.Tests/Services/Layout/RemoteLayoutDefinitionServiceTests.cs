using AdaptiveRemote.Contracts;
using AdaptiveRemote.Models;
using FluentAssertions;

namespace AdaptiveRemote.Services.Layout;

[TestClass]
public class RemoteLayoutDefinitionServiceTests
{
    private static CompiledLayout MakeLayout(params LayoutElementDto[] elements) =>
        new(Guid.Empty, Guid.Empty, "stub", true, 1, elements, "", DateTimeOffset.UtcNow);

    [TestMethod]
    public void RemoteLayoutDefinitionService_RemoteRoot_MapsTiVoCommandCorrectly()
    {
        // Arrange
        CompiledLayout layout = MakeLayout(
            new LayoutGroupDefinitionDto("GROUP",
            [
                new CommandDefinitionDto(CommandType.TiVo, "Play", "Play", null, "Sent Play", "Pause", "Play"),
            ]));

        RemoteLayoutDefinitionService sut = new(layout);

        // Act
        LayoutGroup root = (LayoutGroup)sut.RemoteRoot;
        LayoutGroup group = (LayoutGroup)root.Elements.First();
        TiVoCommand cmd = (TiVoCommand)group.Elements.First();

        // Assert
        cmd.Name.Should().Be("Play");
        cmd.Reverse.Should().Be("Pause");
        cmd.SpeakPhrase.Should().Be("Sent Play");
    }

    [TestMethod]
    public void RemoteLayoutDefinitionService_RemoteRoot_MapsIRCommandCorrectly()
    {
        // Arrange
        CompiledLayout layout = MakeLayout(
            new LayoutGroupDefinitionDto("GROUP",
            [
                new CommandDefinitionDto(CommandType.IR, "VolumeUp", "Up", null, "Sent Volume Up", "VolumeDown", "VolumeUp"),
            ]));

        RemoteLayoutDefinitionService sut = new(layout);

        // Act
        LayoutGroup root = (LayoutGroup)sut.RemoteRoot;
        LayoutGroup group = (LayoutGroup)root.Elements.First();
        RemoteLayoutElement cmd = group.Elements.First();

        // Assert
        cmd.Should().BeOfType<IRCommand>();
        cmd.As<IRCommand>().Name.Should().Be("VolumeUp");
        cmd.As<IRCommand>().SpeakPhrase.Should().Be("Sent Volume Up");
    }

    [TestMethod]
    public void RemoteLayoutDefinitionService_RemoteRoot_MapsLifecycleCommandCorrectly()
    {
        // Arrange
        CompiledLayout layout = MakeLayout(
            new LayoutGroupDefinitionDto("GROUP",
            [
                new CommandDefinitionDto(CommandType.Lifecycle, "Exit", "Exit", null, "Goodbye", null, "Exit"),
            ]));

        RemoteLayoutDefinitionService sut = new(layout);

        // Act
        LayoutGroup root = (LayoutGroup)sut.RemoteRoot;
        LayoutGroup group = (LayoutGroup)root.Elements.First();
        RemoteLayoutElement cmd = group.Elements.First();

        // Assert
        cmd.Should().BeOfType<LifecycleCommand>();
        cmd.As<LifecycleCommand>().Name.Should().Be("Exit");
    }

    [TestMethod]
    public void RemoteLayoutDefinitionService_RemoteRoot_MapsLayoutGroupCorrectly()
    {
        // Arrange
        CompiledLayout layout = MakeLayout(
            new LayoutGroupDefinitionDto("DPAD",
            [
                new CommandDefinitionDto(CommandType.TiVo, "Up",   "Up",   null, "Sent Up",   "Down", "Up"),
                new CommandDefinitionDto(CommandType.TiVo, "Down", "Down", null, "Sent Down", "Up",   "Down"),
            ]));

        RemoteLayoutDefinitionService sut = new(layout);

        // Act
        LayoutGroup root = (LayoutGroup)sut.RemoteRoot;
        LayoutGroup dpad = (LayoutGroup)root.Elements.First();

        // Assert
        dpad.CSSID.Should().Be("DPAD");
        dpad.Elements.Should().HaveCount(2);
    }

    [TestMethod]
    public void RemoteLayoutDefinitionService_RemoteRoot_GutterAlwaysAppendedAsLastChild()
    {
        // Arrange
        CompiledLayout layout = MakeLayout(
            new LayoutGroupDefinitionDto("DPAD",
            [
                new CommandDefinitionDto(CommandType.TiVo, "Up", "Up", null, "Sent Up", null, "Up"),
            ]));

        RemoteLayoutDefinitionService sut = new(layout);

        // Act
        LayoutGroup root = (LayoutGroup)sut.RemoteRoot;
        RemoteLayoutElement lastChild = root.Elements.Last();

        // Assert
        lastChild.Should().BeOfType<LayoutGroup>();
        LayoutGroup gutter = (LayoutGroup)lastChild;
        gutter.CSSID.Should().Be("GUTTER");

        List<RemoteLayoutElement> gutterElements = gutter.Elements.ToList();
        gutterElements.Should().HaveCount(3);
        gutterElements[0].Should().BeOfType<ConversationView>();
        gutterElements[1].Should().BeOfType<LifecycleCommand>()
            .Which.Name.Should().Be("Learn");
        gutterElements[2].Should().BeOfType<LifecycleCommand>()
            .Which.Name.Should().Be("Exit");
    }

    [TestMethod]
    public void RemoteLayoutDefinitionService_RemoteRoot_UnknownCommandTypeThrows()
    {
        // Arrange
        CompiledLayout layout = MakeLayout(
            new LayoutGroupDefinitionDto("GROUP",
            [
                new CommandDefinitionDto((CommandType)99, "Unknown", "Unknown", null, "Unknown", null, "Unknown"),
            ]));

        // Act
        Action act = () => _ = new RemoteLayoutDefinitionService(layout);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown CommandType*");
    }

    [TestMethod]
    public void RemoteLayoutDefinitionService_RemoteRoot_GutterInLayoutThrowsDescriptiveError()
    {
        // Arrange
        CompiledLayout layout = MakeLayout(
            new LayoutGroupDefinitionDto("GUTTER",
            [
                new CommandDefinitionDto(CommandType.Lifecycle, "Exit", "Exit", null, "Goodbye", null, "Exit"),
            ]));

        // Act
        Action act = () => _ = new RemoteLayoutDefinitionService(layout);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already contains a GUTTER*");
    }

    [TestMethod]
    public void RemoteLayoutDefinitionService_RemoteRoot_MapsNestedGroupsRecursively()
    {
        // Arrange
        CompiledLayout layout = MakeLayout(
            new LayoutGroupDefinitionDto("OUTER",
            [
                new LayoutGroupDefinitionDto("INNER",
                [
                    new CommandDefinitionDto(CommandType.TiVo, "Select", "Select", null, "Sent Select", null, "Select"),
                ]),
            ]));

        RemoteLayoutDefinitionService sut = new(layout);

        // Act
        LayoutGroup root = (LayoutGroup)sut.RemoteRoot;
        LayoutGroup outer = (LayoutGroup)root.Elements.First();
        LayoutGroup inner = (LayoutGroup)outer.Elements.First();

        // Assert
        outer.CSSID.Should().Be("OUTER");
        inner.CSSID.Should().Be("INNER");
        inner.Elements.First().Should().BeOfType<TiVoCommand>();
    }
}
