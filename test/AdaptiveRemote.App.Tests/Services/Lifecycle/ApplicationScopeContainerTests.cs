using FluentAssertions;
using Moq;

namespace AdaptiveRemote.Services.Lifecycle;

[TestClass]
public class ApplicationScopeContainerTests
{
    private readonly Mock<IApplicationScope> MockScope = new();
    private readonly Mock<IServiceProvider> MockServiceProvider = new();

    public TestContext? TestContext { get; set; }

    [TestInitialize]
    public void Setup()
    {
        // Default behavior: invoke the provided work using the mock service provider
        MockScope
            .Setup(s => s.InvokeInScopeAsync(It.IsAny<Func<IServiceProvider, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<IServiceProvider, CancellationToken, Task>, CancellationToken>((work, ct) => work.Invoke(MockServiceProvider.Object, ct));

        MockScope
            .Setup(s => s.RecycleAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        MockScope.Verify();
        MockServiceProvider.Verify();
    }

    [TestMethod]
    public async Task InvokeInScopeAsync_UsesCurrentScope()
    {
        // Arrange
        Expect_InvokeInScopeAsyncOn(MockScope);

        ApplicationScopeContainer container = new ApplicationScopeContainer();
        container.SetScope(MockScope.Object);

        bool called = false;

        // Act
        await ((IApplicationScopeProvider)container).InvokeInScopeAsync(async (sp, ct) =>
        {
            called = true;
            await Task.CompletedTask;
        }, default);

        // Assert
        called.Should().BeTrue();
    }

    [TestMethod]
    public async Task ReleaseScopeAsync_ReleasesAndWaitsForInvokes()
    {
        // Arrange
        TaskCompletionSource invokeTcs = new TaskCompletionSource();

        MockScope
            .Setup(s => s.InvokeInScopeAsync(It.IsAny<Func<IServiceProvider, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<IServiceProvider, CancellationToken, Task>, CancellationToken>(async (work, ct) =>
            {
                await invokeTcs.Task;
            })
            .Verifiable();

        Expect_RecycleAsyncOn(MockScope);

        ApplicationScopeContainer container = new ApplicationScopeContainer();
        container.SetScope(MockScope.Object);

        // Act
        Task invokeTask = ((IApplicationScopeProvider)container).InvokeInScopeAsync(async (sp, ct) =>
        {
            await Task.Delay(10, ct);
        }, default);

        Task releaseTask = ((IApplicationScopeProvider)container).RecycleScopeAsync(default);

        // Assert - release should be waiting
        releaseTask.IsCompleted.Should().BeFalse();

        invokeTcs.SetResult();

        await releaseTask; // should complete now
    }

    [TestMethod]
    public async Task ReleaseScopeAsync_RecyclesScope()
    {
        // Arrange
        Expect_RecycleAsyncOn(MockScope);

        ApplicationScopeContainer container = new ApplicationScopeContainer();
        container.SetScope(MockScope.Object);

        // Act
        await ((IApplicationScopeProvider)container).RecycleScopeAsync(default);

        // Assert - verified in TestCleanup
    }

    private void Expect_InvokeInScopeAsyncOn(Mock<IApplicationScope> scope, Task? result = default)
    {
        if (result is not null)
        {
            scope
                .Setup(s => s.InvokeInScopeAsync(It.IsAny<Func<IServiceProvider, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
                .Returns(result)
                .Verifiable();
        }
        else
        {
            scope
                .Setup(s => s.InvokeInScopeAsync(It.IsAny<Func<IServiceProvider, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
                .Returns<Func<IServiceProvider, CancellationToken, Task>, CancellationToken>((work, ct) => work.Invoke(MockServiceProvider.Object, ct))
                .Verifiable();
        }
    }

    private static void Expect_RecycleAsyncOn(Mock<IApplicationScope> scope)
    {
        scope
            .Setup(s => s.RecycleAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();
    }
}
