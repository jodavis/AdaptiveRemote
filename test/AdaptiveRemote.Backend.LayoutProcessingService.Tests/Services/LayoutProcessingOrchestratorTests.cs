using AdaptiveRemote.Backend.LayoutProcessingService.Configuration;
using AdaptiveRemote.Backend.LayoutProcessingService.Services;
using AdaptiveRemote.Contracts;
using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AdaptiveRemote.Backend.LayoutProcessingService.Tests.Services;

/// <summary>
/// Unit tests for LayoutProcessingOrchestrator covering:
/// - Success path: compile, validate, store, notify, delete
/// - Validation failure path: write-back, no store, no notify, delete
/// - Processing error path: no delete (SQS retry)
/// </summary>
[TestClass]
public class LayoutProcessingOrchestratorTests
{
    private Mock<IAmazonSQS> _mockSqs = null!;
    private Mock<IRawLayoutRepository> _mockRawLayoutRepository = null!;
    private Mock<IRawLayoutStatusWriter> _mockRawLayoutStatusWriter = null!;
    private Mock<ILayoutCompilerClient> _mockCompilerClient = null!;
    private Mock<ILayoutValidationClient> _mockValidationClient = null!;
    private Mock<ICompiledLayoutRepository> _mockCompiledLayoutRepository = null!;
    private Mock<INotificationPublisher> _mockNotificationPublisher = null!;
    private Mock<ILogger<LayoutProcessingOrchestrator>> _mockLogger = null!;

    private static readonly string TestQueueUrl = "http://localhost:4566/000000000000/TestQueue";
    private static readonly string TestUserId = "user-123";
    private static readonly Guid TestRawLayoutId = Guid.NewGuid();

    [TestInitialize]
    public void Setup()
    {
        _mockSqs = new Mock<IAmazonSQS>();
        _mockRawLayoutRepository = new Mock<IRawLayoutRepository>();
        _mockRawLayoutStatusWriter = new Mock<IRawLayoutStatusWriter>();
        _mockCompilerClient = new Mock<ILayoutCompilerClient>();
        _mockValidationClient = new Mock<ILayoutValidationClient>();
        _mockCompiledLayoutRepository = new Mock<ICompiledLayoutRepository>();
        _mockNotificationPublisher = new Mock<INotificationPublisher>();
        _mockLogger = new Mock<ILogger<LayoutProcessingOrchestrator>>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        // VerifyNoOtherCalls is not applied to _mockSqs because ReceiveMessageAsync is called
        // on every poll iteration and the exact call count is non-deterministic in tests that
        // use a semaphore to signal completion. DeleteMessageAsync and other SQS calls are
        // asserted explicitly in each test.
        _mockRawLayoutRepository.VerifyNoOtherCalls();
        _mockRawLayoutStatusWriter.VerifyNoOtherCalls();
        _mockCompilerClient.VerifyNoOtherCalls();
        _mockValidationClient.VerifyNoOtherCalls();
        _mockCompiledLayoutRepository.VerifyNoOtherCalls();
        _mockNotificationPublisher.VerifyNoOtherCalls();
    }

    // ─── Success path ──────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task LayoutProcessingOrchestrator_ProcessMessageAsync_SuccessPath_CompilesValidatesStoresAndNotifies()
    {
        // Arrange
        RawLayout rawLayout = CreateTestRawLayout();
        CompiledLayout compiledLayout = CreateTestCompiledLayout(rawLayout.Id);
        ValidationResult validResult = new(true, Array.Empty<ValidationIssue>());
        CompiledLayout savedLayout = compiledLayout with { Id = Guid.NewGuid() };

        // First receive returns one message; second receive (after processing) returns empty to allow cancellation
        SemaphoreSlim messageProcessed = new(0, 1);
        int receiveCount = 0;

        _mockSqs
            .Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (receiveCount++ == 0)
                {
                    return CreateReceiveMessageResponse(TestRawLayoutId);
                }

                // Block until cancellation to allow the test to verify
                messageProcessed.Release();
                return new ReceiveMessageResponse { Messages = [] };
            });

        _mockRawLayoutRepository
            .Setup(r => r.GetAsync(TestRawLayoutId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rawLayout);

        _mockCompilerClient
            .Setup(c => c.CompileAsync(It.IsAny<RawLayout>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(compiledLayout);

        _mockValidationClient
            .Setup(v => v.ValidateAsync(It.IsAny<CompiledLayout>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validResult);

        _mockCompiledLayoutRepository
            .Setup(r => r.SaveAsync(It.IsAny<CompiledLayout>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedLayout);

        _mockNotificationPublisher
            .Setup(p => p.PublishLayoutReadyAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockSqs
            .Setup(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteMessageResponse());

        // Act
        LayoutProcessingOrchestrator orchestrator = CreateOrchestrator();
        CancellationTokenSource cts = new();
        await orchestrator.StartAsync(cts.Token);

        // Wait for the message to be processed (second receive poll signals this)
        bool processed = await messageProcessed.WaitAsync(TimeSpan.FromSeconds(10));
        processed.Should().BeTrue(because: "message should be processed within 10 seconds");

        await cts.CancelAsync();
        await orchestrator.StopAsync(CancellationToken.None);

        // Assert
        _mockRawLayoutRepository.Verify(r => r.GetAsync(TestRawLayoutId, It.IsAny<CancellationToken>()), Times.Once);
        _mockCompilerClient.Verify(c => c.CompileAsync(It.IsAny<RawLayout>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockValidationClient.Verify(v => v.ValidateAsync(It.IsAny<CompiledLayout>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockCompiledLayoutRepository.Verify(r => r.SaveAsync(It.IsAny<CompiledLayout>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockNotificationPublisher.Verify(p => p.PublishLayoutReadyAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockSqs.Verify(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task LayoutProcessingOrchestrator_ProcessMessageAsync_SuccessPath_DoesNotCallStatusWriter()
    {
        // Arrange
        RawLayout rawLayout = CreateTestRawLayout();
        CompiledLayout compiledLayout = CreateTestCompiledLayout(rawLayout.Id);
        ValidationResult validResult = new(true, Array.Empty<ValidationIssue>());
        CompiledLayout savedLayout = compiledLayout with { Id = Guid.NewGuid() };

        SemaphoreSlim messageProcessed = new(0, 1);
        int receiveCount = 0;

        _mockSqs
            .Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (receiveCount++ == 0)
                {
                    return CreateReceiveMessageResponse(TestRawLayoutId);
                }

                messageProcessed.Release();
                return new ReceiveMessageResponse { Messages = [] };
            });

        _mockRawLayoutRepository
            .Setup(r => r.GetAsync(TestRawLayoutId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rawLayout);
        _mockCompilerClient
            .Setup(c => c.CompileAsync(It.IsAny<RawLayout>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(compiledLayout);
        _mockValidationClient
            .Setup(v => v.ValidateAsync(It.IsAny<CompiledLayout>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validResult);
        _mockCompiledLayoutRepository
            .Setup(r => r.SaveAsync(It.IsAny<CompiledLayout>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedLayout);
        _mockNotificationPublisher
            .Setup(p => p.PublishLayoutReadyAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockSqs
            .Setup(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteMessageResponse());

        // Act
        LayoutProcessingOrchestrator orchestrator = CreateOrchestrator();
        CancellationTokenSource cts = new();
        await orchestrator.StartAsync(cts.Token);

        bool processed = await messageProcessed.WaitAsync(TimeSpan.FromSeconds(10));
        processed.Should().BeTrue();

        await cts.CancelAsync();
        await orchestrator.StopAsync(CancellationToken.None);

        // Assert — IRawLayoutStatusWriter must NOT be called on success
        _mockRawLayoutRepository.Verify(r => r.GetAsync(TestRawLayoutId, It.IsAny<CancellationToken>()), Times.Once);
        _mockCompilerClient.Verify(c => c.CompileAsync(It.IsAny<RawLayout>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockValidationClient.Verify(v => v.ValidateAsync(It.IsAny<CompiledLayout>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockCompiledLayoutRepository.Verify(r => r.SaveAsync(It.IsAny<CompiledLayout>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockNotificationPublisher.Verify(p => p.PublishLayoutReadyAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockRawLayoutStatusWriter.Verify(
            w => w.UpdateValidationResultAsync(It.IsAny<Guid>(), It.IsAny<ValidationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── Validation failure path ────────────────────────────────────────────────────

    [TestMethod]
    public async Task LayoutProcessingOrchestrator_ProcessMessageAsync_ValidationFailure_WritesBackAndDeletesMessage()
    {
        // Arrange
        RawLayout rawLayout = CreateTestRawLayout();
        CompiledLayout compiledLayout = CreateTestCompiledLayout(rawLayout.Id);
        ValidationResult invalidResult = new(
            IsValid: false,
            Issues: new[] { new ValidationIssue("ERR001", "Label is empty", "/elements/0/label") }
        );

        SemaphoreSlim messageProcessed = new(0, 1);
        int receiveCount = 0;

        _mockSqs
            .Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (receiveCount++ == 0)
                {
                    return CreateReceiveMessageResponse(TestRawLayoutId);
                }

                messageProcessed.Release();
                return new ReceiveMessageResponse { Messages = [] };
            });

        _mockRawLayoutRepository
            .Setup(r => r.GetAsync(TestRawLayoutId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rawLayout);
        _mockCompilerClient
            .Setup(c => c.CompileAsync(It.IsAny<RawLayout>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(compiledLayout);
        _mockValidationClient
            .Setup(v => v.ValidateAsync(It.IsAny<CompiledLayout>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invalidResult);
        _mockRawLayoutStatusWriter
            .Setup(w => w.UpdateValidationResultAsync(TestRawLayoutId, It.IsAny<ValidationResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockSqs
            .Setup(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteMessageResponse());

        // Act
        LayoutProcessingOrchestrator orchestrator = CreateOrchestrator();
        CancellationTokenSource cts = new();
        await orchestrator.StartAsync(cts.Token);

        bool processed = await messageProcessed.WaitAsync(TimeSpan.FromSeconds(10));
        processed.Should().BeTrue();

        await cts.CancelAsync();
        await orchestrator.StopAsync(CancellationToken.None);

        // Assert
        _mockRawLayoutRepository.Verify(r => r.GetAsync(TestRawLayoutId, It.IsAny<CancellationToken>()), Times.Once);
        _mockCompilerClient.Verify(c => c.CompileAsync(It.IsAny<RawLayout>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockValidationClient.Verify(v => v.ValidateAsync(It.IsAny<CompiledLayout>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockRawLayoutStatusWriter.Verify(
            w => w.UpdateValidationResultAsync(TestRawLayoutId, It.IsAny<ValidationResult>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockCompiledLayoutRepository.Verify(
            r => r.SaveAsync(It.IsAny<CompiledLayout>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockNotificationPublisher.Verify(
            p => p.PublishLayoutReadyAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockSqs.Verify(
            s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── SQS retry behavior ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task LayoutProcessingOrchestrator_ProcessMessageAsync_ProcessingError_DoesNotDeleteMessage()
    {
        // Arrange — compile throws; message should NOT be deleted so SQS retries it
        RawLayout rawLayout = CreateTestRawLayout();
        InvalidOperationException compileException = new("Compile failed");

        SemaphoreSlim messageProcessed = new(0, 1);
        int receiveCount = 0;

        _mockSqs
            .Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (receiveCount++ == 0)
                {
                    return CreateReceiveMessageResponse(TestRawLayoutId);
                }

                messageProcessed.Release();
                return new ReceiveMessageResponse { Messages = [] };
            });

        _mockRawLayoutRepository
            .Setup(r => r.GetAsync(TestRawLayoutId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rawLayout);
        _mockCompilerClient
            .Setup(c => c.CompileAsync(It.IsAny<RawLayout>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(compileException);

        // Act
        LayoutProcessingOrchestrator orchestrator = CreateOrchestrator();
        CancellationTokenSource cts = new();
        await orchestrator.StartAsync(cts.Token);

        bool processed = await messageProcessed.WaitAsync(TimeSpan.FromSeconds(10));
        processed.Should().BeTrue();

        await cts.CancelAsync();
        await orchestrator.StopAsync(CancellationToken.None);

        // Assert — message is NOT deleted on error; SQS will retry
        _mockRawLayoutRepository.Verify(r => r.GetAsync(TestRawLayoutId, It.IsAny<CancellationToken>()), Times.Once);
        _mockCompilerClient.Verify(c => c.CompileAsync(It.IsAny<RawLayout>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockSqs.Verify(
            s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockValidationClient.Verify(v => v.ValidateAsync(It.IsAny<CompiledLayout>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockCompiledLayoutRepository.Verify(
            r => r.SaveAsync(It.IsAny<CompiledLayout>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockNotificationPublisher.Verify(
            p => p.PublishLayoutReadyAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockRawLayoutStatusWriter.Verify(
            w => w.UpdateValidationResultAsync(It.IsAny<Guid>(), It.IsAny<ValidationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task LayoutProcessingOrchestrator_ProcessMessageAsync_RawLayoutNotFound_DeletesMessage()
    {
        // Arrange — raw layout not found; message deleted without further pipeline steps
        SemaphoreSlim messageProcessed = new(0, 1);
        int receiveCount = 0;

        _mockSqs
            .Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (receiveCount++ == 0)
                {
                    return CreateReceiveMessageResponse(TestRawLayoutId);
                }

                messageProcessed.Release();
                return new ReceiveMessageResponse { Messages = [] };
            });

        _mockRawLayoutRepository
            .Setup(r => r.GetAsync(TestRawLayoutId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RawLayout?)null);

        _mockSqs
            .Setup(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteMessageResponse());

        // Act
        LayoutProcessingOrchestrator orchestrator = CreateOrchestrator();
        CancellationTokenSource cts = new();
        await orchestrator.StartAsync(cts.Token);

        bool processed = await messageProcessed.WaitAsync(TimeSpan.FromSeconds(10));
        processed.Should().BeTrue();

        await cts.CancelAsync();
        await orchestrator.StopAsync(CancellationToken.None);

        // Assert
        _mockRawLayoutRepository.Verify(r => r.GetAsync(TestRawLayoutId, It.IsAny<CancellationToken>()), Times.Once);
        _mockSqs.Verify(
            s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockCompilerClient.Verify(
            c => c.CompileAsync(It.IsAny<RawLayout>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockValidationClient.Verify(v => v.ValidateAsync(It.IsAny<CompiledLayout>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockCompiledLayoutRepository.Verify(r => r.SaveAsync(It.IsAny<CompiledLayout>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockNotificationPublisher.Verify(p => p.PublishLayoutReadyAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRawLayoutStatusWriter.Verify(
            w => w.UpdateValidationResultAsync(It.IsAny<Guid>(), It.IsAny<ValidationResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── Unrecognized message body ─────────────────────────────────────────────────

    [TestMethod]
    public async Task LayoutProcessingOrchestrator_ProcessMessageAsync_UnrecognizedMessageBody_DeletesMessageWithoutPipelineSteps()
    {
        // Arrange — message body is not a valid GUID
        const string InvalidBody = "not-a-guid";
        string receiptHandle = $"receipt-{Guid.NewGuid():N}";

        SemaphoreSlim messageProcessed = new(0, 1);
        int receiveCount = 0;

        _mockSqs
            .Setup(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (receiveCount++ == 0)
                {
                    return new ReceiveMessageResponse
                    {
                        Messages =
                        [
                            new Message
                            {
                                MessageId = Guid.NewGuid().ToString(),
                                ReceiptHandle = receiptHandle,
                                Body = InvalidBody,
                                Attributes = new Dictionary<string, string>
                                {
                                    ["ApproximateReceiveCount"] = "1"
                                }
                            }
                        ]
                    };
                }

                messageProcessed.Release();
                return new ReceiveMessageResponse { Messages = [] };
            });

        _mockSqs
            .Setup(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteMessageResponse());

        // Act
        LayoutProcessingOrchestrator orchestrator = CreateOrchestrator();
        CancellationTokenSource cts = new();
        await orchestrator.StartAsync(cts.Token);

        bool processed = await messageProcessed.WaitAsync(TimeSpan.FromSeconds(10));
        processed.Should().BeTrue(because: "unrecognized message should be deleted within 10 seconds");

        await cts.CancelAsync();
        await orchestrator.StopAsync(CancellationToken.None);

        // Assert — message is deleted and no pipeline steps are invoked
        _mockSqs.Verify(
            s => s.DeleteMessageAsync(
                It.Is<DeleteMessageRequest>(r => r.ReceiptHandle == receiptHandle),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _mockRawLayoutRepository.Verify(
            r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockCompilerClient.Verify(
            c => c.CompileAsync(It.IsAny<RawLayout>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockValidationClient.Verify(
            v => v.ValidateAsync(It.IsAny<CompiledLayout>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockCompiledLayoutRepository.Verify(
            r => r.SaveAsync(It.IsAny<CompiledLayout>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockNotificationPublisher.Verify(
            p => p.PublishLayoutReadyAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────────

    private LayoutProcessingOrchestrator CreateOrchestrator()
    {
        SqsSettings settings = new()
        {
            QueueUrl = TestQueueUrl,
            MaxNumberOfMessages = 10,
            VisibilityTimeoutSeconds = 60,
            WaitTimeSeconds = 0,   // No long-poll in tests
            EmptyQueueDelayMs = 0  // No delay in tests
        };

        return new LayoutProcessingOrchestrator(
            _mockSqs.Object,
            Options.Create(settings),
            _mockRawLayoutRepository.Object,
            _mockRawLayoutStatusWriter.Object,
            _mockCompilerClient.Object,
            _mockValidationClient.Object,
            _mockCompiledLayoutRepository.Object,
            _mockNotificationPublisher.Object,
            _mockLogger.Object);
    }

    private static ReceiveMessageResponse CreateReceiveMessageResponse(Guid rawLayoutId)
    {
        return new ReceiveMessageResponse
        {
            Messages =
            [
                new Message
                {
                    MessageId = Guid.NewGuid().ToString(),
                    ReceiptHandle = $"receipt-{Guid.NewGuid():N}",
                    Body = rawLayoutId.ToString(),
                    Attributes = new Dictionary<string, string>
                    {
                        ["ApproximateReceiveCount"] = "1"
                    }
                }
            ]
        };
    }

    private static RawLayout CreateTestRawLayout()
    {
        return new RawLayout(
            Id: TestRawLayoutId,
            UserId: TestUserId,
            Name: "Test Layout",
            Elements: new[]
            {
                new RawCommandDefinitionDto(
                    Type: CommandType.TiVo,
                    Name: "Play",
                    Label: "Play",
                    Glyph: null,
                    SpeakPhrase: "Play",
                    Reverse: "Pause",
                    CssId: "PLAY",
                    GridRow: 1,
                    GridColumn: 1)
            },
            Version: 1,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            ValidationResult: null
        );
    }

    private static CompiledLayout CreateTestCompiledLayout(Guid rawLayoutId)
    {
        return new CompiledLayout(
            Id: Guid.NewGuid(),
            RawLayoutId: rawLayoutId,
            UserId: TestUserId,
            IsActive: false,
            Version: 1,
            Elements: new[]
            {
                new CommandDefinitionDto(
                    Type: CommandType.TiVo,
                    Name: "Play",
                    Label: "Play",
                    Glyph: null,
                    SpeakPhrase: "Play",
                    Reverse: "Pause",
                    CssId: "PLAY")
            },
            CssDefinitions: string.Empty,
            CompiledAt: DateTimeOffset.UtcNow
        );
    }
}
