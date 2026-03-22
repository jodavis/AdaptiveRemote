using AdaptiveRemote.Services.Testing;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Extensions.Logging;
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

    public PlaywrightUITestService(IBrowserUIAccess browserProvider, ILogger<PlaywrightUITestService> logger)
    {
        _browserProvider = browserProvider;
        Logger = logger;

        // Warm up Playwright
        CurrentPage = _browserProvider.CurrentPage as IPage
            ?? throw new InvalidOperationException("IBrowserUIAccess service did not provide an object of type IPage");
    }

    private IPage CurrentPage { get; }

    protected ILogger<PlaywrightUITestService> Logger { get; }

    public virtual async Task<IReadOnlyList<AccessibilityViolation>> CheckAccessibilityAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Starting accessibility check using axe...");
        try
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
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while running accessibility checker: {Message}", ex.Message);
            throw;
        }
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
        return label switch
        {
            "Channel Down" => GetButtonLocatorByLabel("Down").Nth(1),
            "Channel Up" => GetButtonLocatorByLabel("Up").Nth(1),
            "Volume Down" => GetButtonLocatorByLabel("Down").Nth(2),
            "Volume Up" => GetButtonLocatorByLabel("Up").Nth(2),
            _ => CurrentPage.GetByRole(AriaRole.Button, new() { Name = label, Exact = true })
                .Describe($"button with label '{label}'")
        };
    }

    public async Task ClickTextAsync(string text, CancellationToken cancellationToken = default)
    {
        ILocator locator = GetTextLocator(text);

        // Verify the text is visible
        bool isVisible = await locator.IsVisibleAsync();
        if (!isVisible)
        {
            throw new InvalidOperationException($"Text '{text}' is not visible.");
        }

        // Click the text element
        await locator.ClickAsync(new LocatorClickOptions
        {
            Timeout = DefaultTimeoutMs
        });
    }

    private ILocator GetTextLocator(string text)
    {
        // Use Playwright's getByText with partial match (contains)
        return CurrentPage.GetByText(text, new() { Exact = false });
    }

    public async Task<string?> GetTextFromElementWithCssClassAsync(string cssClass, CancellationToken cancellationToken = default)
    {
        try
        {
            // Find elements with the specified CSS class
            ILocator locator = CurrentPage.Locator($".{cssClass}");

            // Check if at least one matching element is visible
            if (await locator.First.IsVisibleAsync())
            {
                // Return the text content of the first visible element
                return await locator.First.TextContentAsync();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetInnerHtmlFromElementWithCssClassAsync(string cssClass, CancellationToken cancellationToken = default)
    {
        try
        {
            ILocator locator = CurrentPage.Locator($".{cssClass}");

            if (await locator.First.IsVisibleAsync())
            {
                return await locator.First.InnerHTMLAsync();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public async Task<IUIButtonTestObject?> FindButtonByLabelAsync(string label, CancellationToken cancellationToken)
    {
        ILocator locator = GetButtonLocatorByLabel(label);

        if (await locator.CountAsync() == 0)
        {
            return null;
        }

        PlaywrightButtonTestObject button = new(locator);

        await button.ValidateLocatorAsync();

        return button;
    }
}
