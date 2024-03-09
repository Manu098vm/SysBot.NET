using System;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using SysBot.Base;

namespace SysBot.Pokemon;

public interface ITradePartner
{
    public uint TID7 { get; }
    public uint SID7 { get; }
    public string OT { get; }
    public int Game { get; }
    public int Gender { get; }
    public int Language { get; }
}

public class TradeExtensions<T> where T : PKM, new()
{
    public static bool CanUsePartnerDetails(RoutineExecutor<PokeBotState> executor, T pk, SaveFile sav, ITradePartner partner, PokeTradeDetail<T> trade, PokeTradeHubConfig config, out T res)
    {
        void Log(string msg) => executor.Log(msg);

        res = (T)pk.Clone();

        //Invalid trade request. Ditto is often requested for Masuda method, better to not apply partner details.
        if ((Species)pk.Species is Species.None or Species.Ditto || trade.Type is not PokeTradeType.Specific)
        {
            Log("Can not apply Partner details: Not a specific trade request.");
            return false;
        }

        //Current handler cannot be past gen OT
        if (!pk.IsNative && !config.Legality.ForceTradePartnerInfo)
        {
            Log("Can not apply Partner details: Current handler cannot be different gen OT.");
            return false;
        }

        //Only override trainer details if user didn't specify OT details in the Showdown/PK9 request
        if (HasSetDetails(config, pk, sav))
        {
            Log("Can not apply Partner details: Requested Pokémon already has set Trainer details.");
            return false;
        }

        res.OriginalTrainerName = partner.OT;
        res.OriginalTrainerGender = (byte)partner.Gender;
        res.TrainerTID7 = partner.TID7;
        res.TrainerSID7 = partner.SID7;
        res.Language = partner.Language;
        res.Version = (GameVersion)partner.Game;

        if (!pk.IsNicknamed)
            res.ClearNickname();

        if (pk.IsShiny)
            res.PID = (uint)((res.TID16 ^ res.SID16 ^ (res.PID & 0xFFFF) ^ pk.ShinyXor) << 16) | (res.PID & 0xFFFF);

        if (!pk.ChecksumValid)
            res.RefreshChecksum();

        var la = new LegalityAnalysis(res);
        if (!la.Valid)
        {
            res.Version = pk.Version;

            if (!pk.ChecksumValid)
                res.RefreshChecksum();

            la = new LegalityAnalysis(res);

            if (!la.Valid)
            {
                if (!config.Legality.ForceTradePartnerInfo)
                {
                    Log("Can not apply Partner details:");
                    Log(la.Report());
                    return false;
                }

                Log("Trying to force Trade Partner Info discarding the game version...");
                res.Version = pk.Version;

                if (!pk.ChecksumValid)
                    res.RefreshChecksum();

                la = new LegalityAnalysis(res);
                if (!la.Valid)
                {
                    Log("Can not apply Partner details:");
                    Log(la.Report());
                    return false;
                }
            }
        }

        Log($"Applying trade partner details: {partner.OT} ({(partner.Gender == 0 ? "M" : "F")}), " +
                $"TID: {partner.TID7:000000}, SID: {partner.SID7:0000}, {(LanguageID)partner.Language} ({(GameVersion)res.Version})");

        return true;
    }

    private static bool HasSetDetails(PokeTradeHubConfig config, PKM set, ITrainerInfo fallback)
    {
        var set_trainer = new SimpleTrainerInfo((GameVersion)set.Version)
        {
            OT = set.OriginalTrainerName,
            TID16 = set.TID16,
            SID16 = set.SID16,
            Gender = set.OriginalTrainerGender,
            Language = set.Language,
        };

        var def_trainer = new SimpleTrainerInfo((GameVersion)fallback.Version)
        {
            OT = config.Legality.GenerateOT,
            TID16 = config.Legality.GenerateTID16,
            SID16 = config.Legality.GenerateSID16,
            Gender = config.Legality.GenerateGenderOT,
            Language = (int)config.Legality.GenerateLanguage,
        };

        var alm_trainer = config.Legality.GeneratePathTrainerInfo != string.Empty ?
            TrainerSettings.GetSavedTrainerData(fallback.Generation, (GameVersion)fallback.Version, fallback, (LanguageID)fallback.Language) : null;

        return !IsEqualTInfo(set_trainer, def_trainer) && !IsEqualTInfo(set_trainer, alm_trainer);
    }

    private static bool IsEqualTInfo(ITrainerInfo trainerInfo, ITrainerInfo? compareInfo)
    {
        if (compareInfo is null)
            return false;

        if (!trainerInfo.OT.Equals(compareInfo.OT))
            return false;

        if (trainerInfo.Gender != compareInfo.Gender)
            return false;

        if (trainerInfo.Language != compareInfo.Language)
            return false;

        if (trainerInfo.TID16 != compareInfo.TID16)
            return false;

        if (trainerInfo.SID16 != compareInfo.SID16)
            return false;

        return true;
    }

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
        pk.EggLocation = pk switch
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
        pk.MetLevel = 1;
        pk.MetLocation = pk switch
        {
            PB8 => 65535,
            PK9 => 0,
            _ => 30002, //PK8
        };

        pk.CurrentHandler = 0;
        pk.OriginalTrainerFriendship = 1;
        pk.HandlingTrainerName = "";
        pk.HandlingTrainerFriendship = 0;
        pk.ClearMemories();
        pk.StatNature = pk.Nature;
        pk.SetEVs(new int[] { 0, 0, 0, 0, 0, 0 });

        MarkingApplicator.SetMarkings(pk);
        pk.ClearRelearnMoves();

        if (pk is PK8 pk8)
        {
            pk8.HandlingTrainerLanguage = 0;
            pk8.HandlingTrainerGender = 0;
            pk8.HandlingTrainerMemory = 0;
            pk8.HandlingTrainerLanguage = 0;
            pk8.HandlingTrainerMemoryIntensity = 0;
            pk8.DynamaxLevel = pk8.GetSuggestedDynamaxLevel(pk8, 0);
        }
        else if (pk is PB8 pb8)
        {
            pb8.HandlingTrainerLanguage = 0;
            pb8.HandlingTrainerGender = 0;
            pb8.HandlingTrainerMemory = 0;
            pb8.HandlingTrainerLanguage = 0;
            pb8.HandlingTrainerMemoryIntensity = 0;
            pb8.DynamaxLevel = pb8.GetSuggestedDynamaxLevel(pb8, 0);
        }
        else if (pk is PK9 pk9)
        {
            pk9.HandlingTrainerLanguage = 0;
            pk9.HandlingTrainerGender = 0;
            pk9.HandlingTrainerMemory = 0;
            pk9.HandlingTrainerLanguage = 0;
            pk9.HandlingTrainerMemoryIntensity = 0;
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
        if (pkMet.Version is not GameVersion.GO)
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

    public static string PokeImg(PKM pkm, bool canGmax, bool fullSize)
    {
        bool md = false;
        bool fd = false;
        string[] baseLink;
        if (fullSize)
            baseLink = "https://raw.githubusercontent.com/zyro670/HomeImages/master/512x512/poke_capture_0001_000_mf_n_00000000_f_n.png".Split('_');
        else baseLink = "https://raw.githubusercontent.com/zyro670/HomeImages/master/128x128/poke_capture_0001_000_mf_n_00000000_f_n.png".Split('_');

        if (Enum.IsDefined(typeof(GenderDependent), pkm.Species) && !canGmax && pkm.Form is 0)
        {
            if (pkm.Gender == 0 && pkm.Species != (int)Species.Torchic)
                md = true;
            else fd = true;
        }

        int form = pkm.Species switch
        {
            (int)Species.Sinistea or (int)Species.Polteageist or (int)Species.Rockruff or (int)Species.Mothim => 0,
            (int)Species.Alcremie when pkm.IsShiny || canGmax => 0,
            _ => pkm.Form,

        };

        if (pkm.Species is (ushort)Species.Sneasel)
        {
            if (pkm.Gender is 0)
                md = true;
            else fd = true;
        }

        if (pkm.Species is (ushort)Species.Basculegion)
        {
            if (pkm.Gender is 0)
            {
                md = true;
                pkm.Form = 0;
            }
            else
            {
                pkm.Form = 1;
            }

            string s = pkm.IsShiny ? "r" : "n";
            string g = md && pkm.Gender is not 1 ? "md" : "fd";
            return $"https://raw.githubusercontent.com/zyro670/HomeImages/master/128x128/poke_capture_0" + $"{pkm.Species}" + "_00" + $"{pkm.Form}" + "_" + $"{g}" + "_n_00000000_f_" + $"{s}" + ".png";
        }

        baseLink[2] = pkm.Species < 10 ? $"000{pkm.Species}" : pkm.Species < 100 && pkm.Species > 9 ? $"00{pkm.Species}" : pkm.Species >= 1000 ? $"{pkm.Species}" : $"0{pkm.Species}";
        baseLink[3] = pkm.Form < 10 ? $"00{form}" : $"0{form}";
        baseLink[4] = pkm.PersonalInfo.OnlyFemale ? "fo" : pkm.PersonalInfo.OnlyMale ? "mo" : pkm.PersonalInfo.Genderless ? "uk" : fd ? "fd" : md ? "md" : "mf";
        baseLink[5] = canGmax ? "g" : "n";
        baseLink[6] = "0000000" + (pkm.Species == (int)Species.Alcremie && !canGmax ? pkm.Data[0xD0] : 0);
        baseLink[8] = pkm.IsShiny ? "r.png" : "n.png";
        return string.Join("_", baseLink);
    }

    public static string FormOutput(ushort species, byte form, out string[] formString)
    {
        var strings = GameInfo.GetStrings("en");
        formString = FormConverter.GetFormList(species, strings.Types, strings.forms, GameInfo.GenderSymbolASCII, typeof(T) == typeof(PK9) ? EntityContext.Gen9 : EntityContext.Gen4);
        if (formString.Length is 0)
            return string.Empty;

        formString[0] = "";
        if (form >= formString.Length)
            form = (byte)(formString.Length - 1);
        return formString[form].Contains('-') ? formString[form] : formString[form] == "" ? "" : $"-{formString[form]}";
    }

    public static bool HasMark(IRibbonIndex pk, out RibbonIndex result)
    {
        result = default;
        for (var mark = RibbonIndex.MarkLunchtime; mark <= RibbonIndex.MarkSlump; mark++)
        {
            if (pk.GetRibbon((int)mark))
            {
                result = mark;
                return true;
            }
        }
        return false;
    }

    public static readonly string[] MarkTitle =
    [
        " the Peckish"," the Sleepy"," the Dozy"," the Early Riser"," the Cloud Watcher"," the Sodden"," the Thunderstruck"," the Snow Frolicker"," the Shivering"," the Parched"," the Sandswept"," the Mist Drifter",
        " the Chosen One"," the Catch of the Day"," the Curry Connoisseur"," the Sociable"," the Recluse"," the Rowdy"," the Spacey"," the Anxious"," the Giddy"," the Radiant"," the Serene"," the Feisty"," the Daydreamer",
        " the Joyful"," the Furious"," the Beaming"," the Teary-Eyed"," the Chipper"," the Grumpy"," the Scholar"," the Rampaging"," the Opportunist"," the Stern"," the Kindhearted"," the Easily Flustered"," the Driven",
        " the Apathetic"," the Arrogant"," the Reluctant"," the Humble"," the Pompous"," the Lively"," the Worn-Out", " of the Distant Past", " the Twinkling Star", " the Paldea Champion", " the Great", " the Teeny", " the Treasure Hunter",
        " the Reliable Partner", " the Gourmet", " the One-in-a-Million", " the Former Alpha", " the Unrivaled", " the Former Titan",
    ];
}
