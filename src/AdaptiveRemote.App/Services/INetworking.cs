using System.Net;
using System.Net.NetworkInformation;

namespace AdaptiveRemote.Services;

internal interface INetworking
{
    Task<IPHostEntry> GetDnsEntryAsync(string hostNameOrAddress, CancellationToken cancellationToken);

    Task<IEnumerable<(IPAddress, IPAddress)>> GetOperationalNetworkInterfaceAddressesAsync(CancellationToken cancellationToken);

    Task<PingReply> SendPingAsync(IPAddress address, CancellationToken cancellationToken);
}
