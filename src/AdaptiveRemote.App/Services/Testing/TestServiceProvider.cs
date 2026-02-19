using System.Reflection;
using AdaptiveRemote.Logging;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Provides test services from the host application after it has been built.
/// This allows tests to interact with services within the application's DI scope.
/// </summary>
internal class TestServiceProvider : ITestServiceProvider
{
    private readonly IApplicationScopeProvider _scopeProvider;
    private readonly MessageLogger _logger;

    public TestServiceProvider(IApplicationScopeProvider scopeProvider, ILogger<TestServiceProvider> logger)
    {
        _scopeProvider = scopeProvider;
        _logger = new(logger);
    }

    public Task<IApplicationTestService> CreateTestServiceAsync(string assemblyPath, string typeName, CancellationToken cancellationToken)
        => CreateRemotableServiceAsync<IApplicationTestService>(assemblyPath, typeName, cancellationToken);

    public Task<ITestLogger> CreateTestLoggerAsync(string assemblyPath, string typeName, CancellationToken cancellationToken)
        => CreateRemotableServiceAsync<ITestLogger>(assemblyPath, typeName, cancellationToken);

    public Task<IUITestService> CreateUITestServiceAsync(string assemblyPath, string typeName, CancellationToken cancellationToken)
        => CreateRemotableServiceAsync<IUITestService>(assemblyPath, typeName, cancellationToken);

    public Task<ISpeechTestService> CreateSpeechTestServiceAsync(string assemblyPath, string typeName, CancellationToken cancellationToken)
        => CreateRemotableServiceAsync<ISpeechTestService>(assemblyPath, typeName, cancellationToken);

    public void Dispose()
    {
        // No resources to dispose
    }

    private async Task<ServiceType> CreateRemotableServiceAsync<ServiceType>(string assemblyPath, string typeName, CancellationToken cancellationToken)
        where ServiceType : class
    {
        _logger.TestEndpointService_LoadingTestService(typeName, assemblyPath);

        Assembly assembly = Assembly.LoadFrom(assemblyPath);

        Type? serviceType = assembly.GetType(typeName)
            ?? throw new ArgumentException($"Type not found: {typeName}", nameof(typeName));

        if (!typeof(ServiceType).IsAssignableFrom(serviceType))
        {
            _logger.TestEndpointService_ServiceTypeIncompatible(typeName, typeof(ServiceType).FullName);
            throw new ArgumentException($"Type {typeName} does not implement {typeof(ServiceType).FullName}", nameof(typeName));
        }

        // Store the type to instantiate later within each scoped invocation
        _logger.TestEndpointService_LoadingTestServiceSucceeded();

        // Create the test service within the application scope so it gets access to scoped services
        ServiceType? testService = null;
        await _scopeProvider.InvokeInScopeAsync((scopedProvider, ct) =>
        {
            testService = (ServiceType)ActivatorUtilities.CreateInstance(scopedProvider, serviceType);
            return Task.CompletedTask;
        }, cancellationToken);

        return testService ?? throw new InvalidOperationException("Failed to create test service instance");
    }
}
