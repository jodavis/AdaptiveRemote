using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Channels;
using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Broadlink;

internal class UdpService : IUdpService
{
    private const int MinimumResponseSize = 0x30;

    private readonly ISocketFactory _socketFactory;
    private readonly BroadlinkSettings _settings;
    private readonly ILogger<UdpService> _logger;

    public UdpService(ISocketFactory socketFactory, IOptions<BroadlinkSettings> settings, ILogger<UdpService> logger)
    {
        _socketFactory = socketFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    IAsyncEnumerable<ScanResponsePayload> IUdpService.BroadcastAsync(ScanRequestPayload packet, CancellationToken cancellationToken)
    {
        Channel<ScanResponsePayload> responseChannel = Channel.CreateUnbounded<ScanResponsePayload>(new()
        {
            AllowSynchronousContinuations = true,
            SingleReader = true,
            SingleWriter = true,
        });

        _ = Task.Run(async () =>
        {

            IPEndPoint discoverEndPoint = IPEndPoint.Parse("255.255.255.255:80");

            using ISocket socket = _socketFactory.CreateForBroadcast();

            DateTime startTime = DateTime.Now;
            TimeSpan timeout = TimeSpan.FromSeconds(_settings.ScanTimeout);
            HashSet<(EndPoint, PhysicalAddress, int)> discovered = new();

            try
            {
                while (DateTime.Now - startTime < timeout)
                {
                    TimeSpan timeLeft = timeout - (DateTime.Now - startTime);
                    socket.SetTimeout(timeLeft);

                    _logger.LogInformation(Message.UdpService_Sending, "Scan");

                    await socket.SendToAsync(packet.GetBuffer(), discoverEndPoint, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    _logger.LogInformation(Message.UdpService_Sent, packet);

                    while (true)
                    {
                        Memory<byte> buffer = new byte[0x400];
                        SocketReceiveFromResult result;
                        try
                        {
                            result = await socket.ReceiveFromAsync(buffer, discoverEndPoint, cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();
                            buffer = buffer.Slice(result.ReceivedBytes);

                            _logger.LogInformation(Message.UdpService_ReceivedResponse, packet);
                        }
                        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                        {
                            break;
                        }

                        ScanResponsePayload response = new(buffer)
                        {
                            HostEndPoint = discoverEndPoint,
                        };

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
                _logger.LogInformation(Message.UdpService_Cancelled, packet);
                responseChannel.Writer.TryComplete(error);
            }
            catch (UdpException error)
            {
                _logger.LogError(Message.UdpService_Failed, packet, error.Message);
                responseChannel.Writer.TryComplete(error);
            }
            catch (Exception error)
            {
                _logger.LogError(Message.UdpService_UnexpectedError, packet, error.Message);
                responseChannel.Writer.TryComplete(error);
            }
        }, cancellationToken);

        return responseChannel.Reader.ReadAllAsync(cancellationToken);
    }

    async Task<ResponsePacket> IUdpService.SendAsync(EndPoint remoteEndPoint, SendPacket packet, CancellationToken cancellationToken)
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
                socket.SetTimeout(timeLeft);

                _logger.LogInformation(Message.UdpService_Sending, packet, packet.Size, remoteEndPoint);

                await socket.SendToAsync(packet.GetBuffer(), remoteEndPoint, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogInformation(Message.UdpService_Sent, packet);

                try
                {
                    SocketReceiveFromResult result = await socket.ReceiveFromAsync(responseBuffer, remoteEndPoint, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    responseBuffer = responseBuffer.Slice(0, result.ReceivedBytes);

                    _logger.LogInformation(Message.UdpService_ReceivedResponse, packet, responseBuffer.Length, result.RemoteEndPoint);
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
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

            ResponsePacket responsePacket = new ResponsePacket(remoteEndPoint, responseBuffer);
            int realCheckSum = responsePacket.ComputeChecksum();

            if (realCheckSum != responsePacket.Header.NominalChecksum)
            {
                throw Errors.UdpService_ChecksumMismatch(responsePacket.Header.NominalChecksum, realCheckSum);
            }

            return responsePacket;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(Message.UdpService_Cancelled, packet);
            throw;
        }
        catch (UdpException error)
        {
            _logger.LogError(Message.UdpService_Failed, packet, error.Message);
            throw;
        }
        catch (Exception error)
        {
            _logger.LogError(Message.UdpService_UnexpectedError, packet, error.Message);
            throw;
        }
    }
}
