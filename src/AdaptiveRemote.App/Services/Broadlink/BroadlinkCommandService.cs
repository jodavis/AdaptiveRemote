using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Broadlink;

internal sealed class BroadlinkCommandService : CommandServiceBase<IRCommand>
{
    private readonly IDeviceLocator _deviceLocator;
    private readonly IDeviceConnection.Factory _connectionFactory;
    private readonly IRemoteDefinitionService _definitionService;
    private readonly IPersistSettings _persistSettings;
    private readonly BroadlinkSettings _settings;

    private IDeviceConnection? _connection;
    private readonly Dictionary<string, byte[]> _activeData = new();

    public BroadlinkCommandService(
        IDeviceLocator deviceLocator,
        IDeviceConnection.Factory connectionFactory,
        IRemoteDefinitionService definitionService,
        IPersistSettings persistSettings,
        IOptions<BroadlinkSettings> settings,
        ILogger<BroadlinkCommandService> logger)
        : base("Broadlink IR Commands", definitionService, logger)
    {
        _deviceLocator = deviceLocator;
        _connectionFactory = connectionFactory;
        _definitionService = definitionService;
        _persistSettings = persistSettings;
        _settings = settings.Value;
    }

    public override async Task InitializeAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
    {
        activity.Description = Phrases.Startup_ConnectingToBroadlink;

        Logger.BroadlinkCommandService_SearchingForDevice();
        ScanResponsePacket found = await _deviceLocator.FindDeviceAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        _connection = _connectionFactory.Create(found.HostEndPoint, found.HostAddress, found.DeviceType);

        Logger.BroadlinkCommandService_Authenticating(found.HostEndPoint);
        await _connection.AuthenticateAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        Logger.BroadlinkCommandService_Authenticated(found.HostEndPoint);

        await LoadProgrammedDataAsync();

        await base.InitializeAsync(activity, cancellationToken);

        foreach (IRCommand command in _definitionService.GetCommands<IRCommand>())
        {
            command.ProgramAsync = CreateProgramHandler(command);
        }

        Logger.BroadlinkCommandService_Ready();
    }

    protected override Command.ExecuteDelegate CreateHandler(IRCommand command)
    {
        if (!_activeData.TryGetValue(command.Name, out byte[]? data))
        {
            data = Convert.FromBase64String(command.Data);
            _activeData[command.Name] = data;
        }

        return cancellationToken => _connection!.SendDataAsync(_activeData[command.Name], cancellationToken);
    }

    private async Task LoadProgrammedDataAsync()
    {
        foreach (IRCommand command in _definitionService.GetCommands<IRCommand>())
        {
            string settingKey = $"IRData:{command.Name}";
            string? base64Data = await _persistSettings.GetAsync(settingKey);
            if (base64Data is not null)
            {
                Logger.BroadlinkCommandService_LoadedProgrammedData(command.Name);
                _activeData[command.Name] = Convert.FromBase64String(base64Data);
            }
        }
    }

    private Command.ProgramDelegate CreateProgramHandler(IRCommand command)
    {
        return async (cancellationToken) =>
        {
            Logger.BroadlinkCommandService_EnteringLearningMode(command);
            await _connection!.EnterLearningModeAsync(cancellationToken);

            using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(_settings.LearningTimeout));
            using CancellationTokenSource combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            Logger.BroadlinkCommandService_WaitingForIRSignal(command);
            byte[]? data = null;
            while (data is null)
            {
                try
                {
                    data = await _connection.CheckLearnedDataAsync(combined.Token);
                    if (data is null)
                    {
                        await Task.Delay(100, combined.Token);
                    }
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    throw Errors.Broadlink_LearningTimeout();
                }
            }

            Logger.BroadlinkCommandService_LearnedData(command);

            string base64Data = Convert.ToBase64String(data);
            _persistSettings.Set($"IRData:{command.Name}", base64Data);
            _activeData[command.Name] = data;
        };
    }
}
