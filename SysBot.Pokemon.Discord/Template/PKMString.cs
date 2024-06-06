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
    // Default language
    private PKM pkm;
    private int PKMLanguage => pkm.Language;
    private int GameLanguage => PKMLanguage < 6 ? PKMLanguage - 1 : PKMLanguage == 6 || PKMLanguage == 7 ? 0 : PKMLanguage > 7 ? PKMLanguage - 2 : 0;
    private GameStrings Strings => GameInfo.GetStrings(GameLanguage);

    // Data panel
    private PokeTradeHub<T> Hub;

    // Definition of the message's properties
    public ShowdownSet set => new ShowdownSet($"{pkm.Species}");
    public string Species => this.GetSpecies(pkm);
    public string Shiny => this.GetShiny(pkm);
    public string Gender => this.GetGender(pkm);
    public List<string> Moves => this.GetMoves(pkm);
    public List<string> MovesEmoji => this.GetMovesEmoji(pkm);
    public string Scale => this.GetScale(pkm);
    public string Ability => this.GetAbility(pkm);
    public string Nature => this.GetNature(pkm);
    public string TeraType => this.GetTeraType(pkm);
    public string TeraTypeEmoji => this.GetTeraTypePic(pkm);
    public string holdItem => this.GetHoldItem(pkm);
    public (string,string) Mark => this.GetMark(pkm);
    public List<int> IVs => this.GetIvs(pkm);
    public string ballImg => this.GetBallImg(pkm);
    public string pokeImg => this.GetPokeImg(pkm);
    
    // Definition of message's info/method
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
        return $"{ this.Strings.Natures[(int)pkm.Nature] }";
    }
    private string GetTeraType(PKM pkm)
    {
        if (pkm.Generation == 9)
        {
            var pk = (PK9)pkm;
            var teratype = pk.TeraType <= MoveType.Fairy ? (byte)pk.TeraType : (byte)18;
            return $"{Strings.types[teratype]}";
        }

        return "";
    }
    private string GetTeraTypePic(PKM pkm)
    {
        if (pkm.Generation == 9)
        {
            var pk = (PK9)pkm;
            int TypeValue = (int)pk.TeraType;
            var linq = Hub.Config.Discord.EmbedSetting.MoveEmojiConfigs.Where(z => (z.MoveTypeValue == TypeValue)).Select(z => z.EmojiCode);
            string moveEmoji = linq.ToList()[0] != "" ? $"<:MoveEmoji:{linq.ToList()[0]}> " : "";
            return moveEmoji;
        }

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

    private List<string> GetMovesEmoji(PKM pkm)
    {
        List<string> MovesEmoji = new();
        
        for (int moveIndex = 0; moveIndex < pkm.Moves.Length; moveIndex++)
        {
            int moveTypeValue = MoveInfo.GetType(pkm.Moves[moveIndex], default);
            var linq =  Hub.Config.Discord.EmbedSetting.MoveEmojiConfigs.Where( z => (z.MoveTypeValue == moveTypeValue) ).Select( z => z.EmojiCode );
            string moveEmoji = linq.ToList()[0] != "" ? $"<:MoveEmoji:{linq.ToList()[0]}> " : "";
            MovesEmoji.Add( moveEmoji );
        }
            
        
        return MovesEmoji;
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
        string specieName = $"{SpeciesName.GetSpeciesNameGeneration(pkm.Species, pkm.Language, (byte)(pkm.Generation <= 8 ? 8 : 9))}";
        string specieForm = TradeExtensions<T>.FormOutput(pkm.Species, pkm.Form, out _);
        string specieInfo = $"{specieName}{specieForm}";
        return specieInfo;
    }

    private string GetHoldItem(PKM pkm)
    {
        string holdItem = pkm.HeldItem != 0 ? this.Strings.Item[pkm.HeldItem]: "";
        
        return holdItem;
    }
    

    // Build function
    public PKMString(PKM pkm, PokeTradeHub<T> Hub)
    {
        this.pkm = pkm;
        this.Hub = Hub;
    }

}
