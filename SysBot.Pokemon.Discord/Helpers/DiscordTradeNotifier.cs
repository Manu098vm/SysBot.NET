using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;


public class DiscordTradeNotifier<T>(T Data, PokeTradeTrainerInfo Info, int Code, SocketUser Trader, SocketCommandContext Context)
    : IPokeTradeNotifier<T>
    where T : PKM, new()
{
    private T Data { get; } = Data;
    private PokeTradeTrainerInfo Info { get; } = Info;
    private int Code { get; } = Code;
    private SocketUser Trader { get; } = Trader;
    private SocketCommandContext Context { get; } = Context;
    public Action<PokeRoutineExecutor<T>>? OnFinish { private get; set; }
    public readonly PokeTradeHub<T> Hub = SysCord<T>.Runner.Hub;

    public string TradeDisplayingInfo(PokeTradeDetail<T> info)
    {
        string msg = "Displaying your ";
        var mode = info.Type;
        switch (mode)
        {
            case PokeTradeType.Specific: msg += "request!"; break;
            case PokeTradeType.Clone: msg += "clone!"; break;
        }
        return msg;
    }

    public void TradeEmbed(PKM pkm, PokeTradeDetail<T> info)
    {
        var template = new TemplateTrade<T>(pkm, Context, Hub);
        EmbedBuilder embed = template.Generate();
        
        // 获取displaying信息
        string msg = TradeDisplayingInfo(info);
        
        Context.Channel.SendMessageAsync(Trader.Username + " - " + msg, embed: embed.Build()).ConfigureAwait(false);
    }

    public void TradeInitialize(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
    {
        // 获取PKM
        PKM pkm = info.TradeData;
        
        // 发送Embed卡片
        TradeEmbed(pkm, info);
        
        // 发送文字信息
        var receive = Data.Species == 0 ? string.Empty : $" ({Data.Nickname})";
        Trader.SendMessageAsync($"Initializing trade{receive}. Please be ready. Your code is **{Code:0000 0000}**.").ConfigureAwait(false);
    }

    public void TradeSearching(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
    {
        var name = Info.TrainerName;
        var trainer = string.IsNullOrEmpty(name) ? string.Empty : $", {name}";
        Trader.SendMessageAsync($"I'm waiting for you{trainer}! Your code is **{Code:0000 0000}**. My IGN is **{routine.InGameName}**.").ConfigureAwait(false);
    }

    public void TradeCanceled(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeResult msg)
    {
        OnFinish?.Invoke(routine);
        Trader.SendMessageAsync($"Trade canceled: {msg}").ConfigureAwait(false);
    }

    public void TradeFinished(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result)
    {
        OnFinish?.Invoke(routine);
        var tradedToUser = Data.Species;
        var message = tradedToUser != 0 ? $"Trade finished. Enjoy your {(Species)tradedToUser}!" : "Trade finished!";
        Trader.SendMessageAsync(message).ConfigureAwait(false);
        if (result.Species != 0 && Hub.Config.Discord.ReturnPKMs)
            Trader.SendPKMAsync(result, "Here's what you traded me!").ConfigureAwait(false);

        // if (Hub.Config.Trade.TradeDisplay && info.Type is not PokeTradeType.Dump)
        // {
        //     PKM emb = info.TradeData;
        //     if (emb.Species == 0 || info.Type is PokeTradeType.Clone)
        //         emb = result;

        //     if (emb.Species != 0)
        //     {
        //         var shiny = emb.ShinyXor == 0 ? "■" : emb.ShinyXor <= 16 ? "★" : "";
        //         var set = new ShowdownSet($"{emb.Species}");
        //         var ballImg = $"https://raw.githubusercontent.com/BakaKaito/HomeImages/main/Ballimg/50x50/" + $"{(Ball)emb.Ball}ball".ToLower() + ".png";
        //         var gender = emb.Gender == 0 ? " - (M)" : emb.Gender == 1 ? " - (F)" : "";
        //         var pokeImg = TradeExtensions<T>.PokeImg(emb, false, false);
        //         string scale = "";

        //         if (emb is PK9 fin9)
        //             scale = $"Scale: {PokeSizeDetailedUtil.GetSizeRating(fin9.Scale)} ({fin9.Scale})";
        //         if (emb is PA8 fin8a)
        //             scale = $"Scale: {PokeSizeDetailedUtil.GetSizeRating(fin8a.Scale)} ({fin8a.Scale})";
        //         if (emb is PB8 fin8b)
        //             scale = $"Scale: {PokeSizeDetailedUtil.GetSizeRating(fin8b.HeightScalar)} ({fin8b.HeightScalar})";
        //         if (emb is PK8 fin8)
        //             scale = $"Scale: {PokeSizeDetailedUtil.GetSizeRating(fin8.HeightScalar)} ({fin8.HeightScalar})";

        //         var trademessage = $"Pokémon IVs: {emb.IV_HP}/{emb.IV_ATK}/{emb.IV_DEF}/{emb.IV_SPA}/{emb.IV_SPD}/{emb.IV_SPE}\n" +
        //             $"Ability: {GameInfo.GetStrings(1).Ability[emb.Ability]}\n" +
        //             $"{(Nature)emb.Nature} Nature\n{scale}" +
        //             (TradeExtensions<T>.HasMark((IRibbonIndex)emb, out RibbonIndex mark) ? $"\nPokémon Mark: {mark.ToString().Replace("Mark", "")}{Environment.NewLine}" : "");

        //         string markEntryText = "";
        //         var index = (int)mark - (int)RibbonIndex.MarkLunchtime;
        //         if (index > 0)
        //             markEntryText = TradeExtensions<T>.MarkTitle[index];

        //         var specitem = emb.HeldItem != 0 ? $"{SpeciesName.GetSpeciesNameGeneration(emb.Species, 2, emb.Generation <= 8 ? 8 : 9)}{TradeExtensions<T>.FormOutput(emb.Species, emb.Form, out _) + " (" + ShowdownParsing.GetShowdownText(emb).Split('@', '\n')[1].Trim() + ")"}" : $"{SpeciesName.GetSpeciesNameGeneration(emb.Species, 2, emb.Generation <= 8 ? 8 : 9) + TradeExtensions<T>.FormOutput(emb.Species, emb.Form, out _)}{markEntryText}";

        //         string specieInfo = "";
        //         string itemName = "";
        //         if (emb.HeldItem != 0)
        //         {
        //             string specieName = $"{SpeciesName.GetSpeciesNameGeneration(emb.Species, 2, emb.Generation <= 8 ? 8 : 9)}";
        //             string specieForm = TradeExtensions<T>.FormOutput(emb.Species, emb.Form, out _);
        //             itemName = ShowdownParsing.GetShowdownText(emb).Split('@', '\n')[1].Trim();
        //             specieInfo = $"{specieName}{specieForm}";
        //         } 
        //         else
        //         {
        //             string specieName = $"{SpeciesName.GetSpeciesNameGeneration(emb.Species, 2, emb.Generation <= 8 ? 8 : 9)}";
        //             string specieForm = TradeExtensions<T>.FormOutput(emb.Species, emb.Form, out _);
        //             specieInfo = $"{specieName}{specieForm}{markEntryText}";
        //         }
                

        //         var msg = "Displaying your ";
        //         var mode = info.Type;
        //         switch (mode)
        //         {
        //             case PokeTradeType.Specific: msg += "request!"; break;
        //             case PokeTradeType.Clone: msg += "clone!"; break;
        //         }
        //         string TIDFormatted = emb.Generation >= 7 ? $"{emb.TrainerTID7:000000}" : $"{emb.TID16:00000}";
        //         var footer = new EmbedFooterBuilder { Text = $"Trainer Info: {emb.OriginalTrainerName}/{TIDFormatted}" };
        //         var author = new EmbedAuthorBuilder
        //         {
        //             Name = $"{Context.User.Username}'s Pokémon",
        //             IconUrl = ballImg
        //         };
        //         var embed = new EmbedBuilder { Color = emb.IsShiny && emb.ShinyXor == 0 ? Color.Gold : emb.IsShiny ? Color.LighterGrey : Color.Teal, Author = author, Footer = footer, ThumbnailUrl = pokeImg };
        //         embed.AddField(x =>
        //         {
        //             x.Name = $"{shiny} {specieInfo}{gender}";
        //             x.Value = itemName + trademessage;
        //             x.IsInline = false;
        //         });
        //         Context.Channel.SendMessageAsync(Trader.Username + " - " + msg, embed: embed.Build()).ConfigureAwait(false);
        //     }
        // }
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, string message)
    {
        Trader.SendMessageAsync(message).ConfigureAwait(false);
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeSummary message)
    {
        if (message.ExtraInfo is SeedSearchResult r)
        {
            SendNotificationZ3(r);
            return;
        }

        var msg = message.Summary;
        if (message.Details.Count > 0)
            msg += ", " + string.Join(", ", message.Details.Select(z => $"{z.Heading}: {z.Detail}"));
        Trader.SendMessageAsync(msg).ConfigureAwait(false);
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result, string message)
    {
        if (result.Species != 0 && (Hub.Config.Discord.ReturnPKMs || info.Type == PokeTradeType.Dump))
            Trader.SendPKMAsync(result, message).ConfigureAwait(false);
    }

    private void SendNotificationZ3(SeedSearchResult r)
    {
        var lines = r.ToString();

        var embed = new EmbedBuilder { Color = Color.LighterGrey };
        embed.AddField(x =>
        {
            x.Name = $"Seed: {r.Seed:X16}";
            x.Value = lines;
            x.IsInline = false;
        });
        var msg = $"Here are the details for `{r.Seed:X16}`:";
        Trader.SendMessageAsync(msg, embed: embed.Build()).ConfigureAwait(false);
    }
}
