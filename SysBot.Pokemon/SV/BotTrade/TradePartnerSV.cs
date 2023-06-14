using PKHeX.Core;
using System;
using System.Buffers.Binary;

namespace SysBot.Pokemon
{
    public sealed class TradePartnerSV
    {
        public TradeMyStatus Info { get; private set; }

        public string TID7 { get => $"{Info.DisplayTID:D6}"; }
        public string SID7 { get => $"{Info.DisplaySID:D4}"; }
        public string TrainerName { get => Info.OT; }

        public TradePartnerSV(TradeMyStatus info) => Info = info;
    }

    public sealed class TradeMyStatus
    {
        public readonly byte[] Data = new byte[0x30];

        public uint DisplaySID => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(0)) / 1_000_000;
        public uint DisplayTID => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(0)) % 1_000_000;

        public int Game => Data[4];
        public int Gender => Data[5];
        public int Language => Data[6];

        public string OT => StringConverter8.GetString(Data.AsSpan(8, 24));
    }
}