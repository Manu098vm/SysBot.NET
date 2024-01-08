using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PKHeX.Core;
using PKHeX.Core.AutoMod;

namespace SysBot.Pokemon;

public class TradeExtensions<T> where T : PKM, new()
{
    public static void EggTrade(PKM pk, IBattleTemplate template)
    {
        pk.IsNicknamed = true;
        pk.Nickname = pk.Language switch
        {
            1 => "タマゴ",
            3 => "Œuf",
            4 => "Uovo",
            5 => "Ei",
            7 => "Huevo",
            8 => "알",
            9 or 10 => "蛋",
            _ => "Egg",
        };

        pk.IsEgg = true;
        pk.Egg_Location = pk switch
        {
            PB8 => 60010,
            PK9 => 30023,
            _ => 60002, //PK8
        };

        pk.MetDate = DateOnly.FromDateTime(DateTime.Today);
        pk.EggMetDate = pk.MetDate;
        pk.HeldItem = 0;
        pk.CurrentLevel = 1;
        pk.EXP = 0;
        pk.Met_Level = 1;
        pk.Met_Location = pk switch
        {
            PB8 => 65535,
            PK9 => 0,
            _ => 30002, //PK8
        };

        pk.CurrentHandler = 0;
        pk.OT_Friendship = 1;
        pk.HT_Name = "";
        pk.HT_Friendship = 0;
        pk.ClearMemories();
        pk.StatNature = pk.Nature;
        pk.SetEVs(new int[] { 0, 0, 0, 0, 0, 0 });

        pk.SetMarking(0, 0);
        pk.SetMarking(1, 0);
        pk.SetMarking(2, 0);
        pk.SetMarking(3, 0);
        pk.SetMarking(4, 0);
        pk.SetMarking(5, 0);

        pk.ClearRelearnMoves();

        if (pk is PK8 pk8)
        {
            pk8.HT_Language = 0;
            pk8.HT_Gender = 0;
            pk8.HT_Memory = 0;
            pk8.HT_Feeling = 0;
            pk8.HT_Intensity = 0;
            pk8.DynamaxLevel = pk8.GetSuggestedDynamaxLevel(pk8, 0);
        }
        else if (pk is PB8 pb8)
        {
            pb8.HT_Language = 0;
            pb8.HT_Gender = 0;
            pb8.HT_Memory = 0;
            pb8.HT_Feeling = 0;
            pb8.HT_Intensity = 0;
            pb8.DynamaxLevel = pb8.GetSuggestedDynamaxLevel(pb8, 0);
        }
        else if (pk is PK9 pk9)
        {
            pk9.HT_Language = 0;
            pk9.HT_Gender = 0;
            pk9.HT_Memory = 0;
            pk9.HT_Feeling = 0;
            pk9.HT_Intensity = 0;
            pk9.Obedience_Level = 1;
            pk9.Version = 0;
            pk9.BattleVersion = 0;
            pk9.TeraTypeOverride = (MoveType)19;
        }

        pk = TrashBytes(pk);
        var la = new LegalityAnalysis(pk);
        var enc = la.EncounterMatch;
        pk.SetSuggestedRibbons(template, enc, true);
        pk.SetSuggestedMoves();
        la = new LegalityAnalysis(pk);
        enc = la.EncounterMatch;
        pk.CurrentFriendship = enc is IHatchCycle h ? h.EggCycles : pk.PersonalInfo.HatchCycles;

        Span<ushort> relearn = stackalloc ushort[4];
        la.GetSuggestedRelearnMoves(relearn, enc);
        pk.SetRelearnMoves(relearn);

        pk.Move1_PPUps = pk.Move2_PPUps = pk.Move3_PPUps = pk.Move4_PPUps = 0;
        pk.SetMaximumPPCurrent(pk.Moves);
        pk.SetSuggestedHyperTrainingData();
    }

    public static PKM TrashBytes(PKM pkm, LegalityAnalysis? la = null)
    {
        var pkMet = (T)pkm.Clone();
        if (pkMet.Version is not (int)GameVersion.GO)
            pkMet.MetDate = DateOnly.FromDateTime(DateTime.Now);

        var analysis = new LegalityAnalysis(pkMet);
        var pkTrash = (T)pkMet.Clone();
        if (analysis.Valid)
        {
            pkTrash.IsNicknamed = true;
            pkTrash.Nickname = "MANUMANUMANU";
            pkTrash.SetDefaultNickname(la ?? new LegalityAnalysis(pkTrash));
        }

        if (new LegalityAnalysis(pkTrash).Valid)
            pkm = pkTrash;
        else if (analysis.Valid)
            pkm = pkMet;
        return pkm;
    }
}
