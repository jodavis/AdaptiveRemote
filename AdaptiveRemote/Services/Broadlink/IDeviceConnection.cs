namespace AdaptiveRemote.Services.Broadlink;

internal interface IDeviceConnection
{
    Task<bool> AuthenticateAsync(CancellationToken cancellationToken);

    Task SendData(byte[] data, CancellationToken cancellationToken);
}
