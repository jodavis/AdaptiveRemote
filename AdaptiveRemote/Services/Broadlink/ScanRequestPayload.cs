using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdaptiveRemote.Services.Broadlink;

internal class ScanRequestPayload : Payload
{
    public ScanRequestPayload()
        : base(0x30)
    {
        Set(0x26, 6);
    }

    public DateTime RequestTime
    {
        get => DateTime.FromBinary(GetLong(0x08));
        set => Set(0x08, value.ToBinary());
    }

    public byte[] LocalIPAddress
    {
        get => GetBytes(0x18, 4);
        set => Set(0x18, value);
    }

    public short LocalPort
    {
        get => GetShort(0x1C);
        set => Set(0x1C, value);
    }

    public short Checksum
    {
        get => GetShort(0x20);
        set => Set(0x20, value);
    }
}
