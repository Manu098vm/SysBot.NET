using PKHeX.Core;
using System;
using System.Buffers.Binary;

namespace SysBot.Pokemon;

public sealed class TradePartnerBS
{
    public uint TID7 { get; }
    public uint SID7 { get; }
    public uint TrainerID { get; }
    public string TrainerName { get; }
    public int Game { get; }
    public int Gender { get; }
    public int Language { get; }

    public TradePartnerBS(byte[] TIDSID, byte[] trainerNameObject, byte[] info)
    {
        var tidsid = BitConverter.ToUInt32(TIDSID, 0);
        TID7 = BinaryPrimitives.ReadUInt32LittleEndian(TIDSID.AsSpan()) % 1_000_000;
        SID7 = BinaryPrimitives.ReadUInt32LittleEndian(TIDSID.AsSpan()) / 1_000_000;
        TrainerID = tidsid;
        TrainerName = ReadStringFromRAMObject(trainerNameObject);
        Game = info[0];
        Gender = 0; //TODO
        Language = info[4];
    }

    public const int MaxByteLengthStringObject = 0x14 + 0x1A;

    public static string ReadStringFromRAMObject(byte[] obj)
    {
        // 0x10 typeinfo/monitor, 0x4 len, char[len]
        const int ofs_len = 0x10;
        const int ofs_chars = 0x14;

        // Detect string length, but be cautious about its correctness (protect against bad data)
        int maxCharCount = (obj.Length - ofs_chars) / 2;
        int length = BitConverter.ToInt32(obj, ofs_len);
        if (length < 0 || length > maxCharCount)
            length = maxCharCount;

        return StringConverter8.GetString(obj.AsSpan(ofs_chars, length * 2));
    }
}