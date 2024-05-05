using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Conversation;

internal class MockOptions<TOptions> : IOptions<TOptions>, IOptionsSnapshot<TOptions>
    where TOptions : class, new()
{
    public TOptions Value { get; } = new();

    public TOptions Get(string? name) => throw new NotImplementedException();
}
