using System.Net;
using System.Net.Sockets;

namespace AdaptiveRemote.Services.Broadlink;

internal interface ISocketFactory
{
    ISocket CreateForBroadcast();

    ISocket Create();
}
