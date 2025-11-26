using System.Net;
using System.Net.NetworkInformation;

namespace AdaptiveRemote.Services.Broadlink;

internal class DeviceConnection : IDeviceConnection
{
    private const int AuthenticateCommandComde = 0x65;
    private const int SendDataCommandCode = 0x6A;

    private readonly IPEndPoint _hostEndPoint;
    private readonly PhysicalAddress _hostAddress;
    private readonly short _deviceType;
    private readonly IUdpService _udpService;
    private readonly IEncryption.Factory _encryptionFactory;

    private short _deviceID;
    private int _messageCount;
    private IEncryption _encryption;

    public DeviceConnection(IPEndPoint hostEndPoint, PhysicalAddress hostAddress, short deviceType, IUdpService udpService, IEncryption.Factory encryptionFactory)
    {
        _hostEndPoint = hostEndPoint;
        _hostAddress = hostAddress;
        _deviceType = deviceType;
        _udpService = udpService;
        _encryptionFactory = encryptionFactory;

        _messageCount = Random.Shared.Next(0x8000, 0xFFFF);
        _deviceID = 0;
        _encryption = _encryptionFactory.Default;
    }

    public async Task<bool> AuthenticateAsync(CancellationToken cancellationToken)
    {
        _deviceID = 0;
        _encryption = _encryptionFactory.Default;

        AuthenticateRequestPayload payload = new();

        ResponsePacket response = await SendPacketAsync(AuthenticateCommandComde, payload, cancellationToken);
        CheckError(response.Header.ErrorCode);
        AuthenticateResponsePayload responsePayload = Decrypt<AuthenticateResponsePayload>(response.Payload);

        _deviceID = responsePayload.DeviceID;
        _encryption = _encryptionFactory.Create(responsePayload.EncryptionKey);

        return true;
    }

    public async Task SendData(byte[] data, CancellationToken cancellationToken)
    {
        CommandPayload payload = new(0x2, data);
        ResponsePacket response = await SendPacketAsync(SendDataCommandCode, payload, cancellationToken);
        CheckError(response.Header.ErrorCode);
    }

    private async Task<ResponsePacket> SendPacketAsync(short packetType, Payload payload, CancellationToken cancellationToken)
    {
        _messageCount = ((_messageCount + 1) | 0x8000) & 0XFFFF;

        SendPacketHeader header = new()
        {
            DeviceType = _deviceType,
            PacketType = packetType,
            MessageCount = (short)_messageCount,
            HostAddress = _hostAddress,
            DeviceID = _deviceID,
            PayloadChecksum = payload.ComputeChecksum()
        };

        SendPacket packet = new(_hostEndPoint, header, Encrypt(payload));
        packet.Header.PacketChecksum = packet.ComputeChecksum();

        return await _udpService.SendAsync(packet, cancellationToken);
    }

    private DecryptedType Decrypt<DecryptedType>(Payload payload)
        where DecryptedType : Payload
        => (DecryptedType)Activator.CreateInstance(typeof(DecryptedType), _encryption.Decrypt(payload.GetBuffer()))!;
    private Payload Encrypt(Payload payload)
        => new Payload(_encryption.Encrypt(payload.GetBuffer()));

    private static void CheckError(short errorCode)
    {
        if (errorCode != 0)
        {
            throw errorCode switch
            {
                -1 => Errors.Broadlink_AuthenticationFailed(),
                -2 => Errors.Broadlink_Loggedout(),
                -3 => Errors.Broadlink_DeviceOffline(),
                -4 => Errors.Broadlink_CommandNotSupported(),
                -5 => Errors.Broadlink_StorageError(),
                -6 => Errors.Broadlink_AbnormalStructure(),
                -7 => Errors.Broadlink_ControlKeyExpired(),
                -8 => Errors.Broadlink_SendError(),
                -9 => Errors.Broadlink_WriteError(),
                -10 => Errors.Broadlink_ReadError(),
                -11 => Errors.Broadlink_SsidNotFound(),

                _ => Errors.Broadlink_UnknownError(),
            };
        }
    }

    internal class Factory(IUdpService _udpService, IEncryption.Factory _encryptionFactory) : IDeviceConnection.Factory
    {
        IDeviceConnection IDeviceConnection.Factory.Create(IPEndPoint hostEndPoint, PhysicalAddress hostAddress, short deviceType)
            => new DeviceConnection(hostEndPoint, hostAddress, deviceType, _udpService, _encryptionFactory);
    }
}
