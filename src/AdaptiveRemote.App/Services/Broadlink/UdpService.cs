using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Channels;
using AdaptiveRemote.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Broadlink;

internal class UdpService : IUdpService
{
    private const int MinimumResponseSize = 0x30;

    private readonly ISocket.Factory _socketFactory;
    private readonly BroadlinkSettings _settings;
    private readonly MessageLogger _logger;

    public UdpService(ISocket.Factory socketFactory, IOptions<BroadlinkSettings> settings, ILogger<UdpService> logger)
    {
        _socketFactory = socketFactory;
        _settings = settings.Value;
        _logger = new(logger);
    }

    IAsyncEnumerable<ScanResponsePacket> IUdpService.BroadcastAsync(ScanRequestPacket packet, CancellationToken cancellationToken)
    {
        Channel<ScanResponsePacket> responseChannel = Channel.CreateUnbounded<ScanResponsePacket>(new()
        {
            AllowSynchronousContinuations = true,
            SingleReader = true,
            SingleWriter = true,
        });

        _ = Task.Run(async () =>
        {
            // Use configured discovery endpoint or default to broadcast on port 80
            IPAddress discoveryIp = _settings.DiscoveryAddress != null
                ? IPAddress.Parse(_settings.DiscoveryAddress)
                : IPAddress.Broadcast;
            int discoveryPort = _settings.DiscoveryPort ?? 80;
            IPEndPoint discoverEndPoint = new(discoveryIp, discoveryPort);

            DateTime startTime = DateTime.Now;
            TimeSpan timeout = TimeSpan.FromSeconds(_settings.ScanTimeout);
            HashSet<(EndPoint, PhysicalAddress, int)> discovered = new();

            try
            {
                using ISocket socket = _socketFactory.CreateForBroadcast();

                // Populate LocalPort and LocalIPAddress in the scan packet
                if (socket.LocalEndPoint is IPEndPoint localEndPoint)
                {
                    packet.LocalPort = (short)localEndPoint.Port;
                    packet.LocalIPAddress = localEndPoint.Address.GetAddressBytes();
                }

                while (DateTime.Now - startTime < timeout)
                {
                    TimeSpan timeLeft = timeout - (DateTime.Now - startTime);
                    using CancellationTokenSource timeoutCancellation = new(timeLeft);
                    using CancellationTokenSource combinedCancel = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken, timeoutCancellation.Token);

                    _logger.UdpService_Sending(packet, packet.Size, discoverEndPoint);

                    await socket.SendToAsync(packet.GetBuffer(), discoverEndPoint, combinedCancel.Token);
                    cancellationToken.ThrowIfCancellationRequested();

                    _logger.UdpService_Sent(packet);

                    while (true)
                    {
                        Memory<byte> buffer = new byte[0x400];
                        SocketReceiveFromResult result;
                        try
                        {
                            result = await socket.ReceiveFromAsync(buffer, discoverEndPoint, combinedCancel.Token);
                            cancellationToken.ThrowIfCancellationRequested();
                            buffer = buffer.Slice(0, result.ReceivedBytes);

                            _logger.UdpService_ReceivedResponse(packet, result.ReceivedBytes, result.RemoteEndPoint);
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        ScanResponsePacket response = new((IPEndPoint)result.RemoteEndPoint, buffer);

                        // TODO: add comparison for ScanResponsePayload so it can be added directly
                        if (!discovered.Add((response.HostEndPoint, response.HostAddress, response.DeviceType)))
                        {
                            continue;
                        }

                        responseChannel.Writer.TryWrite(response);
                    }
                }

                responseChannel.Writer.TryComplete();
            }
            catch (OperationCanceledException error)
            {
                _logger.UdpService_Cancelled(packet);
                responseChannel.Writer.TryComplete(error);
            }
            catch (UdpException error) when (cancellationToken.IsCancellationRequested)
            {
                OperationCanceledException cancelled = new("UDP broadcast was cancelled.", error, cancellationToken);
                _logger.UdpService_Cancelled(packet);
                responseChannel.Writer.TryComplete(cancelled);
            }
            catch (Exception error) when (cancellationToken.IsCancellationRequested)
            {
                OperationCanceledException cancelled = new("UDP broadcast was cancelled.", error, cancellationToken);
                _logger.UdpService_Cancelled(packet);
                responseChannel.Writer.TryComplete(cancelled);
            }
            catch (UdpException error)
            {
                _logger.UdpService_Failed(packet, error);
                responseChannel.Writer.TryComplete(error);
            }
            catch (Exception error)
            {
                _logger.UdpService_UnexpectedError(packet, error);
                responseChannel.Writer.TryComplete(error);
            }
        }, cancellationToken);

        return responseChannel.Reader.ReadAllAsync(cancellationToken);
    }

    async Task<ResponsePacket> IUdpService.SendAsync(SendPacket packet, CancellationToken cancellationToken)
    {
        try
        {
            using ISocket socket = _socketFactory.Create();

            DateTime startTime = DateTime.Now;
            TimeSpan timeout = TimeSpan.FromSeconds(_settings.SendTimeout);

            Memory<byte> responseBuffer = new byte[0x400];

            while (true)
            {
                TimeSpan timeLeft = timeout - (DateTime.Now - startTime);
                using CancellationTokenSource timeoutCancellation = new(timeLeft);
                using CancellationTokenSource combinedCancel = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutCancellation.Token);

                _logger.UdpService_Sending(packet, packet.Size, packet.RemoteEndPoint);

                try
                {
                    await socket.SendToAsync(packet.GetBuffer(), packet.RemoteEndPoint, combinedCancel.Token);
                    combinedCancel.Token.ThrowIfCancellationRequested();

                    _logger.UdpService_Sent(packet);

                    SocketReceiveFromResult result = await socket.ReceiveFromAsync(responseBuffer, packet.RemoteEndPoint, combinedCancel.Token);
                    combinedCancel.Token.ThrowIfCancellationRequested();
                    responseBuffer = responseBuffer.Slice(0, result.ReceivedBytes);

                    _logger.UdpService_ReceivedResponse(packet, responseBuffer.Length, result.RemoteEndPoint);
                    break;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    if (DateTime.Now - startTime > timeout)
                    {
                        throw Errors.UdpService_NetworkTimeoutError(timeout);
                    }
                }
            }

            if (responseBuffer.Length < MinimumResponseSize)
            {
                throw Errors.UdpService_ResponseTooShort(MinimumResponseSize, responseBuffer.Length);
            }

            ResponsePacket responsePacket = new ResponsePacket(packet.RemoteEndPoint, responseBuffer);
            int realCheckSum = responsePacket.ComputeChecksum();

            if (realCheckSum != responsePacket.Header.NominalChecksum)
            {
                throw Errors.UdpService_ChecksumMismatch(responsePacket.Header.NominalChecksum, realCheckSum);
            }

            return responsePacket;
        }
        catch (OperationCanceledException)
        {
            _logger.UdpService_Cancelled(packet);
            throw;
        }
        catch (UdpException error) when (cancellationToken.IsCancellationRequested)
        {
            _logger.UdpService_Cancelled(packet);
            throw new OperationCanceledException("UDP send was cancelled.", error, cancellationToken);
        }
        catch (Exception error) when (cancellationToken.IsCancellationRequested)
        {
            _logger.UdpService_Cancelled(packet);
            throw new OperationCanceledException("UDP send was cancelled.", error, cancellationToken);
        }
        catch (UdpException error)
        {
            _logger.UdpService_Failed(packet, error);
            throw;
        }
        catch (Exception error)
        {
            _logger.UdpService_UnexpectedError(packet, error);
            throw;
        }
    }
}
