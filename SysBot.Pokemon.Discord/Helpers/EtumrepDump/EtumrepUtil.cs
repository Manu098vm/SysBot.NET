using System;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using PKHeX.Core;
using SysBot.Base;

namespace SysBot.Pokemon.Discord
{
    public class EtumrepUtil
    {
        private static TradeQueueInfo<PA8> Info => SysCord<PA8>.Runner.Hub.Queues.Info;
        private static readonly PokeTradeHubConfig Config = Info.Hub.Config;

        private static ConcurrentQueue<EtumrepUser> UserQueue { get; set; } = new();
        private static bool IsRunning { get; set; }
        private static string IP { get => Config.EtumrepDump.IP; }
        private static int Port { get => Config.EtumrepDump.Port; }
        private static string Token { get => Config.EtumrepDump.Token; }

        internal class EtumrepUser
        {
            internal EtumrepUser(TcpClient client, AuthenticatedStream stream, SocketMessageComponent component)
            {
                Client = client;
                Stream = stream;
                Component = component;
                BotName = $"{component.Message.Author.Username}#{component.Message.Author.Discriminator}";
                SeedCheckerName = $"{component.User.Username}#{component.User.Discriminator}";
                SeedCheckerID = component.User.Id;
                Stream.ReadTimeout = 2_000;
                Stream.WriteTimeout = 2_000;
            }

            public TcpClient Client { get; }
            public AuthenticatedStream Stream { get; }
            public SocketMessageComponent Component { get; }
            public string BotName { get; }
            public string SeedCheckerName { get; }
            public ulong SeedCheckerID { get; }
            public bool IsAuthenticated { get; set; }
        }

        internal class UserAuth
        {
            public string HostName { get; set; } = string.Empty;
            public string HostID { get; set; } = string.Empty;
            public string Token { get; set; } = string.Empty;
            public string SeedCheckerName { get; set; } = string.Empty;
            public string SeedCheckerID { get; set; } = string.Empty;
        }

        public static async Task SendEtumrepEmbedAsync(SocketUser user, IReadOnlyList<PA8> pkms)
        {
            var list = new List<FileAttachment>();
            for (int i = 0; i < pkms.Count; i++)
            {
                var pk = pkms[i];
                var ms = new MemoryStream(pk.Data);
                var name = Util.CleanFileName(pk.FileName);
                list.Add(new(ms, name));
            }

            var embed = new EmbedBuilder { Color = Color.Blue };
            var dmCh = await user.CreateDMChannelAsync().ConfigureAwait(false);

            if (IP != string.Empty && Token != string.Empty)
            {
                var buttonYes = new ButtonBuilder() { CustomId = "etumrep_yes", Label = "Yes", Style = ButtonStyle.Primary };
                var buttonNo = new ButtonBuilder() { CustomId = "etumrep_no", Label = "No", Style = ButtonStyle.Secondary };
                var components = new ComponentBuilder().WithButton(buttonYes).WithButton(buttonNo);

                embed.Description = "Here are all the Pokémon you dumped!\nWould you now like to run EtumrepMMO?";
                embed.WithAuthor(x => { x.Name = "EtumrepMMO Service"; });

                await dmCh.SendFilesAsync(list, null, false, embed: embed.Build(), null, null, null, components: components.Build()).ConfigureAwait(false);
                return;
            }

            embed.Description = "Here are all the Pokémon you dumped!";
            embed.WithAuthor(x => { x.Name = "Pokémon Legends: Arceus Dump"; });
            await dmCh.SendFilesAsync(list, null, false, embed: embed.Build()).ConfigureAwait(false);
        }

        public static async Task HandleEtumrepRequestAsync(SocketMessageComponent component, string id)
        {
            if (id is "etumrep_yes")
            {
                var msg = "Attempting to communicate with the EtumrepMMO server, please wait...";
                await UpdateEtumrepEmbed(component.Message, msg, Color.Green).ConfigureAwait(false);
                LogUtil.LogInfo(msg, "[EtumrepMMO Handler]");

                var user = await AuthenticateConnection(component).ConfigureAwait(false);
                if (user is null)
                {
                    msg = "Unable to connect to the server. Server might be offline.";
                    await UpdateEtumrepEmbed(component.Message, msg, Color.Red).ConfigureAwait(false);
                    LogUtil.LogInfo(msg, "[EtumrepMMO Handler]");
                    return;
                }

                if (!user.IsAuthenticated)
                {
                    DisposeStream(user);
                    msg = "Server rejected the authorization.";
                    await UpdateEtumrepEmbed(component.Message, msg, Color.Red).ConfigureAwait(false);
                    LogUtil.LogInfo($"{user.BotName}: {msg}", "[EtumrepMMO Handler]");
                    return;
                }

                var authenticated = await Authenticate(user).ConfigureAwait(false);
                if (!authenticated)
                {
                    DisposeStream(user);
                    msg = "Bot owner is not authorized to use the EtumrepMMO server.";
                    await UpdateEtumrepEmbed(component.Message, msg, Color.Red).ConfigureAwait(false);
                    LogUtil.LogInfo($"{user.BotName}: {msg}", "[EtumrepMMO Handler]");
                    return;
                }

                msg = "Successfully authenticated with the server, entering the queue...";
                await UpdateEtumrepEmbed(component.Message, msg, Color.Green).ConfigureAwait(false);
                LogUtil.LogInfo($"{user.BotName}: Successfully authenticated with the server, enqueueing {user.SeedCheckerName} ({user.SeedCheckerID})...", "[EtumrepMMO Handler]");
                UserQueue.Enqueue(user);

                if (!IsRunning)
                    _ = Task.Run(async () => await EtumrepQueue().ConfigureAwait(false));
            }
            else if (id is "etumrep_no")
            {
                var msg = component.Message.Embeds.First().Description.Split('\n').First();
                await UpdateEtumrepEmbed(component.Message, msg, Color.DarkBlue).ConfigureAwait(false);

                var username = $"{component.User.Username}#{component.User.Discriminator} ({component.User.Id})";
                LogUtil.LogInfo($"{username} did not wish to run EtumrepMMO.", "[EtumrepMMO Handler]");
            }
        }

        private static async Task<EtumrepUser?> AuthenticateConnection(SocketMessageComponent component)
        {
            IPAddress ip = default!;
            var author = component.Message.Author;
            _ = IPAddress.TryParse(IP, out IPAddress? address);

            if (address is not null)
                ip = address;
            else
            {
                try
                {
                    var dns = await Dns.GetHostEntryAsync(IP).ConfigureAwait(false);
                    var addr = dns.AddressList.FirstOrDefault(x => x.ToString().Split('.').Length >= 4)!;
                    ip = IPAddress.Parse(addr.ToString());
                }
                catch (Exception ex)
                {
                    LogUtil.LogInfo($"{author.Username}#{author.Discriminator}: Failed to resolve IP/host address. Dequeueing {component.User.Username}#{component.User.Discriminator}...\n{ex.Message}", "[Connection Authentication]");
                    return null;
                }
            }

            var ep = new IPEndPoint(ip, Port);
            var client = new TcpClient
            {
                SendTimeout = 2_000,
                ReceiveTimeout = 2_000,
            };

            try
            {
                client.Connect(ep);
            }
            catch (Exception ex)
            {
                LogUtil.LogInfo($"{author.Username}#{author.Discriminator}: Failed to connect to server. Dequeueing {component.User.Username}#{component.User.Discriminator}...\n{ex.Message}", "[Connection Authentication]");
                return null;
            }
            client.LingerState = new(true, 0);

            var clientStream = client.GetStream();
            var authStream = new NegotiateStream(clientStream, false);
            var user = new EtumrepUser(client, authStream, component);

            try
            {
                var credentials = new NetworkCredential();
                await authStream.AuthenticateAsClientAsync(credentials, "").ConfigureAwait(false);
                user.IsAuthenticated = true;
                LogUtil.LogInfo($"{user.BotName}: Initial server authentication complete. Continuing to authenticate {user.SeedCheckerName}...", "[Connection Authentication]");
                return user;
            }
            catch (Exception ex)
            {
                LogUtil.LogInfo($"{user.BotName}: Failed to authenticate with server. Dequeueing {user.SeedCheckerName}...\n{ex.Message}", "[Connection Authentication]");
                return user;
            }
        }

        private static async Task<bool> Authenticate(EtumrepUser user)
        {
            if (!user.Stream.CanWrite)
            {
                LogUtil.LogInfo($"{user.BotName}: Cannot write to server. Dequeueing {user.SeedCheckerName}...", "[Server Authentication]");
                return false;
            }

            var auth = new UserAuth()
            {
                HostID = SysCord<PA8>.App.Owner.Id.ToString(),
                HostName = $"{SysCord<PA8>.App.Owner.Username}#{SysCord<PA8>.App.Owner.Discriminator}",
                SeedCheckerID = user.SeedCheckerID.ToString(),
                SeedCheckerName = user.SeedCheckerName,
                Token = Token,
            };

            var authStr = JsonConvert.SerializeObject(auth);
            var authBytes = Encoding.Unicode.GetBytes(authStr);
            await user.Stream.WriteAsync(authBytes).ConfigureAwait(false);

            var conf = new byte[1];
            try
            {
                await user.Stream.ReadAsync(conf.AsMemory(0, 1)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogUtil.LogInfo($"{user.BotName}: Authentication rejected by the server. Dequeueing {user.SeedCheckerName}...\n{ex.Message}", "[Server Authentication]");
                return false;
            }
            return BitConverter.ToBoolean(conf, 0);
        }

        private static async Task EtumrepQueue()
        {
            IsRunning = true;
            while (!UserQueue.IsEmpty)
            {
                bool ready = UserQueue.TryDequeue(out var user);
                if (ready && user is not null)
                {
                    var att = user.Component.Message.Attachments.ToArray();
                    byte[] data = new byte[376 * att.Length];
                    for (int i = 0; i < att.Length; i++)
                    {
                        var download = await NetUtil.DownloadFromUrlAsync(att[i].Url).ConfigureAwait(false);
                        download.CopyTo(data, 376 * i);
                    }

                    var msg = $"Sending data to server, beginning seed calculation. Please wait...";
                    await UpdateEtumrepEmbed(user.Component.Message, msg, Color.DarkGreen).ConfigureAwait(false);
                    LogUtil.LogInfo($"{user.BotName}: Sending data from {user.SeedCheckerName} to server for seed calculation.", "[EtumrepMMO Queue]");

                    try
                    {
                        await user.Stream.WriteAsync(data).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        DisposeStream(user);
                        var msgE = $"Data communication with the server failed.\n{ex.Message}";
                        await UpdateEtumrepEmbed(user.Component.Message, msgE, Color.Red).ConfigureAwait(false);
                        LogUtil.LogInfo($"{user.BotName}: {msgE}", "[EtumrepMMO Queue]");
                        continue;
                    }

                    byte[] buffer = new byte[8];
                    try
                    {
                        await user.Stream.ReadAsync(buffer).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        DisposeStream(user);
                        var msgE = $"Failed to retrieve the result from the server.\n{ex.Message}";
                        await UpdateEtumrepEmbed(user.Component.Message, msgE, Color.Red).ConfigureAwait(false);
                        LogUtil.LogInfo($"{user.BotName}: {msgE}", "[EtumrepMMO Queue]");
                        continue;
                    }

                    DisposeStream(user);
                    var seed = BitConverter.ToUInt64(buffer, 0);

                    if (seed == 0)
                    {
                        msg = "Failed to calculate seed. Please make sure shown Pokémon are the first four spawns, and have come from an MO or MMO.";
                        await UpdateEtumrepEmbed(user.Component.Message, msg, Color.Gold).ConfigureAwait(false);
                    }
                    else
                    {
                        var components = new ComponentBuilder();
                        var buttonYes = new ButtonBuilder() { CustomId = $"permute_yes;{seed}", Label = "Yes", Style = ButtonStyle.Primary };
                        components.WithButton(buttonYes);

                        var buttonNo = new ButtonBuilder() { CustomId = "permute_no", Label = "No", Style = ButtonStyle.Secondary };
                        components.WithButton(buttonNo);

                        var seedMsg = $"Your seed is `{seed}`";
                        msg = $"Result received! {seedMsg}\nWould you now like to run PermuteMMO?";
                        await UpdateEtumrepEmbed(user.Component.Message, msg, Color.Gold, components.Build(), seedMsg).ConfigureAwait(false);
                    }

                    LogUtil.LogInfo($"{user.BotName}: Seed calculation for {user.SeedCheckerName} completed successfully.", "[EtumrepMMO Queue]");
                }
            }
            IsRunning = false;
        }

        private static async Task UpdateEtumrepEmbed(SocketUserMessage message, string desc, Color color, MessageComponent? components = null, string? seed = null)
        {
            var embed = new EmbedBuilder
            {
                Color = color,
                Description = desc,
            }.WithAuthor(x => { x.Name = "EtumrepMMO Service"; });

            await message.ModifyAsync(x =>
            {
                x.Embed = embed.Build();
                x.Components = components;
                x.Content = seed;
            }).ConfigureAwait(false);
        }

        private static void DisposeStream(EtumrepUser user)
        {
            user.Client.Dispose();
            user.Stream.Dispose();
        }
    }
}