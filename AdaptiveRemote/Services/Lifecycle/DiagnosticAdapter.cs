using System.Diagnostics;

namespace AdaptiveRemote.Services.Lifecycle;

internal class DiagnosticAdapter : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    private readonly ILifecycleViewController _controller;
    private ILifecycleActivity? _keyVaultActivity = default;

    public DiagnosticAdapter(ILifecycleViewController controller)
    {
        _controller = controller;
        DiagnosticListener.AllListeners.Subscribe(this);
    }

    public void OnCompleted() { }
    public void OnError(Exception error)
    {
        _controller.SetFatalError(error);
    }

    public void OnNext(DiagnosticListener value)
    {
        value.Subscribe(this);
        Console.WriteLine("Listening to {0}", value.Name);
    }

    public void OnNext(KeyValuePair<string, object?> value)
    {
        Console.WriteLine($"{value.Key}: {value.Value?.GetType().FullName}");

        switch (value.Key)
        {
            case "HostBuilding":
                _controller.SetPhase(LifecyclePhase.Building);
                break;
            case "HostBuilt":
                _controller.SetPhase(LifecyclePhase.Starting);
                break;

            case "SecretClient.GetPropertiesOfSecrets.Start":
                _keyVaultActivity ??= _controller.StartTask("Connecting to KeyVault");
                break;
            case "SecretClient.GetPropertiesOfSecrets.Stop":
                Interlocked.Exchange(ref _keyVaultActivity, null)?.Dispose();
                break;
            case "SecretClient.GetPropertiesOfSecrets.Exception":
                _keyVaultActivity?.SetFatalError((Exception)value.Value!);
                break;

        }
    }
}
