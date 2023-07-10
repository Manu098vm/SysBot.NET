using PKHeX.Core;
using System;
using System.Buffers.Binary;

namespace SysBot.Pokemon;

public sealed class TradePartnerLA
{
    public uint TID7 { get; }
    public uint SID7 { get; }
    public string TrainerName { get; }
    public int Game { get; }
    public int Gender { get; }
    public int Language { get; }

    public TradePartnerLA(byte[] TIDSID, byte[] trainerNameObject, byte[] info)
    {
        TID7 = BinaryPrimitives.ReadUInt32LittleEndian(TIDSID.AsSpan()) % 1_000_000;
        SID7 = BinaryPrimitives.ReadUInt32LittleEndian(TIDSID.AsSpan()) / 1_000_000;
        TrainerName = StringConverter8.GetString(trainerNameObject);
        Game = info[0];
        Gender = info[1];
        Language = info[3];
    }

    public const int MaxByteLengthStringObject = 0x26;
}