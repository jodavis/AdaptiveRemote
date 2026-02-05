using PolyType;
using StreamJsonRpc;

namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Interface for the test control service that runs in the host.
/// Used for bootstrapping test services via JSON-RPC.
/// </summary>
[RpcMarshalable]
[JsonRpcContract]
[GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]
public partial interface ITestEndpoint
{
    /// <summary>
    /// Dynamically loads a test service from the specified assembly and type.
    /// The test service is instantiated within the application's DI scope to access scoped services.
    /// </summary>
    /// <param name="assemblyPath">Full path to the assembly containing the test service type.</param>
    /// <param name="typeName">Fully qualified name of the test service type to instantiate.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A proxy to the test service that can be used to invoke test commands.</returns>
    Task<IApplicationTestService> CreateTestServiceAsync(string assemblyPath, string typeName, CancellationToken cancellationToken);

    /// <summary>
    /// Dynamically loads a test logger from the specified assembly and type.
    /// The test logger is instantiated within the application's DI scope so it can access scoped services
    /// and forward log events back to the host test harness.
    /// </summary>
    /// <param name="assemblyPath">Full path to the assembly containing the test logger type.</param>
    /// <param name="typeName">Fully qualified name of the test logger type to instantiate.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A proxy to the test logger that can be used by tests to emit or collect log events.</returns>
    Task<ITestLogger> CreateTestLoggerAsync(string assemblyPath, string typeName, CancellationToken cancellationToken);

    /// <summary>
    /// Dynamically loads a UI test service from the specified assembly and type.
    /// The UI test service is instantiated within the application's DI scope so it can access
    /// Playwright/WebView2 objects and interact with the UI.
    /// </summary>
    /// <param name="assemblyPath">Full path to the assembly containing the UI test service type.</param>
    /// <param name="typeName">Fully qualified name of the UI test service type to instantiate.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A proxy to the UI test service that can be used to interact with the UI.</returns>
    Task<IUITestService> CreateUITestServiceAsync(string assemblyPath, string typeName, CancellationToken cancellationToken);
}
