using AdaptiveRemote.Services.Testing;

namespace AdaptiveRemote.EndtoEndTests;

public static class ITestEndpointExtensions
{
    public static async Task<IApplicationTestService> CreateTestServiceAsync<ServiceType>(this ITestEndpoint controlService, CancellationToken cancellationToken = default)
        where ServiceType : IApplicationTestService
        => await controlService.CreateTestServiceAsync(
            typeof(ServiceType).Assembly.Location,
            typeof(ServiceType).FullName!,
            cancellationToken);

    public static async Task<ITestLogger> CreateTestLoggerAsync<ServiceType>(this ITestEndpoint controlService, CancellationToken cancellationToken = default)
        where ServiceType : ITestLogger
        => await controlService.CreateTestLoggerAsync(
            typeof(ServiceType).Assembly.Location,
            typeof(ServiceType).FullName!,
            cancellationToken);
}
