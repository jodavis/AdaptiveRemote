using AdaptiveRemote.Services.Testing;
using Microsoft.Playwright;

namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// UI test service implementation for the Headless host using Playwright.
/// </summary>
public class HeadlessUITestService : IUITestService
{
    private readonly IPage _page;
    private const int DefaultTimeoutMs = 2000;

    public HeadlessUITestService(AdaptiveRemote.Headless.PlaywrightBrowserLifetimeService browserService)
    {
        ArgumentNullException.ThrowIfNull(browserService);
        _page = browserService.Page ?? throw new InvalidOperationException("Playwright page is not available. Ensure the browser has been launched.");
    }

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
            // Check if the button is not disabled and not aria-disabled
            bool hasDisabledAttribute = await locator.GetAttributeAsync("disabled") != null;
            string? ariaDisabled = await locator.GetAttributeAsync("aria-disabled");
            bool isAriaDisabled = ariaDisabled == "true";
            
            return !hasDisabledAttribute && !isAriaDisabled;
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
        bool hasDisabledAttribute = await locator.GetAttributeAsync("disabled") != null;
        string? ariaDisabled = await locator.GetAttributeAsync("aria-disabled");
        bool isAriaDisabled = ariaDisabled == "true";
        
        if (hasDisabledAttribute || isAriaDisabled)
        {
            throw new InvalidOperationException($"Button with label '{label}' is not enabled.");
        }

        // Click the button
        await locator.ClickAsync(new LocatorClickOptions
        {
            Timeout = DefaultTimeoutMs
        });

        // Wait a short time for UI updates to settle
        await Task.Delay(100, cancellationToken);
    }

    private async Task<ILocator> GetButtonLocatorByLabelAsync(string label)
    {
        // Find all button elements
        ILocator allButtons = _page.Locator("button");
        int count = await allButtons.CountAsync();

        List<ILocator> matchingButtons = new();

        for (int i = 0; i < count; i++)
        {
            ILocator button = allButtons.Nth(i);
            string? buttonText = await button.TextContentAsync();
            
            // Trim and compare case-sensitive
            if (buttonText?.Trim() == label)
            {
                matchingButtons.Add(button);
            }
        }

        if (matchingButtons.Count == 0)
        {
            throw new InvalidOperationException($"No button found with label '{label}'.");
        }

        if (matchingButtons.Count > 1)
        {
            throw new InvalidOperationException($"Multiple buttons found with label '{label}'. Please disambiguate.");
        }

        return matchingButtons[0];
    }

    public void Dispose()
    {
        // Page is managed by PlaywrightBrowserLifetimeService, no cleanup needed
        GC.SuppressFinalize(this);
    }
}
