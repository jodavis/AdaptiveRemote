namespace AdaptiveRemote.Services.Broadlink;

public interface IEncryptionFactory
{
    IEncryption Default { get; }

    IEncryption Create(byte[] encryptionKey);
}
