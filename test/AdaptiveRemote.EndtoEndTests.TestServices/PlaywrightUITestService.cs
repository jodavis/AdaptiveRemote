using AdaptiveRemote.Services.Testing;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// Base class for UI test service implementations that use Playwright.
/// Subclasses are responsible for providing the IPage instance.
/// </summary>
public class PlaywrightUITestService : IUITestService
{
    private const int DefaultTimeoutMs = 2000;

    private readonly IBrowserUIAccess _browserProvider;

    public PlaywrightUITestService(IBrowserUIAccess browserProvider)
    {
        _browserProvider = browserProvider;

        // Start warming up Playwright if necessary
        _ = Task.Run(() => _ = CurrentPage);
    }

    private IPage CurrentPage => _browserProvider.CurrentPage as IPage
        ?? throw new InvalidOperationException("IBrowserProvider service did not provide an object of type IPage");

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
            return await locator.IsEnabledAsync();
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
    }

    public async Task<IReadOnlyList<AccessibilityViolation>> CheckAccessibilityAsync(CancellationToken cancellationToken = default)
    {
        // Run axe on the current page
        AxeResult axeResults = await CurrentPage.RunAxe();

        // Convert axe violations to our AccessibilityViolation format
        List<AccessibilityViolation> violations = new();

        foreach (AxeResultItem violation in axeResults.Violations)
        {
            foreach (AxeResultNode node in violation.Nodes)
            {
                violations.Add(new AccessibilityViolation
                {
                    RuleId = violation.Id,
                    Impact = violation.Impact ?? "unknown",
                    Description = violation.Description,
                    HtmlSnippet = node.Html,
                    HelpText = violation.Help,
                    HelpUrl = violation.HelpUrl
                });
            }
        }

        return violations;
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
        // Use Playwright's getByRole with exact match - it will throw meaningful errors
        // if there are no matches or ambiguous matches
        return CurrentPage.GetByRole(AriaRole.Button, new() { Name = label, Exact = true });
    }

    public void Dispose()
    {
        if (_browserProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
