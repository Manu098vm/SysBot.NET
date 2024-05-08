using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;


public class TemplateTrade<T> where T : PKM, new()
{
    private PKM pkm;
    private PKMString<T> pkmString;
    private SocketCommandContext Context;
    private PokeTradeHub<T> Hub;

    public TemplateTrade(PKM pkm, SocketCommandContext Context, PokeTradeHub<T> Hub)
    {
        this.pkm = pkm;
        this.pkmString = new PKMString<T>(pkm, Hub);
        this.Context = Context;
        this.Hub = Hub;
    }

    private Color SetColor()
    {
        return pkm.IsShiny && pkm.ShinyXor == 0 ? Color.Gold : pkm.IsShiny ? Color.LighterGrey : Color.Teal;
    }
    
    private EmbedAuthorBuilder SetAuthor()
    {
        EmbedAuthorBuilder author = new EmbedAuthorBuilder
        {
            Name = $"{Context.User.Username}'s Pokémon",
            Url = "https://discord.gg/rBTgvnYTNT",
            IconUrl = pkmString.ballImg
        };
        return author;
    }
    private string SetThumbnailUrl()
    {
        return pkmString.pokeImg;
    }

    private EmbedFooterBuilder SetFooter()
    {
        string TIDFormatted = pkm.Generation >= 7 ? $"{pkm.TrainerTID7:000000}" : $"{pkm.TID16:00000}";
        return  new EmbedFooterBuilder { Text = $"Trainer Info: {pkm.OriginalTrainerName}/{TIDFormatted}" };
    }

    private void SetFiled1(EmbedBuilder embed)
    {
        // Obtain species's info
        string specieInfo = this.pkmString.Species;
        // Obtain holditem's info
        string Shiny = this.pkmString.Shiny;
        // Obtain Gender's info
        string Gender = this.pkmString.Gender.Replace("(F)", "♀️").Replace("(M)", "♂️");
        // Obtain Mark's info
        string Mark, markEntryText;
        (Mark, markEntryText) = this.pkmString.Mark;

        // Build info
        string FiledName = $"{Shiny}{specieInfo}{Gender}{markEntryText}";
        string FiledValue = $"** **";

        
        embed.AddField(FiledName, FiledValue, false);
    }
    private void SetFiled2(EmbedBuilder embed)
    {

        // Obtain holditem's info
        string heldItem = this.pkmString.holdItem;
        if (heldItem == "")
            return ;

        string FiledName = $"**Item Held**: {heldItem}";
        string FiledValue = "** **";

        embed.AddField(FiledName, FiledValue, false);
    }
    private void SetFiled3_1(EmbedBuilder embed)
    {
        // Obtain teraType's info
        string teraType = this.pkmString.TeraType;
        // Define Level's info
        int Level = pkm.CurrentLevel;
        // Define Ability's info
        string Ability = this.pkmString.Ability;
        // Obtain Nature's Nature
        string Nature = this.pkmString.Nature;
        // Obtain Scale's info
        string Scale = this.pkmString.Scale;
        // Obtain Mark's info
        string Mark, markEntryText;
        (Mark, markEntryText) = this.pkmString.Mark;

        // Build info 
        var trademessage = "";
        trademessage += pkm.Generation == 9 ? $"**TeraType:** {teraType}\n" : "";
        trademessage += $"**Level:** {Level}\n";
        trademessage += $"**Ability:** {Ability}\n";
        trademessage += $"**Nature:** {Nature}\n";
        trademessage += $"**Scale:** {Scale}\n";
        trademessage += Mark!="" ? $"**Mark:** {Mark}\n" : "";
                
        // Build info
        string FiledName = $"Pokémon Stats:";
        string FiledValue = $"{trademessage}";

        embed.AddField(FiledName, FiledValue, true);
    }

    private void SetFiled3_2(EmbedBuilder embed)
    {                
        string Moveset = "";
        for (int i = 0; i < this.pkmString.Moves.Count; i++)
        {
            // Obtain Moveset
            string moveString = this.pkmString.Moves[i];
            // Obtain MovePP
            int movePP = i == 0 ? pkm.Move1_PP : i == 1 ? pkm.Move2_PP : i == 2 ? pkm.Move3_PP : pkm.Move4_PP;
            // Setup moveEmoji
            string moveEmoji = Hub.Config.Discord.EmbedSetting.UseMoveEmoji ? this.pkmString.MovesEmoji[i] : "";
            // Generate Moveset's info
            Moveset += $"- {moveEmoji}{moveString} ({movePP}PP)\n";
        }

        string FiledName = $"Moveset:";
        string FiledValue = Moveset;

        embed.AddField(FiledName, FiledValue, true);
    }

    private void SetFiled4_1(EmbedBuilder embed)
    {            
        string IVs = "";
        IVs += $"- {this.pkmString.IVs[0]} HP\n";
        IVs += $"- {this.pkmString.IVs[1]} ATK\n";
        IVs += $"- {this.pkmString.IVs[2]} DEF\n";
        IVs += $"- {this.pkmString.IVs[3]} SPA\n";
        IVs += $"- {this.pkmString.IVs[4]} SPD\n";
        IVs += $"- {this.pkmString.IVs[5]} SPE\n";

        string FiledName = $"Pokémon IVs:";
        string FiledValue = IVs;

        embed.AddField(FiledName, FiledValue, true);        
            
    }
    private void SetFiled4_2(EmbedBuilder embed)
    {            
        string EVs = "";
        EVs += $"- {pkm.EV_HP} HP\n";
        EVs += $"- {pkm.EV_ATK} ATK\n";
        EVs += $"- {pkm.EV_DEF} DEF\n";
        EVs += $"- {pkm.EV_SPA} SPA\n";
        EVs += $"- {pkm.EV_SPD} SPD\n";
        EVs += $"- {pkm.EV_SPE} SPE\n";

        string FiledName = $"Pokémon EVs:";
        string FiledValue = EVs;
            
        embed.AddField(FiledName, FiledValue, true);
    }
    private void SetFiledTemp(EmbedBuilder embed)
    {                
        embed.AddField($"** **", $"** **", true);
    }
   
    public EmbedBuilder Generate()
    {   
        // Build discord Embed
        var embed = new EmbedBuilder { 
            Color = this.SetColor(), 
            Author = this.SetAuthor(), 
            Footer = this.SetFooter(), 
            ThumbnailUrl = this.SetThumbnailUrl(),
            };

        // Build Embed's Files        
        this.SetFiled1(embed);
        this.SetFiled2(embed);
        this.SetFiled3_1(embed);
        this.SetFiledTemp(embed);
        this.SetFiled3_2(embed);
        this.SetFiled4_1(embed);
        this.SetFiledTemp(embed);
        this.SetFiled4_2(embed);

        return embed;
    }
}
