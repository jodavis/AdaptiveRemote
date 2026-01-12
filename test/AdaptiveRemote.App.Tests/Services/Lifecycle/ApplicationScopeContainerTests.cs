using FluentAssertions;
using Moq;

namespace AdaptiveRemote.Services.Lifecycle;

[TestClass]
public class ApplicationScopeContainerTests
{
    private readonly Mock<IApplicationScope> MockScope = new();
    private readonly Mock<IApplicationScope> MockScope2 = new();
    private readonly Mock<IServiceProvider> MockServiceProvider = new();

    public TestContext? TestContext { get; set; }

    private static ApplicationScopeContainer CreateSut() => new();

    [TestInitialize]
    public void Setup()
    {
        // Default behavior: invoke the provided work using the mock service provider
        MockScope
            .Setup(s => s.InvokeInScopeAsync(It.IsAny<Func<IServiceProvider, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<IServiceProvider, CancellationToken, Task>, CancellationToken>((work, ct) => work.Invoke(MockServiceProvider.Object, ct));
        MockScope
            .Setup(s => s.RecycleAsync())
            .Returns(Task.CompletedTask);

        MockScope2
            .Setup(s => s.InvokeInScopeAsync(It.IsAny<Func<IServiceProvider, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<IServiceProvider, CancellationToken, Task>, CancellationToken>((work, ct) => work.Invoke(MockServiceProvider.Object, ct));
        MockScope2
            .Setup(s => s.RecycleAsync())
            .Returns(Task.CompletedTask);
    }

    [TestCleanup]
    public void VerifyMocks()
    {
        MockScope.Verify();
        MockServiceProvider.Verify();
    }

    [TestMethod]
    public void InvokeInScopeAsync_UsesCurrentScope()
    {
        // Arrange
        Expect_InvokeInScopeAsyncOn(MockScope);

        IApplicationScopeProvider sut = CreateSut();
        SetScope(sut, MockScope);

        bool called = false;
        Func<IServiceProvider, CancellationToken, Task> workItem = async (sp, ct) =>
        {
            called = true;
            await Task.CompletedTask;
        };

        // Act
        Task invokeTask = sut.InvokeInScopeAsync(workItem, default);

        // Assert
        invokeTask.Should().BeComplete(because: "workitem can be invoked synchronously");
        called.Should().BeTrue(because: "InvokeInScopeAsync should call back the work item delegate");
    }

    [TestMethod]
    [Timeout(1000)]
    public void InvokeInScopeAsync_DoesNotCallWorkitemIfScopeIsNotSet()
    {
        // Arrange
        Expect_InvokeInScopeAsyncOn(MockScope, Times.Never());

        IApplicationScopeProvider sut = CreateSut();

        bool called = false;
        Func<IServiceProvider, CancellationToken, Task> workItem = async (sp, ct) =>
        {
            called = true;
            await Task.CompletedTask;
        };

        // Act
        Task invokeTask = sut.InvokeInScopeAsync(workItem, default);

        // Assert
        invokeTask.Should().NotBeComplete(because: "there is no IApplicationScope to call back the work item");
        called.Should().BeFalse(because: "there is no IApplicationScope to call back the work item");
    }

    [TestMethod]
    [Timeout(1000)]
    public void InvokeInScopeAsync_DoesNotCallWorkitemIfScopeHasBeenReleased()
    {
        // Arrange
        Expect_InvokeInScopeAsyncOn(MockScope, Times.Never());
        Expect_RecycleAsyncOn(MockScope, Times.Never());

        IApplicationScopeProvider sut = CreateSut();
        SetScope(sut, MockScope);

        bool called = false;
        Func<IServiceProvider, CancellationToken, Task> workItem = async (sp, ct) =>
        {
            called = true;
            await Task.CompletedTask;
        };

        ((IApplicationScopeContainer)sut).ReleaseScopeAsync(MockScope.Object).Should().BeComplete(because: "there is nothing for ReleaseScopeAsync to wait for");

        // Act
        Task invokeTask = sut.InvokeInScopeAsync(workItem, default);

        // Assert
        invokeTask.Should().NotBeComplete(because: "there is no IApplicationScope to call back the work item (MockScope was recycled)");
        called.Should().BeFalse(because: "there is no IApplicationScope to call back the work item (MockScope was recycled)");
    }

    [TestMethod]
    [Timeout(1000)]
    public void InvokeInScopeAsync_DoesNotCallWorkitemIfScopeHasBeenRecycled()
    {
        // Arrange
        Expect_InvokeInScopeAsyncOn(MockScope, Times.Never());
        Expect_RecycleAsyncOn(MockScope);

        IApplicationScopeProvider sut = CreateSut();
        SetScope(sut, MockScope);

        bool called = false;
        Func<IServiceProvider, CancellationToken, Task> workItem = async (sp, ct) =>
        {
            called = true;
            await Task.CompletedTask;
        };

        sut.RecycleScopeAsync().Should().BeComplete(because: "there is nothing for RecycleAsync to wait for");

        // Act
        Task invokeTask = sut.InvokeInScopeAsync(workItem, default);

        // Assert
        invokeTask.Should().NotBeComplete(because: "there is no IApplicationScope to call back the work item (MockScope was recycled)");
        called.Should().BeFalse(because: "there is no IApplicationScope to call back the work item (MockScope was recycled)");
    }

    [TestMethod]
    [Timeout(1000)]
    public void InvokeInScopeAsync_DoesNotCallWorkitemIfScopeIsWaitingToBeRecycled()
    {
        // Arrange
        Expect_InvokeInScopeAsyncOn(MockScope, Times.Never());
        Expect_RecycleAsyncOn(MockScope, new TaskCompletionSource().Task);

        IApplicationScopeProvider sut = CreateSut();
        SetScope(sut, MockScope);

        bool called = false;
        Func<IServiceProvider, CancellationToken, Task> workItem = async (sp, ct) =>
        {
            called = true;
            await Task.CompletedTask;
        };

        sut.RecycleScopeAsync().Should().NotBeComplete(because: "it is waiting for MockScope.RecycleAsync");

        // Act
        Task invokeTask = sut.InvokeInScopeAsync(workItem, default);

        // Assert
        invokeTask.Should().NotBeComplete(because: "there is no IApplicationScope to call back the work item (MockScope was recycled)");
        called.Should().BeFalse(because: "there is no IApplicationScope to call back the work item (MockScope was recycled)");
    }

    [TestMethod]
    [Timeout(1000)]
    public void InvokeInScopeAsync_DoesNotCallWorkitemIfScopeHasBeenRecycledEvenWhenWaitingOnOtherWorkItems()
    {
        // Arrange
        Expect_InvokeInScopeAsyncOn(MockScope, Times.Once());
        Expect_RecycleAsyncOn(MockScope, Times.Never());

        IApplicationScopeProvider sut = CreateSut();
        SetScope(sut, MockScope);

        bool cancelled = false;
        Func<IServiceProvider, CancellationToken, Task> workItem1 = (sp, ct) =>
        {
            ct.Register(() => cancelled = true);
            return new TaskCompletionSource().Task;
        };
        Task invokeTask1 = sut.InvokeInScopeAsync(workItem1, default);

        bool called = false;
        Func<IServiceProvider, CancellationToken, Task> workItem2 = async (sp, ct) =>
        {
            called = true;
            await Task.CompletedTask;
        };

        sut.RecycleScopeAsync().Should().NotBeComplete(because: "it is waiting on workItem1 to complete");

        // Act
        Task invokeTask2 = sut.InvokeInScopeAsync(workItem2, default);

        // Assert
        invokeTask2.Should().NotBeCompleteWithin(TimeSpan.FromMilliseconds(100),
            because: "there is no IApplicationScope to call back the work item (MockScope was recycled)");
        called.Should().BeFalse(because: "there is no IApplicationScope to call back the work item (MockScope was recycled)");
        cancelled.Should().BeTrue(because: "workItem1 should be cancelled when the scope is being recycled");
    }

    [TestMethod]
    [Timeout(1000)]
    public void InvokeInScopeAsync_DoesNotCallWorkitemIfScopeHasBeenReleasedEvenWhenWaitingOnOtherWorkItems()
    {
        // Arrange
        Expect_InvokeInScopeAsyncOn(MockScope, Times.Once());
        Expect_RecycleAsyncOn(MockScope, Times.Never());

        IApplicationScopeProvider sut = CreateSut();
        SetScope(sut, MockScope);

        bool cancelled = false;
        Func<IServiceProvider, CancellationToken, Task> workItem1 = (sp, ct) =>
        {
            ct.Register(() => cancelled = true);
            return new TaskCompletionSource().Task;
        };
        Task invokeTask1 = sut.InvokeInScopeAsync(workItem1, default);

        bool called = false;
        Func<IServiceProvider, CancellationToken, Task> workItem2 = async (sp, ct) =>
        {
            called = true;
            await Task.CompletedTask;
        };

        Task releaseTask = ((IApplicationScopeContainer)sut).ReleaseScopeAsync(MockScope.Object);
        releaseTask.Should().NotBeComplete(because: "it is waiting on workItem1 to complete");

        // Act
        Task invokeTask2 = sut.InvokeInScopeAsync(workItem2, default);

        // Assert
        invokeTask2.Should().NotBeCompleteWithin(TimeSpan.FromMilliseconds(100),
            because: "there is no IApplicationScope to call back the work item (MockScope was recycled)");
        called.Should().BeFalse(because: "there is no IApplicationScope to call back the work item (MockScope was recycled)");
        cancelled.Should().BeTrue(because: "workItem1 should be cancelled when the scope is being recycled");
    }

    [TestMethod]
    [Timeout(1000)]
    public void InvokeInScopeAsync_CallsBackWorkItemAfterScopeIsSet()
    {
        // Arrange
        Expect_InvokeInScopeAsyncOn(MockScope);

        IApplicationScopeProvider sut = CreateSut();

        bool called = false;
        Func<IServiceProvider, CancellationToken, Task> workItem = async (sp, ct) =>
        {
            called = true;
            await Task.CompletedTask;
        };

        Task invokeTask = sut.InvokeInScopeAsync(workItem, default);

        // Act
        SetScope(sut, MockScope);

        // Assert
        invokeTask.Should().BeComplete(because: "workitem can be invoked synchronously after the scope is set");
        called.Should().BeTrue(because: "InvokeInScopeAsync should call back the work item delegate after the scope is set");
    }

    [TestMethod]
    [Timeout(1000)]
    public void InvokeInScopeAsync_AwaitsWorkItemAfterCallback()
    {
        // Arrange
        Expect_InvokeInScopeAsyncOn(MockScope);

        IApplicationScopeProvider sut = CreateSut();
        SetScope(sut, MockScope);

        bool called = false;
        Func<IServiceProvider, CancellationToken, Task> workItem = async (sp, ct) =>
        {
            called = true;
            await new TaskCompletionSource().Task;
        };

        // Act
        Task invokeTask = sut.InvokeInScopeAsync(workItem, default);

        // Assert
        invokeTask.Should().NotBeComplete(because: "work item has not yet completed");
        called.Should().BeTrue(because: "InvokeInScopeAsync should call back the work item delegate after the scope is set");
    }

    [TestMethod]
    [Timeout(1000)]
    public void InvokeInScopeAsync_CompletesWhenWorkItemCompletes()
    {
        // Arrange
        Expect_InvokeInScopeAsyncOn(MockScope);

        IApplicationScopeProvider sut = CreateSut();
        SetScope(sut, MockScope);

        bool called = false;
        TaskCompletionSource workItemTcs = new();
        Func<IServiceProvider, CancellationToken, Task> workItem = async (sp, ct) =>
        {
            called = true;
            await workItemTcs.Task;
        };

        Task invokeTask = sut.InvokeInScopeAsync(workItem, default);

        // Act
        workItemTcs.SetResult();

        // Assert
        invokeTask.Should().BeComplete(because: "work item has completed");
        called.Should().BeTrue(because: "InvokeInScopeAsync should call back the work item delegate after the scope is set");
    }

    [TestMethod]
    [Timeout(1000)]
    public void InvokeInScopeAsync_ShouldBeCancelledByCancellationToken()
    {
        // Arrange
        Expect_InvokeInScopeAsyncOn(MockScope);

        IApplicationScopeProvider sut = CreateSut();
        SetScope(sut, MockScope);

        bool cancelled = false;
        Func<IServiceProvider, CancellationToken, Task> workItem = async (sp, ct) =>
        {
            ct.Register(() => cancelled = true);
            await new TaskCompletionSource().Task;
        };

        using CancellationTokenSource cts = new();

        Task invokeTask = sut.InvokeInScopeAsync(workItem, cts.Token);

        // Act
        cts.Cancel();

        // Assert
        invokeTask.Should().NotBeComplete(because: "work item has not yet completed");
        cancelled.Should().BeTrue(because: "CancellationToken was passed in and then triggered");
    }

    [TestMethod]
    [Timeout(1000)]
    public void RecycleScopeAsync_DoesNothingIfNoScopeWasSet()
    {
        // Arrange
        Expect_RecycleAsyncOn(MockScope, Times.Never());

        IApplicationScopeProvider sut = CreateSut();

        // Act
        Task recycleTask = sut.RecycleScopeAsync();

        // Assert
        recycleTask.Should().BeComplete(because: "no scope was set so there is nothing to do");
    }

    [TestMethod]
    [Timeout(1000)]
    public void RecycleScopeAsync_DoesNotCompleteUntilRecycleAsyncCompletes()
    {
        // Arrange
        Expect_RecycleAsyncOn(MockScope, new TaskCompletionSource().Task);

        IApplicationScopeProvider sut = CreateSut();
        SetScope(sut, MockScope);

        // Act
        Task recycleTask = sut.RecycleScopeAsync();

        // Assert
        recycleTask.Should().NotBeComplete(because: "RecycleAsync on the MockScope is not complete");
    }

    [TestMethod]
    [Timeout(1000)]
    public void RecycleScopeAsync_CompletesWhenRecycleAsyncCompletes()
    {
        // Arrange
        TaskCompletionSource recycleTcs = new();
        Expect_RecycleAsyncOn(MockScope, recycleTcs.Task);

        IApplicationScopeProvider sut = CreateSut();
        SetScope(sut, MockScope);

        Task recycleTask = sut.RecycleScopeAsync();

        // Act
        recycleTcs.SetResult();

        // Assert
        recycleTask.Should().BeComplete(because: "RecycleAsync on the MockScope has completed");
    }

    [TestMethod]
    public void RecycleScopeAsync_WaitsForInvokesToCompleteBeforeRecycleAsync()
    {
        // Arrange
        Expect_RecycleAsyncOn(MockScope, Times.Never());

        IApplicationScopeProvider sut = CreateSut();
        SetScope(sut, MockScope);

        Task invokeTask = sut.InvokeInScopeAsync((sp, ct) => new TaskCompletionSource().Task, default);

        // Act
        Task recycleTask = sut.RecycleScopeAsync();

        // Assert
        recycleTask.Should().NotBeComplete(because: "work items in the current scope are not complete");
    }

    [TestMethod]
    public void RecycleScopeAsync_ReleasesAndWaitsForInvokes()
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

        IApplicationScopeProvider sut = CreateSut();
        SetScope(sut, MockScope);

        // Act
        Task invokeTask = sut.InvokeInScopeAsync(async (sp, ct) =>
        {
            await Task.Delay(10, ct);
        }, default);

        Task releaseTask = sut.RecycleScopeAsync();

        // Assert - release should be waiting
        releaseTask.Should().NotBeComplete(because: "an InvokeInScopeAsync task is still incomplete");

        invokeTcs.SetResult();

        releaseTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "the InvokeInScopeAsync task has completed");
    }

    [TestMethod]
    public void SetScopeAsync_ReplacesAnExistingScope()
    {
        // Arrange
        Expect_InvokeInScopeAsyncOn(MockScope, Times.Never());
        Expect_InvokeInScopeAsyncOn(MockScope2, Times.Once());

        IApplicationScopeContainer sut = CreateSut();
        IApplicationScopeProvider provider = (IApplicationScopeProvider)sut;

        sut.SetScopeAsync(MockScope.Object).Should().BeComplete(
            because: "there is nothing for it to wait on");

        // Act
        Task setScopeTask = sut.SetScopeAsync(MockScope2.Object);

        // Assert
        setScopeTask.Should().BeComplete("there is nothing for it to wait on");

        // Act
        Task invokeTask = provider.InvokeInScopeAsync((sp, ct) => Task.CompletedTask, default);

        // Assert
        invokeTask.Should().BeComplete(because: "it should be invoked on MockScope2");
    }

    [TestMethod]
    public void SetScopeAsync_ReplacesAnExistingScopeAndCancelsPreviousScopeTasks()
    {
        // Arrange
        Expect_InvokeInScopeAsyncOn(MockScope, Times.Once());
        Expect_InvokeInScopeAsyncOn(MockScope2, Times.Once());

        IApplicationScopeContainer sut = CreateSut();
        IApplicationScopeProvider provider = (IApplicationScopeProvider)sut;

        sut.SetScopeAsync(MockScope.Object).Should().BeComplete(
            because: "there is nothing for it to wait on");

        CancellationTokenSource result = new();
        Task preInvokeTask = provider.InvokeInScopeAsync((sp, ct) =>
        {
            ct.Register(result.Cancel);
            return new TaskCompletionSource().Task;
        }, default);

        // Act
        Task setScopeTask = sut.SetScopeAsync(MockScope2.Object);

        // Assert
        setScopeTask.Should().BeComplete("there is nothing for it to wait on");

        // Act
        Task invokeTask = provider.InvokeInScopeAsync((sp, ct) => Task.CompletedTask, default);

        // Assert
        invokeTask.Should().BeComplete(because: "it should be invoked on MockScope2");
        result.Token.WaitForCancelled().Should().BeCompleteWithin(TimeSpan.FromMilliseconds(100),
            because: "The scope that the task was started in is no longer the current scope");
    }

    [TestMethod]
    public void ReleaseScopeAsync_DoesNothingIfScopeNotSet()
    {
        // Arrange
        Expect_RecycleAsyncOn(MockScope, Times.Never());

        IApplicationScopeContainer sut = CreateSut();

        // Act
        Task releaseTask = sut.ReleaseScopeAsync(MockScope.Object);

        // Assert
        releaseTask.Should().BeComplete(because: "there is no current scope so there is nothing for it to do");
    }

    [TestMethod]
    public void ReleaseScopeAsync_ReleasesAnExistingScope()
    {
        // Arrange
        Expect_RecycleAsyncOn(MockScope, Times.Never());

        IApplicationScopeContainer sut = CreateSut();

        sut.SetScopeAsync(MockScope.Object).Should().BeComplete(
            because: "there is nothing for it to wait on");

        // Act
        Task releaseTask = sut.ReleaseScopeAsync(MockScope.Object);

        // Assert
        releaseTask.Should().BeComplete(because: "there is nothing for it to wait on");
    }

    [TestMethod]
    public void ReleaseScopeAsync_DoesNothingIfInputScopeIsNotTheCurrentScope()
    {
        // Arrange
        Expect_RecycleAsyncOn(MockScope, Times.Never());
        Expect_RecycleAsyncOn(MockScope2, Times.Never());
        Expect_InvokeInScopeAsyncOn(MockScope);
        Expect_InvokeInScopeAsyncOn(MockScope2, Times.Never());

        IApplicationScopeContainer sut = CreateSut();

        sut.SetScopeAsync(MockScope.Object).Should().BeComplete(
            because: "the method is not actually going to do anything");

        // Act
        Task releaseTask = sut.ReleaseScopeAsync(MockScope2.Object);

        // Assert
        releaseTask.Should().BeComplete(because: "there is nothing for it to wait on");

        Task invokeTask = ((IApplicationScopeProvider)sut).InvokeInScopeAsync((sp, ct) => Task.CompletedTask, default);
        invokeTask.Should().BeComplete(because: "MockScope is still the active scope");
    }

    private void Expect_InvokeInScopeAsyncOn(Mock<IApplicationScope> scope, Times? times = default)
        => scope
            .Setup(x => x.InvokeInScopeAsync(It.IsAny<Func<IServiceProvider, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<IServiceProvider, CancellationToken, Task>, CancellationToken>((work, ct) => work.Invoke(MockServiceProvider.Object, ct))
            .Verifiable(times ?? Times.Once());
    private static void Expect_RecycleAsyncOn(Mock<IApplicationScope> scope, Times? times = default)
        => scope
            .Setup(s => s.RecycleAsync())
            .Returns(Task.CompletedTask)
            .Verifiable(times ?? Times.Once());
    private static void Expect_RecycleAsyncOn(Mock<IApplicationScope> scope, Task resultTask)
        => scope
            .Setup(s => s.RecycleAsync())
            .Returns(resultTask)
            .Verifiable(Times.Once);

    private static void SetScope(IApplicationScopeProvider provider, Mock<IApplicationScope> scope)
    {
        IApplicationScopeContainer? container = provider as IApplicationScopeContainer;
        container.Should().NotBeNull(because: "ApplicationScopeContainer implements both IApplicationScopeProvider and IApplicationScopeContainer");

        Task setScopeTask = container!.SetScopeAsync(scope.Object);
        setScopeTask.Should().BeCompleteWithin(TimeSpan.FromSeconds(1), because: "SetScopeAsync should not take long to complete for initialization");
    }
}
