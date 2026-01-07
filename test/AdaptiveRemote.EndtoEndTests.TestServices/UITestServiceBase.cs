using AdaptiveRemote.Services.Testing;
using Microsoft.Playwright;

namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// Base class for UI test service implementations that use Playwright.
/// Subclasses are responsible for providing the IPage instance.
/// </summary>
public abstract class UITestServiceBase : IUITestService
{
    private const int DefaultTimeoutMs = 2000;
    private const int ClickSettleDelayMs = 100;

    /// <summary>
    /// Gets the Playwright page instance for interacting with the UI.
    /// Subclasses must implement this to provide access to their specific page source.
    /// </summary>
    protected abstract Task<IPage> GetPageAsync();

    public async Task<bool> IsButtonVisibleAsync(string label, CancellationToken cancellationToken = default)
    {
        ILocator locator = await GetButtonLocatorByLabelAsync(label);
        
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
        ILocator locator = await GetButtonLocatorByLabelAsync(label);
        
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
        ILocator locator = await GetButtonLocatorByLabelAsync(label);
        
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

    private async Task<ILocator> GetButtonLocatorByLabelAsync(string label)
    {
        IPage page = await GetPageAsync();
        
        // Use Playwright's getByRole with exact match - it will throw meaningful errors
        // if there are no matches or ambiguous matches
        return page.GetByRole(AriaRole.Button, new() { Name = label, Exact = true });
    }

    public abstract void Dispose();
}
