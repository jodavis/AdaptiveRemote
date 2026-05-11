namespace AdaptiveRemote.EndtoEndTests.Host;

public interface ITestJwtAuthority : IDisposable
{
    string Authority { get; }
    string TokenEndpointUrl { get; }
    string ValidClientId { get; }
    string ValidClientSecret { get; }
}
