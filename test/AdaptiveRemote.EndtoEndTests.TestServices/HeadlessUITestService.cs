using AdaptiveRemote.Services.Testing;
using Microsoft.Playwright;

namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// UI test service implementation for the Headless host using Playwright.
/// </summary>
public class HeadlessUITestService : IUITestService
{
    private readonly IBrowserProvider _browserProvider;
    private const int DefaultTimeoutMs = 2000;
    private const int ClickSettleDelayMs = 100;

    public HeadlessUITestService(IBrowserProvider browserProvider)
    {
        _browserProvider = browserProvider ?? throw new ArgumentNullException(nameof(browserProvider));
    }

    public async Task<bool> IsButtonVisibleAsync(string label, CancellationToken cancellationToken = default)
    {
        ILocator locator = GetButtonLocatorByLabel(label);
        
        try
        {
            return await locator.IsVisibleAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsButtonEnabledAsync(string label, CancellationToken cancellationToken = default)
    {
        ILocator locator = GetButtonLocatorByLabel(label);
        
        try
        {
            return !await IsButtonDisabledAsync(locator);
        }
        catch
        {
            return false;
        }
    }

    public async Task ClickButtonAsync(string label, CancellationToken cancellationToken = default)
    {
        ILocator locator = GetButtonLocatorByLabel(label);
        
        // Verify the button is visible
        bool isVisible = await locator.IsVisibleAsync();
        if (!isVisible)
        {
            throw new InvalidOperationException($"Button with label '{label}' is not visible.");
        }

        // Verify the button is enabled
        if (await IsButtonDisabledAsync(locator))
        {
            throw new InvalidOperationException($"Button with label '{label}' is not enabled.");
        }

        // Click the button
        await locator.ClickAsync(new LocatorClickOptions
        {
            Timeout = DefaultTimeoutMs
        });

        // Wait a short time for UI updates to settle
        await Task.Delay(ClickSettleDelayMs, cancellationToken);
    }

    private static async Task<bool> IsButtonDisabledAsync(ILocator locator)
    {
        // Check if the button is disabled via attribute or aria-disabled
        bool hasDisabledAttribute = await locator.GetAttributeAsync("disabled") != null;
        string? ariaDisabled = await locator.GetAttributeAsync("aria-disabled");
        bool isAriaDisabled = ariaDisabled == "true";
        
        return hasDisabledAttribute || isAriaDisabled;
    }

    private ILocator GetButtonLocatorByLabel(string label)
    {
        IPage page = _browserProvider.BrowserPage as IPage ?? throw new InvalidOperationException("Playwright page is not available. Ensure the browser has been launched.");
        
        // Use Playwright's getByRole with exact match - it will throw meaningful errors
        // if there are no matches or ambiguous matches
        return page.GetByRole(AriaRole.Button, new() { Name = label, Exact = true });
    }

    public void Dispose()
    {
        // Page is managed by the browser provider, no cleanup needed
        GC.SuppressFinalize(this);
    }
}
