namespace AdaptiveRemote.Services.Broadlink;

/// <summary>
/// <see cref="https://github.com/mjg59/python-broadlink/blob/master/protocol.md#network-discovery"/>
/// </summary>
internal class CommandPayload : Payload
{
    private const int CommandAndDataLengthSize = sizeof(short);
    private const int CommandSize = sizeof(int);

    public CommandPayload(int command, byte[] data)
        : base(data.Length + DataIndex)
    {
        CommandAndDataLength = (short)(data.Length + CommandSize);
        Command = command;
        Data = data;
    }

    private const int CommandAndDataLengthIndex = 0x00;
    /// <summary>
    /// The length of the data plus the size of the command that preceeds it
    /// </summary>
    public short CommandAndDataLength
    {
        get => GetShort(CommandAndDataLengthIndex);
        set => Set(CommandAndDataLengthIndex, value);
    }

    private const int CommandIndex = CommandAndDataLengthIndex + CommandAndDataLengthSize;
    /// <summary>
    /// The command code to send before the data
    /// </summary>
    public int Command
    {
        get => GetInt(CommandIndex);
        set => Set(CommandIndex, value);
    }

    private const int DataIndex = CommandAndDataLengthSize + CommandSize;
    /// <summary>
    /// The data to send
    /// </summary>
    public byte[] Data
    {
        get => GetBytes(DataIndex);
        set => Set(DataIndex, value);
    }
}
