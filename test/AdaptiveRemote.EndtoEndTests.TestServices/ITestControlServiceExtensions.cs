using AdaptiveRemote.Services.Testing;

namespace AdaptiveRemote.EndtoEndTests;

public static class ITestControlServiceExtensions
{
    public static async Task<ITestService> CreateTestServiceAsync<ServiceType>(this ITestControlService controlService, CancellationToken cancellationToken = default)
        where ServiceType : ITestService
        => await controlService.CreateTestServiceAsync(
            typeof(ServiceType).Assembly.Location,
            typeof(ServiceType).FullName!,
            cancellationToken);

    public static async Task<ITestLogger> CreateTestLoggerAsync<ServiceType>(this ITestControlService controlService, CancellationToken cancellationToken = default)
        where ServiceType : ITestLogger
        => await controlService.CreateTestLoggerAsync(
            typeof(ServiceType).Assembly.Location,
            typeof(ServiceType).FullName!,
            cancellationToken);
}
