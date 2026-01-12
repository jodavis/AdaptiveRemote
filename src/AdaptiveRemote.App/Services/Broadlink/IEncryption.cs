
namespace AdaptiveRemote.Services.Broadlink;

/// <summary>
/// Abstraction for an encryption model
/// </summary>
public interface IEncryption
{
    /// <summary>
    /// Encrypt <paramref name="memory"/> and return the enecrypted result
    /// </summary>
    /// <param name="memory">Memory to encrypt</param>
    /// <returns>encrypted result</returns>
    Memory<byte> Encrypt(Memory<byte> memory);

    /// <summary>
    /// Decrypt <paramref name="memory"/> and return the decrypted result
    /// </summary>
    /// <param name="memory">Memory to decrypt</param>
    /// <returns>Decrypted result</returns>
    Memory<byte> Decrypt(Memory<byte> memory);

    interface Factory
    {
        /// <summary>
        /// Return an encryption model using the default Broadlink key
        /// </summary>
        IEncryption Default { get; }

        /// <summary>
        /// Return an encryption model using the given <paramref name="encryptionKey"/>
        /// </summary>
        /// <param name="encryptionKey">The key to use for encryption</param>
        IEncryption Create(byte[] encryptionKey);
    }
}
