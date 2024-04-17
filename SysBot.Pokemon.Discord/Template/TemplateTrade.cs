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
        // 获取species信息
        string specieInfo = this.pkmString.Species;
        // 获取holditem信息
        string Shiny = this.pkmString.Shiny;
        // 获取Gender信息
        string Gender = this.pkmString.Gender.Replace("(F)","♀").Replace("(M)","♂");
        // 获取Mark信息
        string Mark, markEntryText;
        (Mark, markEntryText) = this.pkmString.Mark;

        // 构建信息
        string FiledName = $"{Shiny}{specieInfo}{Gender}{markEntryText}";
        string FiledValue = $"** **";

        
        embed.AddField(FiledName, FiledValue, false);
    }
    private void SetFiled2(EmbedBuilder embed)
    {

        // 获取holditem信息
        string heldItem = this.pkmString.holdItem;
        if (heldItem == "")
            return ;

        string FiledName = $"**Item Held**:{heldItem}";
        string FiledValue = "** **";

        embed.AddField(FiledName, FiledValue, false);
    }
    private void SetFiled3_1(EmbedBuilder embed)
    {
        // 获取teraType信息
        string teraType = this.pkmString.TeraType;
        // 定义Level信息
        int Level = pkm.CurrentLevel;
        // 定义Ability信息
        string Ability = this.pkmString.Ability;
        // 获取Nature信息
        string Nature = this.pkmString.Nature;
        // 获取Scale信息
        string Scale = this.pkmString.Scale;
        // 获取Mark信息
        string Mark, markEntryText;
        (Mark, markEntryText) = this.pkmString.Mark;

        // 构建信息 
        var trademessage = "";
        trademessage += pkm.Generation == 9 ? $"**TeraType:** {teraType}\n" : "";
        trademessage += $"**Level:** {Level}\n";
        trademessage += $"**Ability:** {Ability}\n";
        trademessage += $"**Nature:**{Nature}\n";
        trademessage += $"**Scale:**{Scale}\n";
        trademessage += Mark!="" ? $"**Mark:**{Mark}\n" : "";
                
        // 构建信息
        string FiledName = $"Pokémon Info";
        string FiledValue = $"{trademessage}";

        embed.AddField(FiledName, FiledValue, true);
    }

    private void SetFiled3_2(EmbedBuilder embed)
    {                
        string Moveset = "";
        for (int i = 0; i < this.pkmString.Moves.Count; i++)
        {
            // 获取Move名称
            string moveString = this.pkmString.Moves[i];
            // 获取MovePP
            int movePP = i == 0 ? pkm.Move1_PP : i == 1 ? pkm.Move2_PP : i == 2 ? pkm.Move3_PP : pkm.Move4_PP;
            // 设置moveEmoji
            string moveEmoji = Hub.Config.Discord.EmbedSetting.UseMoveEmoji ? this.pkmString.MovesEmoji[i] : "";
            // 生成Move信息
            Moveset += $"- {moveEmoji}{moveString}({movePP}PP)\n";
        }

        string FiledName = $"Moveset";
        string FiledValue = Moveset;

        embed.AddField(FiledName, FiledValue, true);
    }

    private void SetFiled4_1(EmbedBuilder embed)
    {            
        string IVs = "";
        IVs += $"- {this.pkmString.IVs[0]}HP\n";
        IVs += $"- {this.pkmString.IVs[1]}ATK\n";
        IVs += $"- {this.pkmString.IVs[2]}DEF\n";
        IVs += $"- {this.pkmString.IVs[3]}SPA\n";
        IVs += $"- {this.pkmString.IVs[4]}SPD\n";
        IVs += $"- {this.pkmString.IVs[5]}SPE\n";

        string FiledName = $"Pokémon IVs:";
        string FiledValue = IVs;

        embed.AddField(FiledName, FiledValue, true);        
            
    }
    private void SetFiled4_2(EmbedBuilder embed)
    {            
        string EVs = "";
        EVs += $"- {pkm.EV_HP}HP\n";
        EVs += $"- {pkm.EV_ATK}ATK\n";
        EVs += $"- {pkm.EV_DEF}DEF\n";
        EVs += $"- {pkm.EV_SPA}SPA\n";
        EVs += $"- {pkm.EV_SPD}SPD\n";
        EVs += $"- {pkm.EV_SPE}SPE\n";

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
        // 构建discord的Embed
        var embed = new EmbedBuilder { 
            Color = this.SetColor(), 
            Author = this.SetAuthor(), 
            Footer = this.SetFooter(), 
            ThumbnailUrl = this.SetThumbnailUrl(),
            };

        // 构建Embed中的Filed        
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
