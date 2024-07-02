using System.Net;
using System.Net.Sockets;

namespace AdaptiveRemote.Services.Broadlink;

internal interface ISocketFactory
{
    ISocket Create(EndPoint targetEndPoint, SocketOptionName options);

    ISocket CreateForBroadcast();
}
