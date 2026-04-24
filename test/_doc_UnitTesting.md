# Unit Test Guidelines

Unit tests live in `test/AdaptiveRemote.App.Tests`. Stack: MSTest + Moq + FluentAssertions.

## Naming

```
ClassName_Method_Scenario_ExpectedResult
```

Example: `TiVoService_InitializeAsync_WaitsForTiVoLocator`

## Structure

Use AAA (Arrange-Act-Assert):

- **`[TestInitialize]`** — mock setup and default expectations
- **`[TestCleanup]`** — `Mock.Verify()` calls
- **`Expect_*` helpers** — group related setup calls to keep test bodies readable

```csharp
[TestClass]
public class FooServiceTests
{
    private readonly Mock<IDependency> MockDependency = new();

    private FooService CreateSut() => new FooService(MockDependency.Object);

    [TestInitialize]
    public void SetupMocks()
    {
        MockDependency
            .Setup(x => x.DoSomething())
            .Verifiable(Times.Never);
    }

    [TestCleanup]
    public void VerifyMocks() => MockDependency.Verify();

    [TestMethod]
    public void FooService_DoWork_CallsDependency()
    {
        Expect_DependencyDoesWork();

        var sut = CreateSut();
        sut.DoWork();

        MockDependency.Verify(x => x.DoSomething(), Times.Once);
    }

    private void Expect_DependencyDoesWork()
    {
        MockDependency
            .Setup(x => x.DoSomething())
            .Verifiable(Times.Once);
    }
}
```

## Async / Task Patterns

Use `TaskCompletionSource` to represent an operation that stays incomplete:

```csharp
private static readonly Task IncompleteTask = new TaskCompletionSource().Task;
```

Assert task state without `await` — assert on the `Task` object directly:

```csharp
task.Should().BeComplete();
task.Should().NotBeComplete();
task.Should().BeCanceled();
task.Should().BeFaultedWith(expectedException);
```

Do not `await` a task when you want to assert its synchronous completion; awaiting it will block until it finishes, hiding a bug where the task should have been complete already.

## Log Verification

Log messages are verified via `MockLogger.VerifyMessages`:

```csharp
private readonly MockLogger<FooService> MockLogger = new();

[TestMethod]
public void FooService_DoWork_LogsStarted()
{
    var sut = CreateSut();
    sut.DoWork();

    MockLogger.VerifyMessages(log =>
    {
        log.FooServiceStarted();
    });
}
```

Never assert on raw log strings — always use the typed `MessageLogger` methods so tests stay in sync with the source-generated log definitions.

## Mocking Patterns

Set verifiable expectations in `[TestInitialize]` to a safe default (e.g. `Times.Never`), then override them per-test with `Expect_*` helpers. This makes unexpected calls fail automatically:

```csharp
[TestInitialize]
public void SetupMocks()
{
    MockDependency
        .Setup(x => x.ExpensiveOp())
        .Verifiable(Times.Never);  // fails if called when not expected
}

private void Expect_ExpensiveOpCalledOnce()
{
    MockDependency
        .Setup(x => x.ExpensiveOp())
        .Verifiable(Times.Once);
}
```

## What to Test

- Logic that can go wrong: branching, error handling, async sequencing
- Log output for operations that have observable side effects in production
- Do not test framework or DI wiring — test the behaviour of the class under test

## What Not to Test

- Constructors and property getters with no logic
- Code paths that can only fail if .NET itself is broken
- Integration between classes — that belongs in E2E tests
