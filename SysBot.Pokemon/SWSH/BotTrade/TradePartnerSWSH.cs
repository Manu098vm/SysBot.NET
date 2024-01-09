using PKHeX.Core;

namespace SysBot.Pokemon;

public sealed class TradePartnerSWSH((uint TID7, uint SID7) ID, string ot, GameVersion version, LanguageID language, Gender gender) : ITradePartner
{
    public uint TID7 { get; } = ID.TID7;
    public uint SID7 { get; } = ID.SID7;
    public string OT { get; } = ot;
    public int Game { get; } = (byte)version;
    public int Language { get; } = (byte)language;
    public int Gender { get; } = (byte)gender;
}
