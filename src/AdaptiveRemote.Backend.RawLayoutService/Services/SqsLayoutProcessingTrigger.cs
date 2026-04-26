using AdaptiveRemote.Contracts;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AdaptiveRemote.Backend.RawLayoutService.Configuration;
using AdaptiveRemote.Backend.RawLayoutService.Logging;

namespace AdaptiveRemote.Backend.RawLayoutService.Services;

/// <summary>
/// SQS-backed implementation of ILayoutProcessingTrigger.
/// Enqueues a message to the layout processing SQS queue when a raw layout is saved.
/// Replaces StubLayoutProcessingTrigger from Task 4.
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
