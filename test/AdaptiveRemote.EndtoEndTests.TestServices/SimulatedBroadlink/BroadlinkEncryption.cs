using System.Security.Cryptography;

namespace AdaptiveRemote.EndtoEndTests.SimulatedBroadlink;

/// <summary>
/// Independent AES encryption/decryption for the simulated Broadlink device.
/// Implements the same algorithm as the real device but independently from the app code.
/// </summary>
internal sealed class BroadlinkEncryption : IDisposable
{
    private static readonly byte[] InitialKey = [0x09, 0x76, 0x28, 0x34, 0x3f, 0xe9, 0x9e, 0x23, 0x76, 0x5c, 0x15, 0x13, 0xac, 0xcf, 0x8b, 0x02];
    private static readonly byte[] InitialVector = [0x56, 0x2e, 0x17, 0x99, 0x6d, 0x09, 0x3d, 0x28, 0xdd, 0xb3, 0xba, 0x69, 0x5a, 0x2e, 0x6f, 0x58];

    private readonly Aes _aes;

    public BroadlinkEncryption(byte[]? key = null)
    {
        _aes = Aes.Create();
        _aes.KeySize = 256;
        _aes.BlockSize = 128;
        _aes.Mode = CipherMode.CBC;
        _aes.IV = InitialVector;
        _aes.Key = key ?? InitialKey;
        _aes.Padding = PaddingMode.None;
    }

    public byte[] Encrypt(byte[] data)
    {
        using ICryptoTransform encryptor = _aes.CreateEncryptor();
        using MemoryStream output = new MemoryStream();
        using CryptoStream cryptoStream = new CryptoStream(output, encryptor, CryptoStreamMode.Write);

        cryptoStream.Write(data);

        // Add padding if needed
        int padding = (8192 - data.Length) % 16;
        if (padding > 0)
        {
            byte[] paddingBytes = new byte[padding];
            cryptoStream.Write(paddingBytes, 0, padding);
        }

        cryptoStream.FlushFinalBlock();
        return output.ToArray();
    }

    public byte[] Decrypt(byte[] data)
    {
        using ICryptoTransform decryptor = _aes.CreateDecryptor();
        using MemoryStream input = new MemoryStream(data);
        using CryptoStream cryptoStream = new CryptoStream(input, decryptor, CryptoStreamMode.Read);
        using MemoryStream output = new MemoryStream();

        cryptoStream.CopyTo(output);
        return output.ToArray();
    }

    public void Dispose()
    {
        _aes.Dispose();
    }
}
