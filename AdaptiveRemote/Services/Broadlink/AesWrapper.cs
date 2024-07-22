using System.IO;
using System.Security.Cryptography;

namespace AdaptiveRemote.Services.Broadlink;

internal class AesWrapper : IEncryption
{
    private static readonly byte[] InitialKey = [0x09, 0x76, 0x28, 0x34, 0x3f, 0xe9, 0x9e, 0x23, 0x76, 0x5c, 0x15, 0x13, 0xac, 0xcf, 0x8b, 0x02];
    private static readonly byte[] InitialVector = [0x56, 0x2e, 0x17, 0x99, 0x6d, 0x09, 0x3d, 0x28, 0xdd, 0xb3, 0xba, 0x69, 0x5a, 0x2e, 0x6f, 0x58];
    private static readonly byte[] PaddingBytes = new byte[15];

    private static readonly IEncryption _default = new AesWrapper(InitialKey);

    private readonly Aes _aes;

    private AesWrapper(byte[] key)
    {
        _aes = Aes.Create();
        _aes.KeySize = 256;
        _aes.BlockSize = 128;
        _aes.Mode = CipherMode.CBC;
        _aes.IV = InitialVector;
        _aes.Key = key;
        _aes.Padding = PaddingMode.None;
    }

    Memory<byte> IEncryption.Encrypt(Memory<byte> input)
    {
        using ICryptoTransform encryptor = _aes.CreateEncryptor();
        using MemoryStream toStream = new MemoryStream();
        using CryptoStream fromStream = new CryptoStream(toStream, encryptor, CryptoStreamMode.Write);

        fromStream.Write(input.Span);
        int padding = (8192 - input.Length) % 16;
        if (padding > 0)
        {
            fromStream.Write(PaddingBytes, 0, padding);
        }

        return toStream.ToArray();
    }

    Memory<byte> IEncryption.Decrypt(Memory<byte> input)
    {
        using ICryptoTransform decryptor = _aes.CreateDecryptor();
        using MemoryStream fromStream = new MemoryStream(input.ToArray());
        using CryptoStream toStream = new CryptoStream(fromStream, decryptor, CryptoStreamMode.Read);
        using MemoryStream resultStream = new MemoryStream();

        toStream.CopyTo(resultStream);

        return resultStream.ToArray();
    }

    internal class Factory : IEncryption.Factory
    {
        IEncryption IEncryption.Factory.Default => _default;

        IEncryption IEncryption.Factory.Create(byte[] encryptionKey) => new AesWrapper(encryptionKey);

    }
}
