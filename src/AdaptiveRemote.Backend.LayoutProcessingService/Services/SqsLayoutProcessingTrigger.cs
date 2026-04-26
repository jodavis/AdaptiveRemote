using AdaptiveRemote.Backend.LayoutProcessingService.Configuration;
using AdaptiveRemote.Backend.LayoutProcessingService.Logging;
using AdaptiveRemote.Contracts;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Backend.LayoutProcessingService.Services;

/// <summary>
/// SQS-backed implementation of ILayoutProcessingTrigger.
/// Enqueues a message to the layout processing queue when a raw layout is saved.
/// Registered in RawLayoutService DI to replace the no-op stub from Task 4.
/// </summary>
public class SqsLayoutProcessingTrigger : ILayoutProcessingTrigger
{
    private readonly IAmazonSQS _sqsClient;
    private readonly SqsSettings _settings;
    private readonly ILogger<SqsLayoutProcessingTrigger> _logger;

    public SqsLayoutProcessingTrigger(
        IAmazonSQS sqsClient,
        IOptions<SqsSettings> settings,
        ILogger<SqsLayoutProcessingTrigger> logger)
    {
        _sqsClient = sqsClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task TriggerAsync(Guid rawLayoutId, CancellationToken ct)
    {
        try
        {
            SendMessageRequest request = new()
            {
                QueueUrl = _settings.QueueUrl,
                MessageBody = rawLayoutId.ToString()
            };

            await _sqsClient.SendMessageAsync(request, ct).ConfigureAwait(false);

            _logger.SqsTriggerEnqueued(rawLayoutId, _settings.QueueUrl);
        }
        catch (Exception ex)
        {
            _logger.ErrorEnqueuingSqsTrigger(rawLayoutId, ex);
            throw;
        }
    }
}
