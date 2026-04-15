using System.Text.Json;
using AdaptiveRemote.Backend.RawLayoutService.Configuration;
using AdaptiveRemote.Contracts;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Backend.RawLayoutService.Repositories;

/// <summary>
/// DynamoDB-backed repository for raw layouts.
/// Implements both IRawLayoutRepository (CRUD) and IRawLayoutStatusWriter (validation result updates).
/// Partition key: UserId; Sort key: Id (Guid as string).
/// </summary>
public class DynamoDbRawLayoutRepository : IRawLayoutRepository, IRawLayoutStatusWriter
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;

    public DynamoDbRawLayoutRepository(IAmazonDynamoDB dynamoDb, IOptions<DynamoDbSettings> settings)
    {
        _dynamoDb = dynamoDb;
        _tableName = settings.Value.TableName;
    }

    public async Task<RawLayout?> GetAsync(Guid id, CancellationToken ct)
    {
        // We need to query by Id across all users. In production, this would require a GSI.
        // For MVP, we'll scan (inefficient but acceptable for low volume).
        ScanRequest scanRequest = new()
        {
            TableName = _tableName,
            FilterExpression = "Id = :id",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":id", new AttributeValue { S = id.ToString() } }
            }
        };

        ScanResponse response = await _dynamoDb.ScanAsync(scanRequest, ct);

        if (response.Items.Count == 0)
        {
            return null;
        }

        return response.Items
            .Select(DeserializeRawLayout)
            .FirstOrDefault(l => l.Id == id);
    }

    public async Task<IReadOnlyList<RawLayout>> ListByUserAsync(string userId, CancellationToken ct)
    {
        QueryRequest queryRequest = new()
        {
            TableName = _tableName,
            KeyConditionExpression = "UserId = :userId",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":userId", new AttributeValue { S = userId } }
            }
        };

        QueryResponse response = await _dynamoDb.QueryAsync(queryRequest, ct);

        return response.Items
            .Select(DeserializeRawLayout)
            .Where(l => l.UserId == userId)
            .ToList();
    }

    public async Task<RawLayout> SaveAsync(RawLayout layout, CancellationToken ct)
    {
        Dictionary<string, AttributeValue> item = new()
        {
            { "UserId", new AttributeValue { S = layout.UserId } },
            { "Id", new AttributeValue { S = layout.Id.ToString() } },
            { "Name", new AttributeValue { S = layout.Name } },
            { "Elements", new AttributeValue { S = JsonSerializer.Serialize(layout.Elements, LayoutContractsJsonContext.Default.IReadOnlyListRawLayoutElementDto) } },
            { "Version", new AttributeValue { N = layout.Version.ToString() } },
            { "CreatedAt", new AttributeValue { S = layout.CreatedAt.ToString("O") } },
            { "UpdatedAt", new AttributeValue { S = layout.UpdatedAt.ToString("O") } }
        };

        if (layout.ValidationResult != null)
        {
            item["ValidationResult"] = new AttributeValue
            {
                S = JsonSerializer.Serialize(layout.ValidationResult, LayoutContractsJsonContext.Default.ValidationResult)
            };
        }

        PutItemRequest putRequest = new()
        {
            TableName = _tableName,
            Item = item
        };

        await _dynamoDb.PutItemAsync(putRequest, ct);

        return layout;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        // First, get the item to find the UserId (partition key)
        RawLayout? existingLayout = await GetAsync(id, ct);
        if (existingLayout == null)
        {
            return; // Already deleted or doesn't exist
        }

        DeleteItemRequest deleteRequest = new()
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "UserId", new AttributeValue { S = existingLayout.UserId } },
                { "Id", new AttributeValue { S = id.ToString() } }
            }
        };

        await _dynamoDb.DeleteItemAsync(deleteRequest, ct);
    }

    public async Task UpdateValidationResultAsync(Guid rawLayoutId, ValidationResult result, CancellationToken ct)
    {
        // First, get the item to find the UserId (partition key)
        RawLayout existingLayout = await GetAsync(rawLayoutId, ct)
            ?? throw new InvalidOperationException($"Cannot update validation result: raw layout {rawLayoutId} not found");

        UpdateItemRequest updateRequest = new()
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "UserId", new AttributeValue { S = existingLayout.UserId } },
                { "Id", new AttributeValue { S = rawLayoutId.ToString() } }
            },
            UpdateExpression = "SET ValidationResult = :validationResult",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":validationResult", new AttributeValue
                    {
                        S = JsonSerializer.Serialize(result, LayoutContractsJsonContext.Default.ValidationResult)
                    }
                }
            }
        };

        await _dynamoDb.UpdateItemAsync(updateRequest, ct);
    }

    private static RawLayout DeserializeRawLayout(Dictionary<string, AttributeValue> item)
    {
        Guid id = Guid.Parse(item["Id"].S);
        string userId = item["UserId"].S;
        string name = item["Name"].S;
        IReadOnlyList<RawLayoutElementDto> elements = JsonSerializer.Deserialize(item["Elements"].S, LayoutContractsJsonContext.Default.IReadOnlyListRawLayoutElementDto)
            ?? Array.Empty<RawLayoutElementDto>();
        int version = int.Parse(item["Version"].N);
        DateTimeOffset createdAt = DateTimeOffset.Parse(item["CreatedAt"].S);
        DateTimeOffset updatedAt = DateTimeOffset.Parse(item["UpdatedAt"].S);

        ValidationResult? validationResult = null;
        if (item.TryGetValue("ValidationResult", out AttributeValue? validationResultAttr))
        {
            validationResult = JsonSerializer.Deserialize(validationResultAttr.S, LayoutContractsJsonContext.Default.ValidationResult);
        }

        return new RawLayout(id, userId, name, elements, version, createdAt, updatedAt, validationResult);
    }
}
