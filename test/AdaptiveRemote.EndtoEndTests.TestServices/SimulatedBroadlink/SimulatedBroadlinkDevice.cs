using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;

/// <summary>
/// Simulates a Broadlink IR device for E2E testing.
/// Accepts UDP packets and records commands according to the Broadlink protocol.
/// </summary>
public sealed class SimulatedBroadlinkDevice : ISimulatedBroadlinkDevice
{
    private const short DefaultDeviceType = 0x2737; // RM Mini 3
    private const int AuthenticateCommand = 0x65;
    private const int SendDataCommand = 0x6A;

    // Sub-commands within the 0x6A packet type
    private const int SubCommandSendData = 0x2;
    private const int SubCommandEnterLearning = 0x3;
    private const int SubCommandCheckLearnedData = 0x4;

    // Offset of the sub-command int within the decrypted payload
    private const int SubCommandOffset = 2;

    private readonly ILogger _logger;
    private readonly UdpClient _udpClient;
    private readonly PhysicalAddress _macAddress;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ConcurrentBag<RecordedPacket> _recordedPackets = new();
    private readonly Task _listenerTask;

    private short _deviceId;
    private BroadlinkEncryption? _encryption;
    private bool _disposed;

    // Learning mode state
    private volatile bool _isInLearningMode;
    private byte[]? _pendingLearnedData;
    private short? _pendingCheckError;
    private readonly object _learningLock = new();

    internal SimulatedBroadlinkDevice(int port, ILogger logger)
    {
        _logger = logger;
        _udpClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
        Port = ((IPEndPoint)_udpClient.Client.LocalEndPoint!).Port;

        // Generate a random MAC address for testing
        byte[] macBytes = new byte[6];
        Random.Shared.NextBytes(macBytes);
        macBytes[0] = (byte)((macBytes[0] & 0xFE) | 0x02); // Set locally administered bit
        _macAddress = new PhysicalAddress(macBytes);

        _deviceId = 0;
        _encryption = new BroadlinkEncryption();

        _logger.LogInformation("SimulatedBroadlinkDevice started on UDP port {Port} with MAC {MacAddress}", Port, _macAddress);

        _listenerTask = Task.Run(() => ListenAsync(_cancellationTokenSource.Token));
    }

    /// <inheritdoc/>
    public int Port { get; }

    /// <inheritdoc/>
    public bool IsInLearningMode => _isInLearningMode;

    /// <inheritdoc/>
    public void ProvideLearnedData(byte[] data)
    {
        lock (_learningLock)
        {
            _pendingLearnedData = data;
        }

        _logger.LogInformation("SimulatedBroadlinkDevice: learned data queued ({Length} bytes)", data.Length);
    }

    /// <inheritdoc/>
    public void SimulateNextCheckError(short errorCode)
    {
        lock (_learningLock)
        {
            _pendingCheckError = errorCode;
        }

        _logger.LogInformation("SimulatedBroadlinkDevice: next CheckLearnedData will return error code {ErrorCode}", errorCode);
    }

    /// <inheritdoc/>
    public IReadOnlyList<RecordedPacket> GetRecordedPackets()
    {
        return _recordedPackets.ToList();
    }

    /// <inheritdoc/>
    public void ClearRecordedPackets()
    {
        while (_recordedPackets.TryTake(out _))
        {
            // Remove all items
        }
        _logger.LogInformation("Cleared recorded packets");
    }

    /// <inheritdoc/>
    public void Stop()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Stopping SimulatedBroadlinkDevice on port {Port}", Port);

        _cancellationTokenSource.Cancel();
        _udpClient.Close();

        try
        {
            WaitHelpers.WaitForAsyncTask(_ => _listenerTask, TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error waiting for listener task to complete");
        }

        _disposed = true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Stop();
        _cancellationTokenSource.Dispose();
        _udpClient.Dispose();
        _encryption?.Dispose();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result = await _udpClient.ReceiveAsync(cancellationToken);
                _ = Task.Run(() => HandlePacketAsync(result.Buffer, result.RemoteEndPoint, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving UDP packet");
            }
        }
    }

    private async Task HandlePacketAsync(byte[] data, IPEndPoint remoteEndPoint, CancellationToken _)
    {
        try
        {
            // Try to decode as a scan (discovery) request first
            if (BroadlinkPacketDecoder.TryDecodeScanRequest(data, out DecodedScanRequest? scanRequest, out string? scanError))
            {
                _logger.LogInformation("Received discovery request from {RemoteEndPoint}", remoteEndPoint);
                await HandleDiscoveryRequestAsync(scanRequest!, remoteEndPoint);
                return;
            }

            // Try to decode as a regular packet
            if (BroadlinkPacketDecoder.TryDecodePacket(data, remoteEndPoint, out DecodedPacket? packet, out string? error))
            {
                await HandleCommandPacketAsync(packet!, remoteEndPoint);
            }
            else
            {
                _logger.LogWarning("Failed to decode packet from {RemoteEndPoint}: {Error}", remoteEndPoint, error);
                RecordMalformedPacket(remoteEndPoint, error ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling packet from {RemoteEndPoint}", remoteEndPoint);
        }
    }

    private async Task HandleDiscoveryRequestAsync(DecodedScanRequest request, IPEndPoint remoteEndPoint)
    {
        try
        {
            // Build discovery response
            byte[] response = BroadlinkPacketEncoder.EncodeScanResponse(
                DefaultDeviceType,
                _macAddress,
                isLocked: false);

            // Send response to the port specified in the request
            // Convert signed short to unsigned port number (handles ports > 32767)
            int localPort = request.LocalPort & 0xFFFF;
            IPEndPoint responseEndPoint = new IPEndPoint(remoteEndPoint.Address, localPort);

            int bytesSent = await _udpClient.SendAsync(response, response.Length, responseEndPoint);

            _logger.LogInformation(
                "Sent discovery response ({BytesSent} bytes) to {EndPoint}",
                bytesSent,
                responseEndPoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error sending discovery response to {RemoteEndPoint}: {ExceptionType} - {Message}",
                remoteEndPoint,
                ex.GetType().Name,
                ex.Message);
            throw;
        }
    }

    private async Task HandleCommandPacketAsync(DecodedPacket packet, IPEndPoint remoteEndPoint)
    {
        _logger.LogInformation("Received packet type 0x{PacketType:X2} from {RemoteEndPoint}", packet.PacketType, remoteEndPoint);

        switch (packet.PacketType)
        {
            case AuthenticateCommand:
                await HandleAuthenticateAsync(packet, remoteEndPoint);
                break;

            case SendDataCommand:
                await HandleSendDataCommandAsync(packet, remoteEndPoint);
                break;

            default:
                _logger.LogWarning("Unknown packet type 0x{PacketType:X2}", packet.PacketType);
                break;
        }
    }

    private async Task HandleAuthenticateAsync(DecodedPacket packet, IPEndPoint remoteEndPoint)
    {
        _logger.LogInformation("Handling authentication request");

        // Generate a new device ID
        _deviceId = (short)Random.Shared.Next(0x1000, 0x7FFF);

        // Generate a new encryption key
        byte[] newKey = new byte[16];
        Random.Shared.NextBytes(newKey);

        // Create response payload with device ID and encryption key
        byte[] responsePayload = new byte[0x50];
        Array.Copy(BitConverter.GetBytes(_deviceId), 0, responsePayload, 0x00, 2);
        Array.Copy(newKey, 0, responsePayload, 0x04, 16);

        // Encrypt the response with the default key
        _encryption?.Dispose();
        _encryption = new BroadlinkEncryption();
        byte[] encryptedPayload = _encryption.Encrypt(responsePayload);

        // Build response packet
        byte[] response = BroadlinkPacketEncoder.EncodeResponse(
            DefaultDeviceType,
            AuthenticateCommand,
            packet.MessageCount,
            packet.HostAddress,
            _deviceId,
            encryptedPayload,
            errorCode: 0);

        // Send response
        await _udpClient.SendAsync(response, response.Length, remoteEndPoint);

        // Switch to the new encryption key
        _encryption?.Dispose();
        _encryption = new BroadlinkEncryption(newKey);

        _logger.LogInformation("Authentication complete, assigned device ID 0x{DeviceId:X4}", _deviceId);

        // Record the packet
        RecordPacket(new RecordedPacket
        {
            ReceivedAt = DateTimeOffset.UtcNow,
            IsInbound = true,
            PacketType = AuthenticateCommand,
            DebugDescription = $"Authenticate request (Device ID: 0x{_deviceId:X4})"
        });
    }

    private async Task HandleSendDataCommandAsync(DecodedPacket packet, IPEndPoint remoteEndPoint)
    {
        // Decrypt the payload to determine the sub-command
        byte[] decryptedPayload;
        try
        {
            decryptedPayload = _encryption!.Decrypt(packet.Payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt payload");
            RecordMalformedPacket(remoteEndPoint, "Decryption failed");
            return;
        }

        // Read sub-command from offset 2 (after the 2-byte CommandAndDataLength)
        int subCommand = decryptedPayload.Length >= SubCommandOffset + 4
            ? BitConverter.ToInt32(decryptedPayload, SubCommandOffset)
            : -1;

        _logger.LogInformation("Handling 0x6A packet with sub-command 0x{SubCommand:X}", subCommand);

        switch (subCommand)
        {
            case SubCommandSendData:
                await HandleSendDataAsync(packet, decryptedPayload, remoteEndPoint);
                break;

            case SubCommandEnterLearning:
                await HandleEnterLearningAsync(packet, remoteEndPoint);
                break;

            case SubCommandCheckLearnedData:
                await HandleCheckLearnedDataAsync(packet, remoteEndPoint);
                break;

            default:
                _logger.LogWarning("Unknown 0x6A sub-command 0x{SubCommand:X}", subCommand);
                break;
        }
    }

    private async Task HandleSendDataAsync(DecodedPacket packet, byte[] decryptedPayload, IPEndPoint remoteEndPoint)
    {
        _logger.LogInformation("Handling send data command");

        // Extract IR data from the command payload
        // Format: [length:2][command:4][data:...]
        byte[]? irData = null;
        if (decryptedPayload.Length >= 6)
        {
            short dataLength = BitConverter.ToInt16(decryptedPayload, 0);
            // Command type at offset 2 (not currently validated)

            if (decryptedPayload.Length >= 6 + (dataLength - 4))
            {
                irData = new byte[dataLength - 4];
                Array.Copy(decryptedPayload, 6, irData, 0, irData.Length);
            }
        }

        // Record the packet
        RecordPacket(new RecordedPacket
        {
            ReceivedAt = DateTimeOffset.UtcNow,
            IsInbound = true,
            PacketType = SendDataCommand,
            RawPayload = irData,
            DebugDescription = $"Send IR command ({irData?.Length ?? 0} bytes)"
        });

        // Build response
        byte[] responsePayload = _encryption!.Encrypt(Array.Empty<byte>());
        byte[] response = BroadlinkPacketEncoder.EncodeResponse(
            DefaultDeviceType,
            SendDataCommand,
            packet.MessageCount,
            packet.HostAddress,
            _deviceId,
            responsePayload,
            errorCode: 0);

        // Send response
        await _udpClient.SendAsync(response, response.Length, remoteEndPoint);

        _logger.LogInformation("Send data command complete, IR payload: {PayloadSize} bytes", irData?.Length ?? 0);
    }

    private async Task HandleEnterLearningAsync(DecodedPacket packet, IPEndPoint remoteEndPoint)
    {
        _logger.LogInformation("Handling enter learning mode command");

        lock (_learningLock)
        {
            _isInLearningMode = true;
            _pendingLearnedData = null; // Clear any previous learned data
        }

        // Record the packet
        RecordPacket(new RecordedPacket
        {
            ReceivedAt = DateTimeOffset.UtcNow,
            IsInbound = true,
            PacketType = SendDataCommand,
            DebugDescription = "Enter learning mode request"
        });

        // Respond with success
        byte[] responsePayload = _encryption!.Encrypt(Array.Empty<byte>());
        byte[] response = BroadlinkPacketEncoder.EncodeResponse(
            DefaultDeviceType,
            SendDataCommand,
            packet.MessageCount,
            packet.HostAddress,
            _deviceId,
            responsePayload,
            errorCode: 0);

        await _udpClient.SendAsync(response, response.Length, remoteEndPoint);

        _logger.LogInformation("Enter learning mode complete");
    }

    private async Task HandleCheckLearnedDataAsync(DecodedPacket packet, IPEndPoint remoteEndPoint)
    {
        _logger.LogInformation("Handling check learned data command");

        byte[]? learnedData;
        short? errorCode;
        lock (_learningLock)
        {
            errorCode = _pendingCheckError;
            if (errorCode is not null)
            {
                _pendingCheckError = null;
                _isInLearningMode = false;
                learnedData = null;
            }
            else
            {
                learnedData = _pendingLearnedData;
                if (learnedData is not null)
                {
                    _pendingLearnedData = null;
                    _isInLearningMode = false;
                }
            }
        }

        // Record the packet
        RecordPacket(new RecordedPacket
        {
            ReceivedAt = DateTimeOffset.UtcNow,
            IsInbound = true,
            PacketType = SendDataCommand,
            DebugDescription = errorCode is not null
                ? $"Check learned data (simulated error: {errorCode})"
                : learnedData is not null
                    ? $"Check learned data (data available: {learnedData.Length} bytes)"
                    : "Check learned data (no data yet)"
        });

        if (errorCode is not null)
        {
            // Return the simulated error code
            byte[] emptyPayload = _encryption!.Encrypt(Array.Empty<byte>());
            byte[] errorResponse = BroadlinkPacketEncoder.EncodeResponse(
                DefaultDeviceType,
                SendDataCommand,
                packet.MessageCount,
                packet.HostAddress,
                _deviceId,
                emptyPayload,
                errorCode: errorCode.Value);

            await _udpClient.SendAsync(errorResponse, errorResponse.Length, remoteEndPoint);
            _logger.LogInformation("Check learned data: returned simulated error code {ErrorCode}", errorCode);
            return;
        }

        if (learnedData is null)
        {
            // No data yet: respond with error code -1 (app should keep polling)
            byte[] emptyPayload = _encryption!.Encrypt(Array.Empty<byte>());
            byte[] notReadyResponse = BroadlinkPacketEncoder.EncodeResponse(
                DefaultDeviceType,
                SendDataCommand,
                packet.MessageCount,
                packet.HostAddress,
                _deviceId,
                emptyPayload,
                errorCode: -1);

            await _udpClient.SendAsync(notReadyResponse, notReadyResponse.Length, remoteEndPoint);

            _logger.LogInformation("Check learned data: no data available, returned error code -1");
            return;
        }

        // Data available: build LearnedDataResponsePayload (IR data at offset 0x04)
        byte[] responseData = new byte[6 + learnedData.Length];
        Array.Copy(BitConverter.GetBytes((short)learnedData.Length), 0, responseData, 0, 2);
        Array.Copy(learnedData, 0, responseData, 6, learnedData.Length);

        byte[] encryptedResponseData = _encryption!.Encrypt(responseData);
        byte[] dataResponse = BroadlinkPacketEncoder.EncodeResponse(
            DefaultDeviceType,
            SendDataCommand,
            packet.MessageCount,
            packet.HostAddress,
            _deviceId,
            encryptedResponseData,
            errorCode: 0);

        await _udpClient.SendAsync(dataResponse, dataResponse.Length, remoteEndPoint);

        _logger.LogInformation("Check learned data: returned {Length} bytes of learned IR data", learnedData.Length);
    }

    private void RecordPacket(RecordedPacket packet)
    {
        _recordedPackets.Add(packet);
        _logger.LogInformation("Recorded packet: {Description}", packet.DebugDescription);
    }

    private void RecordMalformedPacket(EndPoint remoteEndPoint, string error)
    {
        RecordPacket(new RecordedPacket
        {
            ReceivedAt = DateTimeOffset.UtcNow,
            IsInbound = true,
            PacketType = 0,
            IsMalformed = true,
            DebugDescription = $"Malformed packet from {remoteEndPoint}: {error}"
        });
    }
}
