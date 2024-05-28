using Discord;
using Discord.Commands;
using PKHeX.Core;

namespace SysBot.Pokemon.Discord;

public class TemplateTrade<T>(PKM pkm, SocketCommandContext Context, PokeTradeHub<T> Hub) where T : PKM, new()
{
    private readonly PKM pkm = pkm;
    private readonly PKMString<T> pkmString = new(pkm, Hub);
    private readonly SocketCommandContext Context = Context;
    private readonly PokeTradeHub<T> Hub = Hub;

    private Color SetColor()
    {
        return pkm.IsShiny && pkm.ShinyXor == 0 ? Color.Gold : pkm.IsShiny ? Color.LighterGrey : Color.Teal;
    }
    
    private EmbedAuthorBuilder SetAuthor()
    {
        EmbedAuthorBuilder author = new()
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

    private EmbedFooterBuilder SetFooter(int positionNum = 1, string etaMessage = "")
    {
        // Current queue position
        string Position = $"Current Position:{positionNum}";
        // Trainer info
        string Trainer = $"OT:{pkm.OriginalTrainerName} | TID:{pkm.DisplayTID} | SID:{pkm.DisplaySID}";

        // display combined footer content
        string FooterContent = "";
        FooterContent += $"\n{Position}";
        FooterContent += $"\n{Trainer}";
        FooterContent += $"\n{etaMessage}";

        return new EmbedFooterBuilder { Text = FooterContent };
    }

    private void SetFiled1(EmbedBuilder embed)
    {
        // Obtain species's info
        string speciesInfo = pkmString.Species;
        // Obtain holditem's info
        string shiny = pkmString.Shiny;
        // Obtain Gender's info
        string gender = pkmString.Gender.Replace("(F)", "♀️").Replace("(M)", "♂️");
        // Obtain Mark's info
        (_, string markEntryText) = pkmString.Mark;

        // Build info
        string filedName = $"{shiny}{speciesInfo}{gender}{markEntryText}";
        string filedValue = $"** **";

        embed.AddField(filedName, filedValue, false);
    }

    private void SetFiled2(EmbedBuilder embed)
    {
        // Obtain holditem's info
        string heldItem = pkmString.holdItem;
        if (heldItem == "")
            return ;

        string filedName = $"**Item Held**: {heldItem}";
        string filedValue = "** **";

        embed.AddField(filedName, filedValue, false);
    }

    private void SetFiled3_1(EmbedBuilder embed)
    {
        // Obtain teraType's info
        string teraType = pkmString.TeraType;
        // Define Level's info
        int level = pkm.CurrentLevel;
        // Define Ability's info
        string ability = pkmString.Ability;
        // Obtain Nature's Nature
        string nature = pkmString.Nature;
        // Obtain Scale's info
        string scale = pkmString.Scale;
        // Obtain Mark's info
        (string mark, _) = pkmString.Mark;

        // Build info 
        var trademessage = "";
        trademessage += pkm.Generation == 9 ? $"**TeraType:** {teraType}\n" : "";
        trademessage += $"**Level:** {level}\n";
        trademessage += $"**Ability:** {ability}\n";
        trademessage += $"**Nature:** {nature}\n";
        trademessage += $"**Scale:** {scale}\n";
        trademessage += mark!="" ? $"**Mark:** {mark}\n" : "";
                
        // Build info
        string filedName = $"Pokémon Stats:";
        string filedValue = $"{trademessage}";

        embed.AddField(filedName, filedValue, true);
    }

    private void SetFiled3_2(EmbedBuilder embed)
    {                
        string moveset = "";
        for (int i = 0; i < pkmString.Moves.Count; i++)
        {
            // Obtain Moveset
            string moveString = pkmString.Moves[i];
            // Obtain MovePP
            int movePP = i == 0 ? pkm.Move1_PP : i == 1 ? pkm.Move2_PP : i == 2 ? pkm.Move3_PP : pkm.Move4_PP;
            // Setup moveEmoji
            string moveEmoji = Hub.Config.Discord.EmbedSetting.UseMoveEmoji ? pkmString.MovesEmoji[i] : "";
            // Generate Moveset's info
            moveset += $"- {moveEmoji}{moveString} ({movePP}PP)\n";
        }

        string FiledName = $"Moveset:";
        string FiledValue = moveset;

        embed.AddField(FiledName, FiledValue, true);
    }

    private void SetFiled4_1(EmbedBuilder embed)
    {            
        string IVs = "";
        IVs += $"- {pkmString.IVs[0]} HP\n";
        IVs += $"- {pkmString.IVs[1]} ATK\n";
        IVs += $"- {pkmString.IVs[2]} DEF\n";
        IVs += $"- {pkmString.IVs[3]} SPA\n";
        IVs += $"- {pkmString.IVs[4]} SPD\n";
        IVs += $"- {pkmString.IVs[5]} SPE\n";

        string filedName = $"Pokémon IVs:";
        string filedValue = IVs;

        embed.AddField(filedName, filedValue, true);        
            
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

        string filedName = $"Pokémon EVs:";
        string filedValue = EVs;
            
        embed.AddField(filedName, filedValue, true);
    }

    private static void SetFiledTemp(EmbedBuilder embed)
    {                
        embed.AddField($"** **", $"** **", true);
    }
   
    public EmbedBuilder Generate(int positionNum = 1, string etaMessage = "")
    {   
        // Build discord Embed
        var embed = new EmbedBuilder { 
            Color = SetColor(), 
            Author = SetAuthor(), 
            Footer = SetFooter(), 
            ThumbnailUrl = SetThumbnailUrl(),
            };

        // Build embed files        
        SetFiled1(embed);
        SetFiled2(embed);
        SetFiled3_1(embed);
        SetFiledTemp(embed);
        SetFiled3_2(embed);
        SetFiled4_1(embed);
        SetFiledTemp(embed);
        SetFiled4_2(embed);

        return embed;
    }
}
