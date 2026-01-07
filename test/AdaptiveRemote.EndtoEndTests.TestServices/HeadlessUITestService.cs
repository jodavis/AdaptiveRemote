using AdaptiveRemote.Services.Testing;
using Microsoft.Playwright;

namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// UI test service implementation for the Headless host using Playwright.
/// </summary>
public class HeadlessUITestService : UITestServiceBase
{
    private readonly IBrowserProvider _browserProvider;

    public HeadlessUITestService(IBrowserProvider browserProvider)
    {
        _browserProvider = browserProvider ?? throw new ArgumentNullException(nameof(browserProvider));
    }

    protected override Task<IPage> GetPageAsync()
    {
        IPage page = _browserProvider.TestContext as IPage ?? throw new InvalidOperationException("Playwright page is not available. Ensure the browser has been launched.");
        return Task.FromResult(page);
    }

    public override void Dispose()
    {
        // Page is managed by the browser provider, no cleanup needed
        GC.SuppressFinalize(this);
    }
}
