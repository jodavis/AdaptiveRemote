using System.Net;
using System.Net.NetworkInformation;

namespace AdaptiveRemote.Services.Broadlink;

internal interface IDeviceConnectionFactory
{
    IDeviceConnection Create(
        IPEndPoint hostEndPoint,
        PhysicalAddress hostAddress,
        short deviceType);
}
