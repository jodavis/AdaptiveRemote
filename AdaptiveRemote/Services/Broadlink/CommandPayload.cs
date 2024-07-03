namespace AdaptiveRemote.Services.Broadlink;

internal class CommandPayload : Payload
{
    public CommandPayload(int command, byte[] data)
        : base(data.Length + 6)
    {
        CommandAndDataLength = (short)(data.Length + 4);
        Command = command;
        Data = data;
    }

    public short CommandAndDataLength
    {
        get => GetShort(0x00);
        set => Set(0x00, value);
    }

    public int Command
    {
        get => GetInt(0x02);
        set => Set(0x02, value);
    }

    public byte[] Data
    {
        get => GetBytes(0x06);
        set => Set(0x06, value);
    }
}
