using AdaptiveRemote.Backend.LayoutProcessingService.Configuration;
using AdaptiveRemote.Backend.LayoutProcessingService.Logging;
using AdaptiveRemote.Contracts;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Backend.LayoutProcessingService.Services;

/// <summary>
/// Background hosted service that polls an SQS queue for layout compilation requests
/// and orchestrates the full pipeline: fetch → compile → validate → store or write-back.
///
/// Pipeline on success:
///   1. Dequeue SQS message containing rawLayoutId
///   2. Fetch RawLayout from IRawLayoutRepository
///   3. Compile via ILayoutCompilerClient
///   4. Validate via ILayoutValidationClient
///   5. Store compiled layout via ICompiledLayoutRepository
///   6. Notify via INotificationPublisher
///   7. Delete the SQS message
///
/// Pipeline on validation failure:
///   1–4 same as above
///   5. Write ValidationResult back to RawLayout via IRawLayoutStatusWriter
///   6. Do not store compiled layout; do not notify
///   7. Delete the SQS message (failure is recorded on the raw layout, not retried)
///
/// Pipeline on processing error:
///   - Do not delete the message; SQS visibility timeout expires and the message
///     becomes visible again for retry (max receive count = 3; then DLQ).
/// </summary>
public class LayoutProcessingOrchestrator : BackgroundService
{
    private readonly IAmazonSQS _sqsClient;
    private readonly SqsSettings _sqsSettings;
    private readonly IRawLayoutRepository _rawLayoutRepository;
    private readonly IRawLayoutStatusWriter _rawLayoutStatusWriter;
    private readonly ILayoutCompilerClient _compilerClient;
    private readonly ILayoutValidationClient _validationClient;
    private readonly ICompiledLayoutRepository _compiledLayoutRepository;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ILogger<LayoutProcessingOrchestrator> _logger;

    public LayoutProcessingOrchestrator(
        IAmazonSQS sqsClient,
        IOptions<SqsSettings> sqsSettings,
        IRawLayoutRepository rawLayoutRepository,
        IRawLayoutStatusWriter rawLayoutStatusWriter,
        ILayoutCompilerClient compilerClient,
        ILayoutValidationClient validationClient,
        ICompiledLayoutRepository compiledLayoutRepository,
        INotificationPublisher notificationPublisher,
        ILogger<LayoutProcessingOrchestrator> logger)
    {
        _sqsClient = sqsClient;
        _sqsSettings = sqsSettings.Value;
        _rawLayoutRepository = rawLayoutRepository;
        _rawLayoutStatusWriter = rawLayoutStatusWriter;
        _compilerClient = compilerClient;
        _validationClient = validationClient;
        _compiledLayoutRepository = compiledLayoutRepository;
        _notificationPublisher = notificationPublisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.SqsPollingStarted(_sqsSettings.QueueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ReceiveMessageRequest request = new()
                {
                    QueueUrl = _sqsSettings.QueueUrl,
                    MaxNumberOfMessages = _sqsSettings.MaxNumberOfMessages,
                    VisibilityTimeout = _sqsSettings.VisibilityTimeoutSeconds,
                    WaitTimeSeconds = _sqsSettings.WaitTimeSeconds,
                    MessageSystemAttributeNames = ["ApproximateReceiveCount"]
                };

                ReceiveMessageResponse response = await _sqsClient
                    .ReceiveMessageAsync(request, stoppingToken)
                    .ConfigureAwait(false);

                if (response.Messages.Count == 0)
                {
                    await Task.Delay(_sqsSettings.EmptyQueueDelayMs, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                foreach (Message message in response.Messages)
                {
                    await ProcessMessageAsync(message, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown; exit the loop.
                break;
            }
            catch (Exception ex)
            {
                _logger.SqsPollingError(ex);

                // Back off briefly before retrying, to avoid a tight error loop.
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
        }

        _logger.SqsPollingStopped();
    }

    private async Task ProcessMessageAsync(Message message, CancellationToken ct)
    {
        string receiptHandle = message.ReceiptHandle;

        if (!Guid.TryParse(message.Body, out Guid rawLayoutId))
        {
            _logger.SqsUnrecognizedMessageWarning(receiptHandle, new FormatException($"Message body is not a valid GUID: '{message.Body}'"));

            // Delete unrecognized messages to avoid them blocking the queue.
            await DeleteMessageAsync(receiptHandle, ct).ConfigureAwait(false);
            return;
        }

        _logger.SqsMessageReceived(rawLayoutId, receiptHandle);

        // Log retry warning after establishing message identity: if ApproximateReceiveCount > 1
        // the message has already been attempted; the next failure will route it to the DLQ.
        if (message.Attributes.TryGetValue("ApproximateReceiveCount", out string? receiveCountStr)
            && int.TryParse(receiveCountStr, out int receiveCount)
            && receiveCount > 1)
        {
            _logger.SqsMessageRetry(rawLayoutId, receiveCount);
        }

        try
        {
            // Step 1: Fetch raw layout
            RawLayout? rawLayout = await _rawLayoutRepository.GetAsync(rawLayoutId, ct).ConfigureAwait(false);
            if (rawLayout is null)
            {
                _logger.RawLayoutNotFound(rawLayoutId);

                // Delete the message — there is no layout to process.
                await DeleteMessageAsync(receiptHandle, ct).ConfigureAwait(false);
                return;
            }

            // Step 2: Compile
            CompiledLayout compiledLayout = await _compilerClient
                .CompileAsync(rawLayout, ct)
                .ConfigureAwait(false);

            _logger.LayoutCompiled(rawLayoutId);

            // Step 3: Validate
            ValidationResult validationResult = await _validationClient
                .ValidateAsync(compiledLayout, ct)
                .ConfigureAwait(false);

            if (!validationResult.IsValid)
            {
                _logger.LayoutValidationFailed(rawLayoutId, validationResult.Issues.Count);

                // Write validation failure back to the raw layout so the editor can display it.
                await _rawLayoutStatusWriter
                    .UpdateValidationResultAsync(rawLayoutId, validationResult, ct)
                    .ConfigureAwait(false);

                _logger.ValidationResultWrittenBack(rawLayoutId);

                // Delete the message — failure is recorded on the raw layout; retrying the
                // same layout without changes will produce the same result.
                await DeleteMessageAsync(receiptHandle, ct).ConfigureAwait(false);
                return;
            }

            _logger.LayoutValidationPassed(rawLayoutId);

            // Step 4: Store compiled layout
            CompiledLayout savedLayout = await _compiledLayoutRepository
                .SaveAsync(compiledLayout, ct)
                .ConfigureAwait(false);

            _logger.CompiledLayoutStored(rawLayoutId, savedLayout.Id);

            // Step 5: Notify clients
            await _notificationPublisher
                .PublishLayoutReadyAsync(rawLayout.UserId, savedLayout.Id, ct)
                .ConfigureAwait(false);

            _logger.LayoutReadyPublished(rawLayout.UserId, savedLayout.Id);

            _logger.SqsMessageProcessedSuccessfully(rawLayoutId);

            // Delete the message only on full success.
            await DeleteMessageAsync(receiptHandle, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Graceful shutdown — do not delete; let visibility timeout expire for retry.
            throw;
        }
        catch (Exception ex)
        {
            _logger.ErrorProcessingSqsMessage(rawLayoutId, receiptHandle, ex);

            // Do not delete the message. SQS will make it visible again after the
            // visibility timeout, up to the max receive count, then route it to the DLQ.
        }
    }

    private async Task DeleteMessageAsync(string receiptHandle, CancellationToken ct)
    {
        DeleteMessageRequest deleteRequest = new()
        {
            QueueUrl = _sqsSettings.QueueUrl,
            ReceiptHandle = receiptHandle
        };

        await _sqsClient.DeleteMessageAsync(deleteRequest, ct).ConfigureAwait(false);
    }
}
