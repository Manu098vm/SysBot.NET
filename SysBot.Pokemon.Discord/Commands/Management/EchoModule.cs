using Discord.Commands;
using Discord.WebSocket;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using PKHeX.Core;
using System.IO;

namespace SysBot.Pokemon.Discord
{
    public class EchoModule : ModuleBase<SocketCommandContext>
    {
        private class EchoChannel
        {
            public readonly ulong ChannelID;
            public readonly string ChannelName;
            public readonly Action<string> Action;
            public readonly Action<string, Embed> EmbedAction;
            public readonly Action<byte[], string, EmbedBuilder> RaidAction;
            public string EmbedResult = string.Empty;

            public EchoChannel(ulong channelId, string channelName, Action<string> action, Action<string, Embed> embedaction, Action<byte[], string, EmbedBuilder> raidAction)
            {
                ChannelID = channelId;
                ChannelName = channelName;
                Action = action;
                EmbedAction = embedaction;
                RaidAction = raidAction;
            }
        }

        private static readonly Dictionary<ulong, EchoChannel> Channels = new();

        public static void RestoreChannels(DiscordSocketClient discord, DiscordSettings cfg)
        {
            foreach (var ch in cfg.EchoChannels)
            {
                if (discord.GetChannel(ch.ID) is ISocketMessageChannel c)
                    AddEchoChannel(c, ch.ID);
            }

            if (SysCordSettings.Settings.EchoOnBotStart)
                EchoUtil.Echo("Added echo notification to Discord channel(s) on Bot startup.");
        }

        [Command("echoHere")]
        [Summary("Makes the echo special messages to the channel.")]
        [RequireSudo]
        public async Task AddEchoAsync()
        {
            var c = Context.Channel;
            var cid = c.Id;
            if (Channels.TryGetValue(cid, out _))
            {
                await ReplyAsync("Already notifying here.").ConfigureAwait(false);
                return;
            }

            AddEchoChannel(c, cid);

            // Add to discord global loggers (saves on program close)
            SysCordSettings.Settings.EchoChannels.AddIfNew(new[] { GetReference(Context.Channel) });
            await ReplyAsync("Added Echo output to this channel!").ConfigureAwait(false);
        }

        private static void AddEchoChannel(ISocketMessageChannel c, ulong cid)
        {
            void Echo(string msg) => c.SendMessageAsync(msg);
            void EchoEmbed(string ping, Embed embed) => c.SendMessageAsync(ping, false, embed);
            async Task RaidEmbedAsync(byte[] bytes, string fileName, EmbedBuilder embed) => await c.SendFileAsync(new MemoryStream(bytes), fileName, "", false, embed: embed.Build()).ConfigureAwait(false);

            Action<string> l = Echo;
            Action<string, Embed> lb = EchoEmbed;
            Action<byte[], string, EmbedBuilder> rb = async (bytes, fileName, embed) => await RaidEmbedAsync(bytes, fileName, embed);
            EchoUtil.Forwarders.Add(l);
            EchoUtil.EmbedForwarders.Add(lb);
            EchoUtil.RaidForwarders.Add(rb);
            var entry = new EchoChannel(cid, c.Name, l, lb, rb);
            Channels.Add(cid, entry);
        }

        public static bool IsEchoChannel(ISocketMessageChannel c)
        {
            var cid = c.Id;
            return Channels.TryGetValue(cid, out _);
        }

        [Command("echoInfo")]
        [Summary("Dumps the special message (Echo) settings.")]
        [RequireSudo]
        public async Task DumpEchoInfoAsync()
        {
            foreach (var c in Channels)
                await ReplyAsync($"{c.Key} - {c.Value}").ConfigureAwait(false);
        }

        [Command("echoClear")]
        [Summary("Clears the special message echo settings in that specific channel.")]
        [RequireSudo]
        public async Task ClearEchosAsync()
        {
            var id = Context.Channel.Id;
            if (!Channels.TryGetValue(id, out var echo))
            {
                await ReplyAsync("Not echoing in this channel.").ConfigureAwait(false);
                return;
            }
            EchoUtil.Forwarders.Remove(echo.Action);
            EchoUtil.EmbedForwarders.Remove(echo.EmbedAction);
            EchoUtil.RaidForwarders.Remove(echo.RaidAction);
            Channels.Remove(Context.Channel.Id);
            SysCordSettings.Settings.EchoChannels.RemoveAll(z => z.ID == id);
            await ReplyAsync($"Echoes cleared from channel: {Context.Channel.Name}").ConfigureAwait(false);
        }

        [Command("echoClearAll")]
        [Summary("Clears all the special message Echo channel settings.")]
        [RequireSudo]
        public async Task ClearEchosAllAsync()
        {
            foreach (var l in Channels)
            {
                var entry = l.Value;
                await ReplyAsync($"Echoing cleared from {entry.ChannelName} ({entry.ChannelID}!").ConfigureAwait(false);
                EchoUtil.Forwarders.Remove(entry.Action);
            }
            EchoUtil.Forwarders.RemoveAll(y => Channels.Select(x => x.Value.Action).Contains(y));
            EchoUtil.EmbedForwarders.RemoveAll(y => Channels.Select(x => x.Value.EmbedAction).Contains(y));
            EchoUtil.RaidForwarders.RemoveAll(y => Channels.Select(x => x.Value.RaidAction).Contains(y));
            Channels.Clear();
            SysCordSettings.Settings.EchoChannels.Clear();
            await ReplyAsync("Echoes cleared from all channels!").ConfigureAwait(false);
        }

        private RemoteControlAccess GetReference(IChannel channel) => new()
        {
            ID = channel.Id,
            Name = channel.Name,
            Comment = $"Added by {Context.User.Username} on {DateTime.Now:yyyy.MM.dd-hh:mm:ss}",
        };

    }
}