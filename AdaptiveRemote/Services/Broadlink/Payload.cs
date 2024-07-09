using System.Text;

namespace AdaptiveRemote.Services.Broadlink;

internal class Payload
{
    private Memory<byte> _buffer;

    public Payload(Memory<byte> buffer)
    {
        _buffer = buffer;
    }

    public Payload(int input)
        : this(new byte[input])
    { }

    public Payload(params Payload[] payloads)
        : this(payloads.Sum(x => x._buffer.Length))
    {
        int startPosition = 0;
        foreach (Payload payload in payloads)
        {
            Memory<byte> newBuffer = _buffer.Slice(startPosition, payload._buffer.Length);
            payload._buffer.CopyTo(newBuffer);
            payload._buffer = newBuffer;
            startPosition += newBuffer.Length;
        }
    }

    internal Memory<byte> GetBuffer() => _buffer;
    internal int Size => _buffer.Length;

    protected virtual Span<byte> ChecksumSpanToIgnore => Span<byte>.Empty;

    public short ComputeChecksum()
    {
        int checksum = 0xBEAF;

        Span<byte> span = _buffer.Span;
        for (int i = 0; i < span.Length; i++)
        {
            checksum += span[i];
        }

        Span<byte> ignore = ChecksumSpanToIgnore;
        for (int i = 0; i < ignore.Length; i++)
        {
            checksum -= ignore[i];
        }

        return (short)(checksum & 0xFFFF);
    }

    protected int GetInt(int position) => BitConverter.ToInt32(Span(position, 4));
    protected void Set(int position, int value) => BitConverter.GetBytes(value).CopyTo(Span(position, 4));
    protected long GetLong(int position) => BitConverter.ToInt64(Span(position, 8));
    protected void Set(int position, long value) => BitConverter.GetBytes(value).CopyTo(Span(position, 8));
    protected short GetShort(int position) => BitConverter.ToInt16(Span(position, 2));
    protected void Set(int position, short value) => BitConverter.GetBytes(value).CopyTo(Span(position, 2));
    protected string GetString(int position)
    {
        Span<byte> stringSpan = Span(position);
        int stringLength = stringSpan.IndexOf((byte)0);
        stringSpan = stringSpan.Slice(0, stringLength);
        return Encoding.ASCII.GetString(stringSpan);
    }
    protected void Set(int position, string value) => Encoding.ASCII.GetBytes(value).CopyTo(Span(position));
    protected byte[] GetBytes(int position) => Span(position).ToArray();
    protected byte[] GetBytes(int position, int length) => Span(position, length).ToArray();
    protected void Set(int position, byte[] value) => value.CopyTo(Span(position, value.Length));

    protected Span<byte> Span(int position) => _buffer.Span.Slice(position);
    protected Span<byte> Span(int position, int length) => _buffer.Span.Slice(position, length);

    public override string ToString() => $"{GetType().Name} ({Size} bytes)";
}
