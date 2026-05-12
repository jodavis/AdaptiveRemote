using System.Diagnostics;
using AdaptiveRemote.TestUtilities;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.EndToEndTests.TestServices.Backend;

/// <summary>
/// Manages a LocalStack Docker container for integration testing.
/// Provides a local DynamoDB instance that services can connect to.
/// </summary>
public class LocalStackFixture : IDisposable
{
    private Process? _dockerProcess;
    private bool _isStarted;
    private bool _ownsContainer; // Track if we created the container
    private readonly ILogger<LocalStackFixture> _logger;

    public LocalStackFixture(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<LocalStackFixture>();
    }

    public string ServiceUrl { get; } = "http://localhost:4566";

    public string Region { get; } = "us-east-1";

    /// <summary>
    /// Starts LocalStack in a Docker container and waits for it to be ready.
    /// </summary>
    public void Start()
    {
        if (_isStarted)
        {
            return;
        }

        // Check if LocalStack container is already running
        Process checkProcess = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "ps --filter name=localstack-test --format {{.Names}}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        checkProcess.Start();
        string existingContainer = WaitHelpers.WaitForAsyncTask(checkProcess.StandardOutput.ReadToEndAsync);
        WaitHelpers.WaitForAsyncTask(checkProcess.WaitForExitAsync);

        if (!string.IsNullOrWhiteSpace(existingContainer))
        {
            _logger.LogInformation("Found an existing localstack-test container. Verifying if it can be reused...");

            // Container already running — verify that SQS is enabled before reusing it.
            // An older container may have been started with SERVICES=dynamodb only.
            WaitForLocalStackReady();
            if (IsSqsEnabled())
            {
                _isStarted = true;
                _ownsContainer = false;
                return;
            }

            _logger.LogInformation("Found an existing localstack-test container, but SQS is not enabled. Stopping the stale container...");

            // SQS not available — stop the stale container so we can start a fresh one
            // with the correct SERVICES configuration.
            Process stopOldProcess = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "stop localstack-test",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            stopOldProcess.Start();
            WaitHelpers.WaitForAsyncTask(stopOldProcess.WaitForExitAsync);
            stopOldProcess.Dispose();
        }

        // Start LocalStack container
        _logger.LogInformation("Starting localstack-test container...");

        ProcessStartInfo startInfo = new()
        {
            FileName = "docker",
            Arguments = "run --rm -d " +
                       "--name localstack-test " +
                       "-p 4566:4566 " +
                       "-e SERVICES=dynamodb,sqs " +
                       "localstack/localstack:3.0",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _dockerProcess = new Process { StartInfo = startInfo };
        _dockerProcess.Start();

        string containerId = WaitHelpers.WaitForAsyncTask(_dockerProcess.StandardOutput.ReadToEndAsync);
        WaitHelpers.WaitForAsyncTask(_dockerProcess.WaitForExitAsync);

        if (_dockerProcess.ExitCode != 0)
        {
            string error = WaitHelpers.WaitForAsyncTask(_dockerProcess.StandardError.ReadToEndAsync);
            _logger.LogError("Failed to start localstack-test container. Exit code: {ExitCode}. Error: {Error}", _dockerProcess.ExitCode, error);
            throw new InvalidOperationException($"Failed to start LocalStack: {error}");
        }

        // Wait for LocalStack to be ready
        WaitForLocalStackReady();

        _isStarted = true;
        _ownsContainer = true; // We created this container

        _logger.LogInformation("LocalStack is ready and running in container {ContainerId}", containerId.Trim());
    }

    /// <summary>
    /// Creates a DynamoDB table in LocalStack for testing.
    /// </summary>
    public void CreateTable(string tableName)
    {
        if (!_isStarted)
        {
            throw new InvalidOperationException("LocalStack must be started before creating tables");
        }

        _logger.LogInformation("Creating DynamoDB table '{TableName}' in LocalStack...", tableName);

        // Use dummy credentials for LocalStack
        Amazon.Runtime.BasicAWSCredentials credentials = new("test", "test");

        AmazonDynamoDBConfig config = new()
        {
            ServiceURL = ServiceUrl,
            // Don't set RegionEndpoint when using ServiceURL - it overrides the custom endpoint
            AuthenticationRegion = Region
        };

        using AmazonDynamoDBClient client = new(credentials, config);

        // Check if table already exists
        try
        {
            WaitHelpers.WaitForAsyncTask(ct => client.DescribeTableAsync(tableName, ct), timeoutInSeconds: 10);
            // Table exists, no need to create
            return;
        }
        catch (AggregateException ex) when (ex.InnerException is Amazon.DynamoDBv2.Model.ResourceNotFoundException)
        {
            // Table doesn't exist, proceed to create
        }

        CreateTableRequest request = new()
        {
            TableName = tableName,
            KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement { AttributeName = "UserId", KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = "Id", KeyType = KeyType.RANGE }
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new AttributeDefinition { AttributeName = "UserId", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "Id", AttributeType = ScalarAttributeType.S }
            },
            BillingMode = BillingMode.PAY_PER_REQUEST
        };

        WaitHelpers.WaitForAsyncTask(ct => client.CreateTableAsync(request, ct), timeoutInSeconds: 10);

        _logger.LogInformation("CreateTable request for '{TableName}' sent. Waiting for table to become active...", tableName);

        // Wait for table to be active
        bool isActive = WaitHelpers.ExecuteWithRetries(() =>
        {
            try
            {
                DescribeTableResponse response = WaitHelpers.WaitForAsyncTask(ct => client.DescribeTableAsync(tableName, ct));
                return response.Table.TableStatus == TableStatus.ACTIVE;
            }
            catch (AggregateException ex) when (ex.InnerException is Amazon.DynamoDBv2.Model.ResourceNotFoundException
                                             || ex.InnerException is OperationCanceledException)
            {
                return false;
            }
        }, timeoutInSeconds: 15);

        if (!isActive)
        {
            _logger.LogError("Table {TableName} did not become active within the expected time.", tableName);
            throw new InvalidOperationException($"Table {tableName} did not become active within 15 seconds");
        }

        _logger.LogInformation("DynamoDB table '{TableName}' is created and active.", tableName);
    }

    /// <summary>
    /// Creates an SQS queue in LocalStack for testing. Idempotent: returns existing queue URL if already present.
    /// </summary>
    public string CreateSqsQueue(string queueName)
    {
        if (!_isStarted)
        {
            throw new InvalidOperationException("LocalStack must be started before creating queues");
        }

        Amazon.Runtime.BasicAWSCredentials credentials = new("test", "test");

        AmazonSQSConfig config = new()
        {
            ServiceURL = ServiceUrl,
            AuthenticationRegion = Region
        };

        using AmazonSQSClient client = new(credentials, config);

        try
        {
            _logger.LogInformation("Checking if SQS queue '{QueueName}' already exists in LocalStack...", queueName);
            GetQueueUrlResponse existingQueue = WaitHelpers.WaitForAsyncTask(ct => client.GetQueueUrlAsync(queueName, ct));

            _logger.LogInformation("SQS queue '{QueueName}' already exists with URL: {QueueUrl}", queueName, existingQueue.QueueUrl);
            return existingQueue.QueueUrl;
        }
        catch (AggregateException ex) when (ex.InnerException is QueueDoesNotExistException)
        {
            // Queue doesn't exist, proceed to create
        }

        _logger.LogInformation("Creating SQS queue '{QueueName}' in LocalStack...", queueName);

        CreateQueueResponse response = WaitHelpers.WaitForAsyncTask(ct => client.CreateQueueAsync(new CreateQueueRequest
        {
            QueueName = queueName
        }, ct), timeoutInSeconds: 15);

        _logger.LogInformation("SQS queue '{QueueName}' created with URL: {QueueUrl}", queueName, response.QueueUrl);

        return response.QueueUrl;
    }

    /// <summary>
    /// Returns the SQS queue URL for the given queue name in LocalStack format.
    /// </summary>
    public string GetSqsQueueUrl(string queueName)
        => $"http://sqs.{Region}.localhost.localstack.cloud:4566/000000000000/{queueName}";

    /// <summary>
    /// Returns true if SQS is enabled in the running LocalStack instance.
    /// </summary>
    private bool IsSqsEnabled()
    {
        try
        {
            using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(5) };
            HttpResponseMessage response = WaitHelpers.WaitForAsyncTask(ct => client.GetAsync($"{ServiceUrl}/_localstack/health", ct));
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            string body = WaitHelpers.WaitForAsyncTask(response.Content.ReadAsStringAsync);
            using System.Text.Json.JsonDocument json = System.Text.Json.JsonDocument.Parse(body);

            // Top-level "status": "running" means all services are implicitly available
            if (json.RootElement.TryGetProperty("status", out System.Text.Json.JsonElement statusEl))
            {
                string status = statusEl.GetString() ?? string.Empty;
                if (string.Equals(status, "running", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (json.RootElement.TryGetProperty("services", out System.Text.Json.JsonElement servicesEl))
            {
                if (servicesEl.TryGetProperty("sqs", out System.Text.Json.JsonElement sqsEl))
                {
                    string sqsStatus = sqsEl.GetString() ?? string.Empty;
                    return string.Equals(sqsStatus, "available", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(sqsStatus, "running", StringComparison.OrdinalIgnoreCase);
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private void WaitForLocalStackReady()
    {
        // Poll LocalStack health endpoint
        using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(2) };

        bool isReady = WaitHelpers.ExecuteWithRetries(() =>
        {
            try
            {
                HttpResponseMessage response = WaitHelpers.WaitForAsyncTask(ct => client.GetAsync($"{ServiceUrl}/_localstack/health", ct));
                if (response.IsSuccessStatusCode)
                {
                    // Give it a bit more time to fully initialize DynamoDB
                    Thread.Sleep(2000);
                    return true;
                }
            }
            catch
            {
                // Ignore exceptions during startup
            }
            return false;
        }, timeoutInSeconds: 60);

        if (!isReady)
        {
            _logger.LogError("LocalStack did not become ready within the expected time.");
            throw new InvalidOperationException("LocalStack did not become ready within 60 seconds");
        }
    }

    public void Dispose()
    {
        // Only stop the container if we created it
        if (_ownsContainer && _dockerProcess != null)
        {
            _logger.LogInformation("Stopping localstack-test container...");

            // Stop and remove the container
            Process stopProcess = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "stop localstack-test",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            stopProcess.Start();
            stopProcess.WaitForExit(10000);
            stopProcess.Dispose();

            _dockerProcess.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
