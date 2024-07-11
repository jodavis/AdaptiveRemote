
namespace AdaptiveRemote.Services.Broadlink;

public interface IEncryption
{
    Memory<byte> Decrypt(Memory<byte> memory);
    Memory<byte> Encrypt(Memory<byte> memory);
}
