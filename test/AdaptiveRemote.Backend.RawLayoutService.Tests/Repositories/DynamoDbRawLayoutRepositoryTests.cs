using AdaptiveRemote.Backend.RawLayoutService.Configuration;
using AdaptiveRemote.Backend.RawLayoutService.Repositories;
using AdaptiveRemote.Contracts;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace AdaptiveRemote.Backend.RawLayoutService.Tests.Repositories;

[TestClass]
public class DynamoDbRawLayoutRepositoryTests
{
    private Mock<IAmazonDynamoDB> _mockDynamoDb = null!;
    private DynamoDbRawLayoutRepository _repository = null!;
    private const string TestTableName = "RawLayoutsTest";
    private const string TestUserId = "test-user-123";
    private const string OtherUserId = "other-user-456";

    [TestInitialize]
    public void Setup()
    {
        _mockDynamoDb = new Mock<IAmazonDynamoDB>();
        IOptions<DynamoDbSettings> settings = Options.Create(new DynamoDbSettings
        {
            TableName = TestTableName,
            Region = "us-east-1"
        });
        _repository = new DynamoDbRawLayoutRepository(_mockDynamoDb.Object, settings);

        // Default: no DynamoDB methods should be called unless a test explicitly sets them up
        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        _mockDynamoDb
            .Setup(db => db.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        _mockDynamoDb
            .Setup(db => db.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        _mockDynamoDb
            .Setup(db => db.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
        _mockDynamoDb
            .Setup(db => db.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .Verifiable(Times.Never);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _mockDynamoDb.Verify();
    }

    // ─── GetAsync ────────────────────────────────────────────────────────────────

    [TestMethod]
    public void GetAsync_WithValidId_ReturnsMatchingLayout()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        Guid decoyId = Guid.NewGuid();

        // Response includes a decoy item with a different ID to prove the code finds the right one
        TaskCompletionSource<ScanResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task<RawLayout?> resultTask = _repository.GetAsync(testId, CancellationToken.None);

        resultTask.Should().NotBeComplete(because: "ScanAsync has not completed yet");

        tcs.SetResult(new ScanResponse
        {
            Items =
            [
                CreateDynamoDbItem(decoyId, OtherUserId, "Decoy Layout", 1),
                CreateDynamoDbItem(testId, TestUserId, "Test Layout", 1)
            ]
        });

        // Assert
        resultTask.Should().BeComplete();
        RawLayout? result = resultTask.Result;
        result.Should().NotBeNull();
        result!.Id.Should().Be(testId);
        result.UserId.Should().Be(TestUserId);
        result.Name.Should().Be("Test Layout");
        result.Version.Should().Be(1);

        // Verify correct scan filter was sent to DynamoDB
        _mockDynamoDb.Verify(db => db.ScanAsync(
            It.Is<ScanRequest>(req =>
                req.FilterExpression == "Id = :id" &&
                req.ExpressionAttributeValues.ContainsKey(":id") &&
                req.ExpressionAttributeValues[":id"].S == testId.ToString()),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public void GetAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        TaskCompletionSource<ScanResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task<RawLayout?> resultTask = _repository.GetAsync(testId, CancellationToken.None);
        resultTask.Should().NotBeComplete(because: "ScanAsync has not completed yet");

        tcs.SetResult(new ScanResponse { Items = [] });

        // Assert
        resultTask.Should().BeComplete();
        resultTask.Result.Should().BeNull();
    }

    [TestMethod]
    public void GetAsync_WhenScanAsyncIsPending_TaskIsNotComplete()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        TaskCompletionSource<ScanResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task<RawLayout?> resultTask = _repository.GetAsync(testId, CancellationToken.None);

        // Assert
        resultTask.Should().NotBeComplete(because: "ScanAsync has not completed yet");

        tcs.SetResult(new ScanResponse { Items = [] });

        resultTask.Should().BeComplete(because: "ScanAsync has now completed");
    }

    [TestMethod]
    public void GetAsync_WhenCancelled_TaskIsCancelled()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        CancellationTokenSource cts = new();
        TaskCompletionSource<ScanResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task<RawLayout?> resultTask = _repository.GetAsync(testId, cts.Token);
        resultTask.Should().NotBeComplete(because: "ScanAsync has not completed yet");

        cts.Cancel();

        // Assert
        resultTask.Should().BeCanceled(because: "cancellation was requested");
    }

    [TestMethod]
    public void GetAsync_WhenScanThrowsException_TaskIsFaulted()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        AmazonDynamoDBException expectedException = new("DynamoDB scan failed");
        TaskCompletionSource<ScanResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task<RawLayout?> resultTask = _repository.GetAsync(testId, CancellationToken.None);
        resultTask.Should().NotBeComplete(because: "ScanAsync has not completed yet");

        tcs.SetException(expectedException);

        // Assert
        resultTask.Should().BeFaultedWith(expectedException);
    }

    // ─── ListByUserAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public void ListByUserAsync_ReturnsOnlyLayoutsForRequestedUser()
    {
        // Arrange
        Guid id1 = Guid.NewGuid();
        Guid id2 = Guid.NewGuid();
        Guid decoyId = Guid.NewGuid();

        // Response includes decoy items for a different user to prove UserId filtering is working
        TaskCompletionSource<QueryResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task<IReadOnlyList<RawLayout>> resultTask = _repository.ListByUserAsync(TestUserId, CancellationToken.None);
        resultTask.Should().NotBeComplete(because: "QueryAsync has not completed yet");

        tcs.SetResult(new QueryResponse
        {
            Items =
            [
                CreateDynamoDbItem(id1, TestUserId, "Layout 1", 1),
                CreateDynamoDbItem(decoyId, OtherUserId, "Decoy Layout", 1),
                CreateDynamoDbItem(id2, TestUserId, "Layout 2", 2)
            ]
        });

        // Assert
        resultTask.Should().BeComplete();
        IReadOnlyList<RawLayout> results = resultTask.Result;
        results.Should().HaveCount(2, because: "decoy items for other users should be filtered out");
        results[0].Id.Should().Be(id1);
        results[1].Id.Should().Be(id2);
        results.Should().OnlyContain(r => r.UserId == TestUserId);

        // Verify correct query parameters were sent to DynamoDB
        _mockDynamoDb.Verify(db => db.QueryAsync(
            It.Is<QueryRequest>(req =>
                req.KeyConditionExpression == "UserId = :userId" &&
                req.ExpressionAttributeValues.ContainsKey(":userId") &&
                req.ExpressionAttributeValues[":userId"].S == TestUserId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public void ListByUserAsync_WithNoLayouts_ReturnsEmptyList()
    {
        // Arrange
        TaskCompletionSource<QueryResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task<IReadOnlyList<RawLayout>> resultTask = _repository.ListByUserAsync(TestUserId, CancellationToken.None);
        resultTask.Should().NotBeComplete(because: "QueryAsync has not completed yet");

        tcs.SetResult(new QueryResponse { Items = [] });

        // Assert
        resultTask.Should().BeComplete();
        resultTask.Result.Should().BeEmpty();
    }

    [TestMethod]
    public void ListByUserAsync_WhenQueryAsyncIsPending_TaskIsNotComplete()
    {
        // Arrange
        TaskCompletionSource<QueryResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task<IReadOnlyList<RawLayout>> resultTask = _repository.ListByUserAsync(TestUserId, CancellationToken.None);

        // Assert
        resultTask.Should().NotBeComplete(because: "QueryAsync has not completed yet");

        tcs.SetResult(new QueryResponse { Items = [] });

        resultTask.Should().BeComplete(because: "QueryAsync has now completed");
    }

    [TestMethod]
    public void ListByUserAsync_WhenCancelled_TaskIsCancelled()
    {
        // Arrange
        CancellationTokenSource cts = new();
        TaskCompletionSource<QueryResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task<IReadOnlyList<RawLayout>> resultTask = _repository.ListByUserAsync(TestUserId, cts.Token);
        resultTask.Should().NotBeComplete(because: "QueryAsync has not completed yet");

        cts.Cancel();

        // Assert
        resultTask.Should().BeCanceled(because: "cancellation was requested");
    }

    [TestMethod]
    public void ListByUserAsync_WhenQueryThrowsException_TaskIsFaulted()
    {
        // Arrange
        AmazonDynamoDBException expectedException = new("DynamoDB query failed");
        TaskCompletionSource<QueryResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task<IReadOnlyList<RawLayout>> resultTask = _repository.ListByUserAsync(TestUserId, CancellationToken.None);
        resultTask.Should().NotBeComplete(because: "QueryAsync has not completed yet");

        tcs.SetException(expectedException);

        // Assert
        resultTask.Should().BeFaultedWith(expectedException);
    }

    // ─── SaveAsync ────────────────────────────────────────────────────────────────

    [TestMethod]
    public void SaveAsync_CreatesNewLayout()
    {
        // Arrange
        RawLayout layout = CreateTestLayout();
        TaskCompletionSource<PutItemResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task<RawLayout> resultTask = _repository.SaveAsync(layout, CancellationToken.None);
        resultTask.Should().NotBeComplete(because: "PutItemAsync has not completed yet");

        tcs.SetResult(new PutItemResponse());

        // Assert
        resultTask.Should().BeComplete();
        resultTask.Result.Should().Be(layout);

        _mockDynamoDb.Verify(db => db.PutItemAsync(
            It.Is<PutItemRequest>(req =>
                req.TableName == TestTableName &&
                req.Item.ContainsKey("UserId") &&
                req.Item.ContainsKey("Id") &&
                req.Item.ContainsKey("Name") &&
                req.Item.ContainsKey("Elements")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public void SaveAsync_WithValidationResult_StoresValidationResult()
    {
        // Arrange
        ValidationResult validationResult = new(false, new List<ValidationIssue>
        {
            new("ERR001", "Test error", "/elements/0")
        });
        RawLayout layout = CreateTestLayout() with { ValidationResult = validationResult };

        TaskCompletionSource<PutItemResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task<RawLayout> resultTask = _repository.SaveAsync(layout, CancellationToken.None);
        resultTask.Should().NotBeComplete(because: "PutItemAsync has not completed yet");

        tcs.SetResult(new PutItemResponse());

        // Assert
        resultTask.Should().BeComplete();
        _mockDynamoDb.Verify(db => db.PutItemAsync(
            It.Is<PutItemRequest>(req => req.Item.ContainsKey("ValidationResult")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public void SaveAsync_WhenPutAsyncIsPending_TaskIsNotComplete()
    {
        // Arrange
        RawLayout layout = CreateTestLayout();
        TaskCompletionSource<PutItemResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task<RawLayout> resultTask = _repository.SaveAsync(layout, CancellationToken.None);

        // Assert
        resultTask.Should().NotBeComplete(because: "PutItemAsync has not completed yet");

        tcs.SetResult(new PutItemResponse());

        resultTask.Should().BeComplete(because: "PutItemAsync has now completed");
    }

    [TestMethod]
    public void SaveAsync_WhenCancelled_TaskIsCancelled()
    {
        // Arrange
        RawLayout layout = CreateTestLayout();
        CancellationTokenSource cts = new();
        TaskCompletionSource<PutItemResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task<RawLayout> resultTask = _repository.SaveAsync(layout, cts.Token);
        resultTask.Should().NotBeComplete(because: "PutItemAsync has not completed yet");

        cts.Cancel();

        // Assert
        resultTask.Should().BeCanceled(because: "cancellation was requested");
    }

    [TestMethod]
    public void SaveAsync_WhenPutThrowsException_TaskIsFaulted()
    {
        // Arrange
        RawLayout layout = CreateTestLayout();
        AmazonDynamoDBException expectedException = new("DynamoDB put failed");
        TaskCompletionSource<PutItemResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task<RawLayout> resultTask = _repository.SaveAsync(layout, CancellationToken.None);
        resultTask.Should().NotBeComplete(because: "PutItemAsync has not completed yet");

        tcs.SetException(expectedException);

        // Assert
        resultTask.Should().BeFaultedWith(expectedException);
    }

    // ─── DeleteAsync ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void DeleteAsync_WithExistingLayout_DeletesLayout()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        Guid decoyId = Guid.NewGuid();

        // Response includes a decoy item with a different ID to prove the correct item is deleted
        TaskCompletionSource<ScanResponse> scanTcs = new();
        TaskCompletionSource<DeleteItemResponse> deleteTcs = new();

        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(scanTcs.Task)
            .Verifiable(Times.Once);
        _mockDynamoDb
            .Setup(db => db.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(deleteTcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task resultTask = _repository.DeleteAsync(testId, CancellationToken.None);
        resultTask.Should().NotBeComplete(because: "ScanAsync has not completed yet");

        scanTcs.SetResult(new ScanResponse
        {
            Items =
            [
                CreateDynamoDbItem(decoyId, OtherUserId, "Decoy Layout", 1),
                CreateDynamoDbItem(testId, TestUserId, "Test Layout", 1)
            ]
        });

        resultTask.Should().NotBeComplete(because: "DeleteItemAsync has not completed yet");

        deleteTcs.SetResult(new DeleteItemResponse());

        // Assert
        resultTask.Should().BeComplete();

        _mockDynamoDb.Verify(db => db.DeleteItemAsync(
            It.Is<DeleteItemRequest>(req =>
                req.TableName == TestTableName &&
                req.Key.ContainsKey("UserId") &&
                req.Key["UserId"].S == TestUserId &&
                req.Key.ContainsKey("Id") &&
                req.Key["Id"].S == testId.ToString()),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public void DeleteAsync_WithNonExistentLayout_DoesNotCallDeleteItem()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        TaskCompletionSource<ScanResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);
        // DeleteItemAsync should NOT be called (the default Times.Never setup handles this)

        // Act
        Task resultTask = _repository.DeleteAsync(testId, CancellationToken.None);
        resultTask.Should().NotBeComplete(because: "ScanAsync has not completed yet");

        tcs.SetResult(new ScanResponse { Items = [] });

        // Assert
        resultTask.Should().BeComplete();
    }

    [TestMethod]
    public void DeleteAsync_WhenScanAsyncIsPending_TaskIsNotComplete()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        TaskCompletionSource<ScanResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task resultTask = _repository.DeleteAsync(testId, CancellationToken.None);

        // Assert
        resultTask.Should().NotBeComplete(because: "ScanAsync has not completed yet");

        tcs.SetResult(new ScanResponse { Items = [] });

        resultTask.Should().BeComplete(because: "ScanAsync has now completed");
    }

    [TestMethod]
    public void DeleteAsync_WhenCancelledDuringScan_TaskIsCancelled()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        CancellationTokenSource cts = new();
        TaskCompletionSource<ScanResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task resultTask = _repository.DeleteAsync(testId, cts.Token);
        resultTask.Should().NotBeComplete(because: "ScanAsync has not completed yet");

        cts.Cancel();

        // Assert
        resultTask.Should().BeCanceled(because: "cancellation was requested during scan");
    }

    [TestMethod]
    public void DeleteAsync_WhenScanThrowsException_TaskIsFaulted()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        AmazonDynamoDBException expectedException = new("DynamoDB scan failed");
        TaskCompletionSource<ScanResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task resultTask = _repository.DeleteAsync(testId, CancellationToken.None);
        resultTask.Should().NotBeComplete(because: "ScanAsync has not completed yet");

        tcs.SetException(expectedException);

        // Assert
        resultTask.Should().BeFaultedWith(expectedException);
    }

    [TestMethod]
    public void DeleteAsync_WhenDeleteItemThrowsException_TaskIsFaulted()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        AmazonDynamoDBException expectedException = new("DynamoDB delete failed");
        TaskCompletionSource<ScanResponse> scanTcs = new();
        TaskCompletionSource<DeleteItemResponse> deleteTcs = new();

        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(scanTcs.Task)
            .Verifiable(Times.Once);
        _mockDynamoDb
            .Setup(db => db.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(deleteTcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task resultTask = _repository.DeleteAsync(testId, CancellationToken.None);
        resultTask.Should().NotBeComplete(because: "ScanAsync has not completed yet");

        scanTcs.SetResult(new ScanResponse { Items = [CreateDynamoDbItem(testId, TestUserId, "Test", 1)] });

        resultTask.Should().NotBeComplete(because: "DeleteItemAsync has not completed yet");

        deleteTcs.SetException(expectedException);

        // Assert
        resultTask.Should().BeFaultedWith(expectedException);
    }

    // ─── UpdateValidationResultAsync ─────────────────────────────────────────────

    [TestMethod]
    public void UpdateValidationResultAsync_WithExistingLayout_UpdatesValidationResult()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        ValidationResult validationResult = new(false, new List<ValidationIssue>
        {
            new("ERR001", "Validation failed", "/elements/0/label")
        });

        TaskCompletionSource<ScanResponse> scanTcs = new();
        TaskCompletionSource<UpdateItemResponse> updateTcs = new();

        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(scanTcs.Task)
            .Verifiable(Times.Once);
        _mockDynamoDb
            .Setup(db => db.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(updateTcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task resultTask = _repository.UpdateValidationResultAsync(testId, validationResult, CancellationToken.None);
        resultTask.Should().NotBeComplete(because: "ScanAsync has not completed yet");

        scanTcs.SetResult(new ScanResponse { Items = [CreateDynamoDbItem(testId, TestUserId, "Test Layout", 1)] });

        resultTask.Should().NotBeComplete(because: "UpdateItemAsync has not completed yet");

        updateTcs.SetResult(new UpdateItemResponse());

        // Assert
        resultTask.Should().BeComplete();

        _mockDynamoDb.Verify(db => db.UpdateItemAsync(
            It.Is<UpdateItemRequest>(req =>
                req.TableName == TestTableName &&
                req.UpdateExpression == "SET ValidationResult = :validationResult"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public void UpdateValidationResultAsync_WithNonExistentLayout_ThrowsInvalidOperationException()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        ValidationResult validationResult = new(false, new List<ValidationIssue>());
        TaskCompletionSource<ScanResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task resultTask = _repository.UpdateValidationResultAsync(testId, validationResult, CancellationToken.None);
        resultTask.Should().NotBeComplete(because: "ScanAsync has not completed yet");

        tcs.SetResult(new ScanResponse { Items = [] });

        // Assert
        resultTask.Should().BeFaultedWith(
            new InvalidOperationException($"Cannot update validation result: raw layout {testId} not found"));
    }

    [TestMethod]
    public void UpdateValidationResultAsync_WhenScanAsyncIsPending_TaskIsNotComplete()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        ValidationResult validationResult = new(false, new List<ValidationIssue>());
        TaskCompletionSource<ScanResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task resultTask = _repository.UpdateValidationResultAsync(testId, validationResult, CancellationToken.None);

        // Assert
        resultTask.Should().NotBeComplete(because: "ScanAsync has not completed yet");

        tcs.SetResult(new ScanResponse { Items = [] });

        // completes (faulted because layout not found, but still complete)
        resultTask.Should().BeFaultedWith(
            new InvalidOperationException($"Cannot update validation result: raw layout {testId} not found"));
    }

    [TestMethod]
    public void UpdateValidationResultAsync_WhenCancelledDuringScan_TaskIsCancelled()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        ValidationResult validationResult = new(false, new List<ValidationIssue>());
        CancellationTokenSource cts = new();
        TaskCompletionSource<ScanResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task resultTask = _repository.UpdateValidationResultAsync(testId, validationResult, cts.Token);
        resultTask.Should().NotBeComplete(because: "ScanAsync has not completed yet");

        cts.Cancel();

        // Assert
        resultTask.Should().BeCanceled(because: "cancellation was requested during scan");
    }

    [TestMethod]
    public void UpdateValidationResultAsync_WhenScanThrowsException_TaskIsFaulted()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        ValidationResult validationResult = new(false, new List<ValidationIssue>());
        AmazonDynamoDBException expectedException = new("DynamoDB scan failed");
        TaskCompletionSource<ScanResponse> tcs = new();
        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(tcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task resultTask = _repository.UpdateValidationResultAsync(testId, validationResult, CancellationToken.None);
        resultTask.Should().NotBeComplete(because: "ScanAsync has not completed yet");

        tcs.SetException(expectedException);

        // Assert
        resultTask.Should().BeFaultedWith(expectedException);
    }

    [TestMethod]
    public void UpdateValidationResultAsync_WhenUpdateItemThrowsException_TaskIsFaulted()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        ValidationResult validationResult = new(false, new List<ValidationIssue>());
        AmazonDynamoDBException expectedException = new("DynamoDB update failed");
        TaskCompletionSource<ScanResponse> scanTcs = new();
        TaskCompletionSource<UpdateItemResponse> updateTcs = new();

        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(scanTcs.Task)
            .Verifiable(Times.Once);
        _mockDynamoDb
            .Setup(db => db.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .WithStandardTaskBehavior(updateTcs.Task)
            .Verifiable(Times.Once);

        // Act
        Task resultTask = _repository.UpdateValidationResultAsync(testId, validationResult, CancellationToken.None);
        resultTask.Should().NotBeComplete(because: "ScanAsync has not completed yet");

        scanTcs.SetResult(new ScanResponse { Items = [CreateDynamoDbItem(testId, TestUserId, "Test", 1)] });

        resultTask.Should().NotBeComplete(because: "UpdateItemAsync has not completed yet");

        updateTcs.SetException(expectedException);

        // Assert
        resultTask.Should().BeFaultedWith(expectedException);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────────

    private static Dictionary<string, AttributeValue> CreateDynamoDbItem(Guid id, string userId, string name, int version)
    {
        return new Dictionary<string, AttributeValue>
        {
            { "Id", new AttributeValue { S = id.ToString() } },
            { "UserId", new AttributeValue { S = userId } },
            { "Name", new AttributeValue { S = name } },
            { "Elements", new AttributeValue { S = "[]" } },
            { "Version", new AttributeValue { N = version.ToString() } },
            { "CreatedAt", new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") } },
            { "UpdatedAt", new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") } }
        };
    }

    private static RawLayout CreateTestLayout()
    {
        return new RawLayout(
            Id: Guid.NewGuid(),
            UserId: TestUserId,
            Name: "Test Layout",
            Elements: Array.Empty<RawLayoutElementDto>(),
            Version: 1,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            ValidationResult: null
        );
    }
}
