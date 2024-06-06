using AdaptiveRemote.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AdaptiveRemote.Services.Lifecycle;

[TestClass]
public class ApplicationLifecycleTests
{
    // TODO
    // Logging
    // Cleanup
    // Error handling

    private readonly Mock<IScopedLifecycle> MockService1 = new();
    private readonly Mock<IScopedLifecycle> MockService2 = new();
    private readonly Mock<IScopedLifecycle> MockService3 = new();

    private readonly Mock<IApplicationScopeFactory> MockScopeFactory = new();
    private readonly Mock<IApplicationScope> MockScope = new();
    private readonly Mock<IServiceProvider> MockServiceProvider = new();

    private ApplicationLifecycle CreateSut() => new ApplicationLifecycle(MockScopeFactory.Object);

    [TestInitialize]
    public void SetupMocks()
    {
        MockScopeFactory
            .Setup(x => x.CreateNewScopeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(MockScope.Object))
            .Verifiable(Times.Once);

        MockScope
            .Setup(x => x.TryInvokeAsync(It.IsAny<Func<IServiceProvider, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(delegate (Func<IServiceProvider, CancellationToken, Task> workItem, CancellationToken cancellationToken)
            {
                return workItem.Invoke(MockServiceProvider.Object, cancellationToken);
            });

        MockServiceProvider
            .Setup(x => x.GetService(typeof(IEnumerable<IScopedLifecycle>)))
            .Returns(new[] { MockService1.Object, MockService2.Object, MockService3.Object })
            .Verifiable(Times.Once);
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        Verify(MockServiceProvider, nameof(MockServiceProvider));
        Verify(MockScopeFactory, nameof(MockScopeFactory));
        Verify(MockScope, nameof(MockScope));

        Verify(MockService1, nameof(MockService1));
        Verify(MockService2, nameof(MockService2));
        Verify(MockService3, nameof(MockService3));

        static void Verify(Mock mock, string name)
        {
            try
            {
                mock.Verify();
            }
            catch (MockException e)
            {
                throw new Exception($"Verify failed on {name}: {e.Message}");
            }
        }
    }

    [TestMethod]
    public void ApplicationLifecycle_StartAsync_StartsExecuteTaskAndInitializesScopedLifecycleServices()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);

        // Act
        Task startTask = sut.StartAsync(default);

        // Assert
        TaskAssert.IsComplete(startTask, nameof(startTask));
        TaskAssert.IsNotComplete(sut.ExecuteTask, nameof(sut.ExecuteTask));
    }

    [TestMethod]
    public void ApplicationLifecycle_StopAsync_DisposesScopeAndCompletesTask()
    {
        // Arrange
        ApplicationLifecycle sut = CreateSut();

        Expect_InitializeAsyncOn(MockService1);
        Expect_InitializeAsyncOn(MockService2);
        Expect_InitializeAsyncOn(MockService3);
        Expect_DisposeScope();

        Task startTask = sut.StartAsync(default);

        // Act
        Task stopTask = sut.StopAsync(default);

        // Assert
        TaskAssert.IsComplete(startTask, nameof(startTask));
        TaskAssert.IsComplete(stopTask, nameof(stopTask));
        TaskAssert.IsComplete(sut.ExecuteTask, nameof(sut.ExecuteTask));
    }

    private void Expect_DisposeScope()
        => MockScope
            .Setup(x => x.Dispose())
            .Verifiable(Times.Once);

    private static void Expect_InitializeAsyncOn(Mock<IScopedLifecycle> service)
        => service
            .Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
            .Returns(new TaskCompletionSource().Task)
            .Verifiable(Times.Once);
}
