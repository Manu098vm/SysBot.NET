using Discord;
using Discord.Commands;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    // src: https://github.com/foxbot/patek/blob/master/src/Patek/Modules/InfoModule.cs
    // ISC License (ISC)
    // Copyright 2017, Christopher F. <foxbot@protonmail.com>
    public class InfoModule : ModuleBase<SocketCommandContext>
    {
        private const string detail = "I am an open-source Discord bot powered by PKHeX.Core and other open-source software.";
        private const string repo = "https://github.com/kwsch/SysBot.NET";
        private const string fork = "https://github.com/Koi-3088/ForkBot.NET";
        private const string forkoffork = "https://github.com/zyro670/NotForkBot.NET";

        [Command("info")]
        [Alias("about", "whoami", "owner")]
        public async Task InfoAsync()
        {
            var app = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(false);

            var builder = new EmbedBuilder
            {
                Color = new Color(114, 137, 218),
                Description = detail,
            };

            builder.AddField("Info",
                $"- [Original Source Code]({repo})\n" +
                $"- [This Fork's Source Code]({fork})\n" +
                $"- [This Fork's Fork Source Code]({forkoffork})\n" +
                $"- {Format.Bold("Owner")}: {app.Owner} ({app.Owner.Id})\n" +
                $"- {Format.Bold("Library")}: Discord.Net ({DiscordConfig.Version})\n" +
                $"- {Format.Bold("Uptime")}: {GetUptime()}\n" +
                $"- {Format.Bold("Runtime")}: {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture} " +
                $"({RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture})\n" +
                $"- {Format.Bold("Buildtime")}: {GetBuildTime()}\n" +
                $"- {Format.Bold("Core")}: {GetCoreDate()}\n" +
                $"- {Format.Bold("AutoLegality")}: {GetALMDate()}\n"
                );

            builder.AddField("Stats",
                $"- {Format.Bold("Heap Size")}: {GetHeapSize()}MiB\n" +
                $"- {Format.Bold("Guilds")}: {Context.Client.Guilds.Count}\n" +
                $"- {Format.Bold("Channels")}: {Context.Client.Guilds.Sum(g => g.Channels.Count)}\n" +
                $"- {Format.Bold("Users")}: {Context.Client.Guilds.Sum(g => g.MemberCount)}\n" +
                $"{Format.Bold("\nThank you, [Project Pokémon](https://projectpokemon.org), for making Pokémon sprites and images used here publicly available!")}\n"
                );

            await ReplyAsync("Here's a bit about me!", embed: builder.Build()).ConfigureAwait(false);
        }

        private static string GetUptime() => (DateTime.Now - Process.GetCurrentProcess().StartTime).ToString(@"dd\.hh\:mm\:ss");
        private static string GetHeapSize() => Math.Round(GC.GetTotalMemory(true) / (1024.0 * 1024.0), 2).ToString(CultureInfo.CurrentCulture);
        private static string GetBuildTime() => GetAssemblyDate("SysBot.Base");
        public static string GetCoreDate() => GetAssemblyDate("PKHeX.Core");
        public static string GetALMDate() => GetAssemblyDate("PKHeX.Core.AutoMod");

        private static string GetAssemblyDate(string assemblyName)
        {
            var prefix = "+T";
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                if (assembly.GetName().Name == assemblyName)
                {
                    var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                    if (attribute is not null)
                    {
                        var version = attribute.InformationalVersion;
                        var index = version.IndexOf(prefix);
                        if (index > 0)
                        {
                            version = version[(index + prefix.Length)..];
                            if (DateTime.TryParseExact(version, "yyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var buildTime))
                                return buildTime.ToLocalTime().ToString(@"yy-MM-dd\.hh\:mm");
                        }
                    }
                }
            }
            return "Unknown";
        }
    }
}