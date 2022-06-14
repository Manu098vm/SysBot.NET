using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class EtumrepDumpSettings
    {
        private const string FeatureToggle = nameof(FeatureToggle);
        public override string ToString() => "Etumrep Dump Settings";

        [Category(FeatureToggle), Description("Server IP address or host name.")]
        public string IP { get; set; } = string.Empty;

        [Category(FeatureToggle), Description("Server port.")]
        public int Port { get; set; } = 80;

        [Category(FeatureToggle), Description("Authentication token.")]
        public string Token { get; set; } = string.Empty;

        [Category(FeatureToggle), Description("Maximum time to wait for a user to show all Pokémon before quitting.")]
        public int MaxWaitTime { get; set; } = 60;
    }
}