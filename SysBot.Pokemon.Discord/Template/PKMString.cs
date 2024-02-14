using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;


public class PKMString<T> where T : PKM, new()
{
    // 默认语言
    private PKM pkm;
    private int PKMLanguage => pkm.Language;
    private int GameLanguage => PKMLanguage < 6 ? PKMLanguage - 1 : PKMLanguage == 6 || PKMLanguage == 7 ? 0 : PKMLanguage > 7 ? PKMLanguage - 2 : 0;
    private GameStrings Strings => GameInfo.GetStrings(GameLanguage);

    // 定义信息的属性
    public ShowdownSet set => new ShowdownSet($"{pkm.Species}");
    public string Species => this.GetSpecies(pkm);
    public string Shiny => this.GetShiny(pkm);
    public string Gender => this.GetGender(pkm);
    public List<string> Moves => this.GetMoves(pkm);

    public string Scale => this.GetScale(pkm);
    public string Ability => this.GetAbility(pkm);
    public string Nature => this.GetNature(pkm);
    public string TeraType => this.GetTeraType(pkm);
    public string holdItem => this.GetHoldItem(pkm);
    public (string,string) Mark => this.GetMark(pkm);
    public List<int> IVs => this.GetIvs(pkm);
    public string ballImg => this.GetBallImg(pkm);
    public string pokeImg => this.GetPokeImg(pkm);
    
    // 定义信息的方法
    private string GetShiny(PKM pkm)
    {
        return pkm.ShinyXor == 0 ? "■" : pkm.ShinyXor <= 16 ? "★" : "";
    }
    private string GetGender(PKM pkm)
    {
        return pkm.Gender == 0 ? " - (M)" : pkm.Gender == 1 ? " - (F)" : "";
    }
    private string GetAbility(PKM pkm)
    {
        return $"{this.Strings.Ability[pkm.Ability]}";
    }
    private string GetNature(PKM pkm)
    {
        return $"{ this.Strings.Natures[pkm.Nature] }";
    }
    private string GetTeraType(PKM pkm)
    {
        if (pkm.Generation == 9)
            return $"{ this.Strings.types[(byte)((PK9)pkm).TeraType] }";
        else
            return "";
    }
    private string GetBallImg(PKM pkm)
    {
        return $"https://raw.githubusercontent.com/BakaKaito/HomeImages/main/Ballimg/50x50/" + $"{(Ball)pkm.Ball}ball".ToLower() + ".png";
    }
    private string GetPokeImg(PKM pkm)
    {
        return TradeExtensions<T>.PokeImg(pkm, false, false);
    }
    private List<int> GetIvs(PKM pkm)
    {
        return new() {pkm.IV_HP, pkm.IV_ATK, pkm.IV_DEF, pkm.IV_SPA, pkm.IV_SPD, pkm.IV_SPE};
    }

    
    private List<string> GetMoves(PKM pkm)
    {
        List<string> Moves = new();
        
        for (int moveIndex = 0; moveIndex < pkm.Moves.Length; moveIndex++)
            Moves.Add(Strings.Move[ pkm.Moves[moveIndex] ]);
        
        return Moves;
    }
    
    private string GetScale(PKM pkm)
    {
        string scale = "";

        if (pkm is PK9 fin9)
            scale = $"{PokeSizeDetailedUtil.GetSizeRating(fin9.Scale)} ({fin9.Scale})";
        if (pkm is PA8 fin8a)
            scale = $"{PokeSizeDetailedUtil.GetSizeRating(fin8a.Scale)} ({fin8a.Scale})";
        if (pkm is PB8 fin8b)
            scale = $"{PokeSizeDetailedUtil.GetSizeRating(fin8b.HeightScalar)} ({fin8b.HeightScalar})";
        if (pkm is PK8 fin8)
            scale = $"{PokeSizeDetailedUtil.GetSizeRating(fin8.HeightScalar)} ({fin8.HeightScalar})";

        return scale;
    }

    private (string,string) GetMark(PKM pkm)
    {
        string Mark = (TradeExtensions<T>.HasMark((IRibbonIndex)pkm, out RibbonIndex mark) ? $"\nPokémon Mark: {mark.ToString().Replace("Mark", "")}{Environment.NewLine}" : "");

        string markEntryText = "";
        var index = (int)mark - (int)RibbonIndex.MarkLunchtime;
        if (index > 0)
            markEntryText = TradeExtensions<T>.MarkTitle[index];
        
        return (Mark, markEntryText);
    }

    private string GetSpecies(PKM pkm)
    {
        string specieName = $"{SpeciesName.GetSpeciesNameGeneration(pkm.Species, pkm.Language, pkm.Generation <= 8 ? 8 : 9)}";
        string specieForm = TradeExtensions<T>.FormOutput(pkm.Species, pkm.Form, out _);
        string specieInfo = $"{specieName}{specieForm}";
        return specieInfo;
    }

    private string GetHoldItem(PKM pkm)
    {
        string holdItem = pkm.HeldItem != 0 ? this.Strings.Item[pkm.HeldItem]: "";
        
        return holdItem;
    }
    

    // 构建函数
    public PKMString(PKM pkm)
    {
        this.pkm = pkm;
    }

}
