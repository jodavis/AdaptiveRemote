using AdaptiveRemote.Services.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace AdaptiveRemote.Services.Testing;

/// <summary>
/// Proxy for ITestService that creates actual test service instances within the application scope
/// when methods are called. This allows the test service to be created before the scope exists.
/// </summary>
internal class TestServiceProxy : ITestService
{
    private readonly Type _testServiceType;
    private readonly IApplicationScopeProvider _scopeProvider;

    public TestServiceProxy(Type testServiceType, IApplicationScopeProvider scopeProvider)
    {
        _testServiceType = testServiceType;
        _scopeProvider = scopeProvider;
    }

    public async Task WaitForPhaseAsync(LifecyclePhase phase, CancellationToken cancellationToken = default)
    {
        await InvokeMethodAsync(nameof(WaitForPhaseAsync), new object?[] { phase, cancellationToken });
    }

    public async Task InvokeCommandAsync(string commandId, CancellationToken cancellationToken = default)
    {
        await InvokeMethodAsync(nameof(InvokeCommandAsync), new object?[] { commandId, cancellationToken });
    }

    private async Task<object?> InvokeMethodAsync(string methodName, object?[]? args)
    {
        object? result = null;
        await _scopeProvider.InvokeInScopeAsync(async (scopedProvider, ct) =>
        {
            // Create test service instance with scoped services
            object testService = ActivatorUtilities.CreateInstance(scopedProvider, _testServiceType);

            MethodInfo? method = testService.GetType().GetMethod(methodName);
            if (method is null)
            {
                throw new InvalidOperationException($"Method not found: {methodName}");
            }

            object? methodResult = method.Invoke(testService, args);

            if (methodResult is Task task)
            {
                await task;

                // Check if it's Task<T>
                Type resultType = task.GetType();
                if (resultType.IsGenericType)
                {
                    PropertyInfo? resultProperty = resultType.GetProperty("Result");
                    result = resultProperty?.GetValue(task);
                }
            }
            else
            {
                result = methodResult;
            }
        }, CancellationToken.None);

        return result;
    }

    public void Dispose()
    {
        // Nothing to dispose
        GC.SuppressFinalize(this);
    }
}
