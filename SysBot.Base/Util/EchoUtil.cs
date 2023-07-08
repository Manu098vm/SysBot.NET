using Discord;
using System;
using System.Collections.Generic;
using System.IO;

namespace SysBot.Base
{
    public static class EchoUtil
    {
        public static readonly List<Action<string>> Forwarders = new();
        public static readonly List<Action<string, Embed>> EmbedForwarders = new();
        public static readonly List<Action<byte[], string, EmbedBuilder>> RaidForwarders = new();

        public static void Echo(string message)
        {
            foreach (var fwd in Forwarders)
            {
                try
                {
                    fwd(message);
                }
                catch (Exception ex)
                {
                    LogUtil.LogInfo($"Exception: {ex} occurred while trying to echo: {message} to the forwarder: {fwd}", "Echo");
                    LogUtil.LogSafe(ex, "Echo");
                }
            }
            LogUtil.LogInfo(message, "Echo");
        }

        public static void RaidEmbed(byte[] bytes, string fileName, EmbedBuilder embeds)
        {
            foreach (var fwd in RaidForwarders)
            {
                try
                {
                    fwd(bytes, fileName, embeds);
                }
                catch (Exception ex)
                {
                    LogUtil.LogInfo($"Exception: {ex} occurred while trying to echo: RaidEmbed to the forwarder: {fwd}", "Echo");
                    LogUtil.LogSafe(ex, "Echo");
                }
            }
        }
    }
}