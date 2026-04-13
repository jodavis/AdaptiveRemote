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
    }

    [TestCleanup]
    public void Cleanup()
    {
        _mockDynamoDb.VerifyAll();
    }

    [TestMethod]
    public async Task GetAsync_WithValidId_ReturnsLayout()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        Guid nonMatchingId = Guid.NewGuid();
        // Mock should return only matching items (FilterExpression is applied by DynamoDB, not in our code)
        ScanResponse scanResponse = new()
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                CreateDynamoDbItem(testId, TestUserId, "Test Layout", 1)
            }
        };

        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scanResponse);

        // Act
        RawLayout? result = await _repository.GetAsync(testId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(testId);
        result.UserId.Should().Be(TestUserId);
        result.Name.Should().Be("Test Layout");
        result.Version.Should().Be(1);

        // Verify correct scan filter is sent to DynamoDB
        _mockDynamoDb.Verify(db => db.ScanAsync(
            It.Is<ScanRequest>(req =>
                req.FilterExpression == "Id = :id" &&
                req.ExpressionAttributeValues.ContainsKey(":id") &&
                req.ExpressionAttributeValues[":id"].S == testId.ToString()),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        ScanResponse scanResponse = new()
        {
            Items = new List<Dictionary<string, AttributeValue>>()
        };

        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scanResponse);

        // Act
        RawLayout? result = await _repository.GetAsync(testId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public async Task ListByUserAsync_ReturnsAllLayoutsForUser()
    {
        // Arrange
        Guid id1 = Guid.NewGuid();
        Guid id2 = Guid.NewGuid();
        QueryResponse queryResponse = new()
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                CreateDynamoDbItem(id1, TestUserId, "Layout 1", 1),
                CreateDynamoDbItem(id2, TestUserId, "Layout 2", 2)
            }
        };

        _mockDynamoDb
            .Setup(db => db.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResponse);

        // Act
        IReadOnlyList<RawLayout> results = await _repository.ListByUserAsync(TestUserId, CancellationToken.None);

        // Assert
        results.Should().HaveCount(2);
        results[0].Id.Should().Be(id1);
        results[1].Id.Should().Be(id2);
        results.All(r => r.UserId == TestUserId).Should().BeTrue();

        // Verify correct query parameters are sent to DynamoDB
        _mockDynamoDb.Verify(db => db.QueryAsync(
            It.Is<QueryRequest>(req =>
                req.KeyConditionExpression == "UserId = :userId" &&
                req.ExpressionAttributeValues.ContainsKey(":userId") &&
                req.ExpressionAttributeValues[":userId"].S == TestUserId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ListByUserAsync_WithNoLayouts_ReturnsEmptyList()
    {
        // Arrange
        QueryResponse queryResponse = new()
        {
            Items = new List<Dictionary<string, AttributeValue>>()
        };

        _mockDynamoDb
            .Setup(db => db.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResponse);

        // Act
        IReadOnlyList<RawLayout> results = await _repository.ListByUserAsync(TestUserId, CancellationToken.None);

        // Assert
        results.Should().BeEmpty();
    }

    [TestMethod]
    public async Task SaveAsync_CreatesNewLayout()
    {
        // Arrange
        RawLayout layout = CreateTestLayout();
        PutItemResponse putResponse = new();

        _mockDynamoDb
            .Setup(db => db.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(putResponse);

        // Act
        RawLayout result = await _repository.SaveAsync(layout, CancellationToken.None);

        // Assert
        result.Should().Be(layout);
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
    public async Task SaveAsync_WithValidationResult_StoresValidationResult()
    {
        // Arrange
        ValidationResult validationResult = new(false, new List<ValidationIssue>
        {
            new("ERR001", "Test error", "/elements/0")
        });

        RawLayout layout = CreateTestLayout() with { ValidationResult = validationResult };
        PutItemResponse putResponse = new();

        _mockDynamoDb
            .Setup(db => db.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(putResponse);

        // Act
        await _repository.SaveAsync(layout, CancellationToken.None);

        // Assert
        _mockDynamoDb.Verify(db => db.PutItemAsync(
            It.Is<PutItemRequest>(req =>
                req.Item.ContainsKey("ValidationResult")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task DeleteAsync_WithExistingLayout_DeletesLayout()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        ScanResponse scanResponse = new()
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                CreateDynamoDbItem(testId, TestUserId, "Test Layout", 1)
            }
        };

        DeleteItemResponse deleteResponse = new();

        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scanResponse);

        _mockDynamoDb
            .Setup(db => db.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(deleteResponse);

        // Act
        await _repository.DeleteAsync(testId, CancellationToken.None);

        // Assert
        _mockDynamoDb.Verify(db => db.DeleteItemAsync(
            It.Is<DeleteItemRequest>(req =>
                req.TableName == TestTableName &&
                req.Key.ContainsKey("UserId") &&
                req.Key.ContainsKey("Id")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task DeleteAsync_WithNonExistentLayout_DoesNothing()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        ScanResponse scanResponse = new()
        {
            Items = new List<Dictionary<string, AttributeValue>>()
        };

        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scanResponse);

        // Act
        await _repository.DeleteAsync(testId, CancellationToken.None);

        // Assert - DeleteItemAsync should not be called
        _mockDynamoDb.Verify(db => db.DeleteItemAsync(
            It.IsAny<DeleteItemRequest>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task UpdateValidationResultAsync_WithExistingLayout_UpdatesValidationResult()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        ValidationResult validationResult = new(false, new List<ValidationIssue>
        {
            new("ERR001", "Validation failed", "/elements/0/label")
        });

        ScanResponse scanResponse = new()
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                CreateDynamoDbItem(testId, TestUserId, "Test Layout", 1)
            }
        };

        UpdateItemResponse updateResponse = new();

        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scanResponse);

        _mockDynamoDb
            .Setup(db => db.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updateResponse);

        // Act
        await _repository.UpdateValidationResultAsync(testId, validationResult, CancellationToken.None);

        // Assert
        _mockDynamoDb.Verify(db => db.UpdateItemAsync(
            It.Is<UpdateItemRequest>(req =>
                req.TableName == TestTableName &&
                req.UpdateExpression == "SET ValidationResult = :validationResult"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task UpdateValidationResultAsync_WithNonExistentLayout_ThrowsException()
    {
        // Arrange
        Guid testId = Guid.NewGuid();
        ValidationResult validationResult = new(false, new List<ValidationIssue>());

        ScanResponse scanResponse = new()
        {
            Items = new List<Dictionary<string, AttributeValue>>()
        };

        _mockDynamoDb
            .Setup(db => db.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scanResponse);

        // Act & Assert
        Func<Task> act = async () => await _repository.UpdateValidationResultAsync(testId, validationResult, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Cannot update validation result: raw layout {testId} not found");
    }

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
