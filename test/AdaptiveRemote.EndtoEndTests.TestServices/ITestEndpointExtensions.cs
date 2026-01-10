using AdaptiveRemote.Services.Testing;

namespace AdaptiveRemote.EndtoEndTests;

public static class ITestEndpointExtensions
{
    public static ContractType CreateTestService<ContractType, ServiceType>(this ITestEndpoint controlService, TimeSpan timeout)
        where ServiceType : ContractType
    {
        return WaitHelpers.WaitForAsyncTask(ct => controlService.CreateTestServiceAsync<ContractType, ServiceType>(ct), timeout);
    }

    public static async Task<ContractType> CreateTestServiceAsync<ContractType, ServiceType>(this ITestEndpoint controlService, CancellationToken cancellationToken)
        where ServiceType : ContractType
    {
        Func<string, string, CancellationToken, Task> constructor = typeof(ContractType).Name switch
        {
            nameof(IApplicationTestService) => controlService.CreateTestServiceAsync,
            nameof(IUITestService) => controlService.CreateUITestServiceAsync,
            nameof(ITestLogger) => controlService.CreateTestLoggerAsync,
            _ => throw new InvalidOperationException($"There is no method on ITestEndpoint to create a service of type {typeof(ContractType).AssemblyQualifiedName}")
        };

        Task<ContractType> constructorTask = (Task<ContractType>)constructor(
            typeof(ServiceType).Assembly.Location,
            typeof(ServiceType).FullName!,
            cancellationToken);

        return await constructorTask;
    }
}
