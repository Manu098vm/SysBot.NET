using Discord.Commands;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class StreamModule : ModuleBase<SocketCommandContext>
    {
        [Command("stream")]
        [Alias("streamlink")]
        [Summary("Returns the Host's Stream link.")]
        public async Task PingAsync()
        {
            var str = $"Here's the Stream link, enjoy :3 {SysCordSettings.Settings.StreamLink}";
            await ReplyAsync(str).ConfigureAwait(false);
        }
    }
}