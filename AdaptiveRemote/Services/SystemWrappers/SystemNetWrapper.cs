using System.Net;
using System.Net.NetworkInformation;

namespace AdaptiveRemote.Services.SystemWrappers;

internal class SystemNetWrapper : INetworking
{
    public Task<PingReply> SendPingAsync(IPAddress address, CancellationToken cancellationToken)
        => new Ping().SendPingAsync(address, TimeSpan.FromSeconds(2), cancellationToken: cancellationToken);

    public async Task<IPHostEntry> GetDnsEntryAsync(string hostNameOrAddress, CancellationToken cancellationToken)
        => await Dns.GetHostEntryAsync(hostNameOrAddress, cancellationToken);

    public Task<IEnumerable<(IPAddress, IPAddress)>> GetOperationalNetworkInterfaceAddresses(CancellationToken cancellationToken)
        => Task.FromResult<IEnumerable<(IPAddress, IPAddress)>>(
            NetworkInterface.GetAllNetworkInterfaces()
            .Where(x => x.OperationalStatus == OperationalStatus.Up)
            .Where(x => x.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(x => x.GetIPProperties().UnicastAddresses)
            .Where(x => x.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            .Select(x => (x.Address, x.IPv4Mask)));
}
