using Microsoft.Extensions.Options;
using Moq;

namespace AdaptiveRemote.TestUtilities;

internal class MockOptionsSnapshot<SettingsType> : Mock<IOptionsSnapshot<SettingsType>>, IOptionsSnapshot<SettingsType>
    where SettingsType : class, new()
{
    public MockOptionsSnapshot()
        : this(new())
    { }

    public MockOptionsSnapshot(SettingsType settings)
    {
        SetupGet(x => x.Value).Returns(settings);
        Setup(x => x.Get(It.IsAny<string>())).Returns(settings);
    }

    SettingsType IOptions<SettingsType>.Value => Object.Value;

    SettingsType IOptionsSnapshot<SettingsType>.Get(string? name) => Object.Get(name!);
}
