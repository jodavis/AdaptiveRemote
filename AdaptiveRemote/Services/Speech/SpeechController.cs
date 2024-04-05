using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Speech;

internal class SpeechController : ISpeechController, IDisposable
{
    private readonly ILogger<SpeechController> _logger;
    private readonly Listening _viewModel;

    public SpeechController(ILogger<SpeechController> logger, Listening viewModel)
    {
        _logger = logger;
        _viewModel = viewModel;
    }

    public void Start()
    {
        throw new NotImplementedException();
        //_logger.LogInformation("Started");
        //_viewModel.StatusMessage = "Say \"Hey Remote\" to start voice commands";
    }

    public void Dispose()
    {
        throw new NotImplementedException();
        //_logger.LogInformation("Stopped");
        //_viewModel.StatusMessage = String.Empty;
        //_viewModel.IsListening = false;
    }
}
