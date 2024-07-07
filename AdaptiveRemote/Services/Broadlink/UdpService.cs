using System.Net;
using System.Net.Sockets;
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

    IAsyncEnumerable<ResponsePacket> IUdpService.BroadcastAsync(SendPacket packet, CancellationToken cancellationToken)
    {
        //IPEndPoint discoverEndpoint = new(0, 0); // DefaultBroadcastEndPoint?

        //ISocket socket = _socketFactory.CreateForBroadcast();

        //socket.SetTimeout(TimeSpan.FromSeconds(_settings.ScanTimeout));

        //// TODO: Retry with timeout (or don't, until it's needed?)

        //// TODO: Await this
        //// TODO: Pass cancellation
        //_ = socket.SendToAsync(packet.GetBuffer(), discoverEndpoint, default);
        //// TODO: Throw if cancelled

        //Memory<byte> buffer = new byte[0x400];

        //// TODO: Repeat the below to find more devices (or don't, until it's needed?)

        //// TODO: Await this
        //// TODO: Pass cancellation
        //SocketReceiveFromResult result = socket.ReceiveFromAsync(buffer, new IPEndPoint(0, 0), default).Result;
        //// TODO: Throw if cancelled
        //buffer = buffer.Slice(0, result.ReceivedBytes);

        //// TODO: What's the right return value here?
        //yield return new ScanResponsePayload(buffer);

        throw new NotImplementedException();
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

                _logger.LogInformation(Message.UdpService_Sending, packet.Header.MessageCount, packet.Size, remoteEndPoint);

                await socket.SendToAsync(packet.GetBuffer(), remoteEndPoint, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogInformation(Message.UdpService_Sent, packet.Header.MessageCount);

                try
                {
                    SocketReceiveFromResult result = await socket.ReceiveFromAsync(responseBuffer, remoteEndPoint, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    responseBuffer = responseBuffer.Slice(0, result.ReceivedBytes);

                    _logger.LogInformation(Message.UdpService_ReceivedResponse, packet.Header.MessageCount, responseBuffer.Length, result.RemoteEndPoint);
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
            _logger.LogInformation(Message.UdpService_Cancelled, packet.Header.MessageCount);
            throw;
        }
        catch (UdpException error)
        {
            _logger.LogError(Message.UdpService_Failed, packet.Header.MessageCount, error.Message);
            throw;
        }
        catch (Exception error)
        {
            _logger.LogError(Message.UdpService_UnexpectedError, packet.Header.MessageCount, error.Message);
            throw;
        }
    }
}
