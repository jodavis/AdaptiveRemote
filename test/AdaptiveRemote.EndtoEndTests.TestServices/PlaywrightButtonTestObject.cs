using AdaptiveRemote.Services.Testing;
using Microsoft.Playwright;

namespace AdaptiveRemote.EndtoEndTests;

internal class PlaywrightButtonTestObject : IUIButtonTestObject
{
    private ILocator _locator;

    public PlaywrightButtonTestObject(ILocator locator)
    {
        _locator = locator;
    }

    internal async Task ValidateLocatorAsync()
    {
        switch (await _locator.CountAsync())
        {
            case 0:
                throw new InvalidOperationException($"{_locator.Description} did not match any elements.");
            case 1:
                return;
            default:
                throw new InvalidOperationException($"{_locator.Description} matched multiple elements.");
        }
    }

    public async Task ClickAsync(CancellationToken cancellationToken = default)
    {
        await ValidateLocatorAsync();

        // Verify the button is visible
        if (!await IsVisibleAsync(cancellationToken))
        {
            throw new InvalidOperationException($"{_locator.Description} is not visible.");
        }

        // Verify the button is enabled
        if (!await IsEnabledAsync(cancellationToken))
        {
            throw new InvalidOperationException($"{_locator.Description} is not enabled.");
        }

        await _locator.ClickAsync();
    }

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        await ValidateLocatorAsync();

        return await _locator.IsEnabledAsync();
    }

    public async Task<bool> IsProgrammedAsync(CancellationToken cancellationToken = default)
    {
        await ValidateLocatorAsync();

        // Check for the btn-programmed CSS class which provides visual distinction
        return await _locator.EvaluateAsync<bool>(
            "el => el.classList.contains('btn-programmed')");
    }

    public async Task<bool> IsVisibleAsync(CancellationToken cancellationToken = default)
    {
        await ValidateLocatorAsync();

        return await _locator.IsVisibleAsync();
    }

    public void Dispose() { }
}
