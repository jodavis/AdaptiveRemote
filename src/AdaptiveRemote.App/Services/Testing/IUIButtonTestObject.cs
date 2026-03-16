using PolyType;
using StreamJsonRpc;

namespace AdaptiveRemote.Services.Testing;

[RpcMarshalable]
[JsonRpcContract]
[GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface IUIButtonTestObject : IDisposable
{
    /// <summary>
    /// Checks if the button is visible in the UI.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if the button is visible, false otherwise.</returns>
    Task<bool> IsVisibleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the button is enabled in the UI.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if the button is enabled, false otherwise.</returns>
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the button appears to have been programmed (UI shows it has IR data).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if the button appears to be programmed; false otherwise.</returns>
    Task<bool> IsProgrammedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clicks the button in the UI.
    /// The button must be visible and enabled before clicking.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task ClickAsync(CancellationToken cancellationToken = default);
}
