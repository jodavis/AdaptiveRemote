using AdaptiveRemote.Services.Testing;

namespace AdaptiveRemote.EndtoEndTests;

public static class ITestEndpointExtensions
{
    /// <summary>
    /// Creates a test service using the test service provider.
    /// This method supports both the old direct-create flow and the new service provider flow.
    /// </summary>
    public static ContractType CreateTestService<ContractType, ServiceType>(this ITestEndpoint controlService, TimeSpan timeout)
        where ServiceType : ContractType
    {
        return WaitHelpers.WaitForAsyncTask(
            controlService.CreateTestServiceAsync<ContractType, ServiceType>,
            timeout);
    }

    /// <summary>
    /// Creates a test service asynchronously using the test service provider.
    /// This method first gets the service provider (building the host if needed), then creates the service.
    /// </summary>
    public static async Task<ContractType> CreateTestServiceAsync<ContractType, ServiceType>(
        this ITestEndpoint controlService,
        CancellationToken cancellationToken)
        where ServiceType : ContractType
    {
        // Get the test service provider (this will build the host if not already built)
        ITestServiceProvider serviceProvider = await controlService.GetTestServiceProviderAsync(cancellationToken);

        return await serviceProvider.CreateTestServiceAsync<ContractType, ServiceType>(cancellationToken);
    }

    /// <summary>
    /// Creates a test service from the service provider.
    /// </summary>
    public static ContractType CreateTestService<ContractType, ServiceType>(this ITestServiceProvider serviceProvider, TimeSpan timeout)
        where ServiceType : ContractType
    {
        return WaitHelpers.WaitForAsyncTask(ct => serviceProvider.CreateTestServiceAsync<ContractType, ServiceType>(ct), timeout);
    }

    /// <summary>
    /// Creates a test service asynchronously from the service provider.
    /// </summary>
    public static async Task<ContractType> CreateTestServiceAsync<ContractType, ServiceType>(this ITestServiceProvider serviceProvider, CancellationToken cancellationToken)
        where ServiceType : ContractType
    {
        Func<string, string, CancellationToken, Task> constructor = typeof(ContractType).Name switch
        {
            nameof(IApplicationTestService) => serviceProvider.CreateTestServiceAsync,
            nameof(IUITestService) => serviceProvider.CreateUITestServiceAsync,
            nameof(ITestLogger) => serviceProvider.CreateTestLoggerAsync,
            nameof(ISpeechTestService) => serviceProvider.CreateSpeechTestServiceAsync,
            _ => throw new InvalidOperationException($"There is no method on ITestServiceProvider to create a service of type {typeof(ContractType).AssemblyQualifiedName}")
        };

        Task<ContractType> constructorTask = (Task<ContractType>)constructor(
            typeof(ServiceType).Assembly.Location,
            typeof(ServiceType).FullName!,
            cancellationToken);

        return await constructorTask;
    }

    /// <summary>
    /// Injects a test service to replace an application service before the host is built.
    /// </summary>
    public static async Task InjectTestServiceAsync<TContract, TService>(
        this ITestEndpoint testEndpoint,
        CancellationToken cancellationToken)
        where TService : TContract
    {
        await testEndpoint.AddTestServiceAsync(
            typeof(TContract).FullName!,
            typeof(TService).FullName!,
            typeof(TService).Assembly.Location,
            cancellationToken);
    }
}
