using Microsoft.Extensions.Options;
using Moq;

namespace AdaptiveRemote.TestUtilities;

internal class MockOptions<SettingsType> : Mock<IOptions<SettingsType>>, IOptions<SettingsType>
    where SettingsType : class, new()
{
    public MockOptions()
        : this(new())
    { }

    public MockOptions(SettingsType settings)
    {
        SetupGet(x => x.Value)
            .Returns(settings);
    }

    SettingsType IOptions<SettingsType>.Value => Object.Value;
}
