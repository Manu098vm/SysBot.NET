using PKHeX.Core;

namespace SysBot.Pokemon;

public sealed class TradePartnerSWSH
{
    public uint TID7 { get; }
    public uint SID7 { get; }
    public string TrainerName { get; }
    public byte Game { get; }
    public byte Language { get; }
    public byte Gender { get; }

    public TradePartnerSWSH((uint TID7, uint SID7) ID, string ot, GameVersion version, LanguageID language, Gender gender)
    {
        TID7 = ID.TID7;
        SID7 = ID.SID7;
        TrainerName = ot;
        Game = (byte)version;
        Language = (byte)language;
        Gender = (byte)gender;
    }
}