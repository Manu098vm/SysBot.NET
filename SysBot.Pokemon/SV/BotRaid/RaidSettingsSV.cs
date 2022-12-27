using System.Collections.Generic;
using PKHeX.Core;
using System.ComponentModel;
using System.Threading;
using SysBot.Base;
using Discord;

namespace SysBot.Pokemon
{
    public class RaidSettingsSV : IBotStateSettings, ICountSettings
    {
        private const string Hosting = nameof(Hosting);
        private const string Counts = nameof(Counts);
        private const string FeatureToggle = nameof(FeatureToggle);
        public override string ToString() => "Raid Bot Settings";

        [Category(FeatureToggle), Description("Optional description of the raid the bot is hosting.")]
        public string RaidDescription { get; set; } = string.Empty;

        [Category(FeatureToggle), Description("Optional description of the raid title the bot is hosting.")]
        public string RaidTitleDescription { get; set; } = string.Empty;

        [Category(FeatureToggle), Description("Optional footer description of the raid the bot is hosting.")]
        public string RaidFooterDescription { get; set; } = string.Empty;

        [Category(Hosting), Description("Input the Species to post a Thumbnail in the embeds. Ignored if 0.")]
        public Species RaidSpecies { get; set; } = Species.None;

        [Category(Hosting), Description("Minimum amount of seconds to wait per player before starting a raid. Ranges from 0 to 180 seconds.")]
        public int MinTimeToWait { get; set; } = 30;

        [Category(FeatureToggle), Description("If true, the bot will use a random code for the raid.")]
        public bool CodeTheRaid { get; set; } = true;

        [Category(Hosting), Description("Maximum number of join a raider can participate before they get added to the ban list automatically in an instance. If 0 will ignore multidippers.")]
        public int MaxJoinsPerRaider { get; set; } = 0;

        [Category(FeatureToggle), Description("If true, the bot will apply rollback correction.")]
        public bool RollbackTime { get; set; } = true;

        [Category(Hosting), Description("Users NIDs here are banned raiders.")]
        public RemoteControlAccessList RaiderBanList { get; set; } = new() { AllowIfEmpty = false };

        [Category(FeatureToggle), Description("If true, the bot will export the current raider ban list to a json file.")]
        public bool ExportBanListToJson { get; set; } = true;

        [Category(Hosting), Description("Amount of raids to complete before rolling time back 1 hour.")]
        public int RollbackTimeAfterThisManyRaids { get; set; } = 10;

        [Category(Hosting), Description("Time to scroll down duration in milliseconds for accessing date/time settings during rollover correction. [Default: 1000ms]")]
        public int TimeToScrollDownForRollover { get; set; } = 1000;

        [Category(Hosting), Description("Enter Discord channel ID(s) to post raid embeds to. Feature has to be initialized via \"$resv\" after every client restart.")]
        public string RaidEmbedChannelsSV { get; set; } = string.Empty;

        [Category(FeatureToggle), Description("When enabled, the screen will be turned off during normal bot loop operation to save power.")]
        public bool ScreenOff { get; set; } = false;

        private int _completedRaids;

        [Category(Counts), Description("Raids Started")]
        public int CompletedRaids
        {
            get => _completedRaids;
            set => _completedRaids = value;
        }

        [Category(Counts), Description("When enabled, the counts will be emitted when a status check is requested.")]
        public bool EmitCountsOnStatusCheck { get; set; }

        public int AddCompletedRaids() => Interlocked.Increment(ref _completedRaids);

        public IEnumerable<string> GetNonZeroCounts()
        {
            if (!EmitCountsOnStatusCheck)
                yield break;
            if (CompletedRaids != 0)
                yield return $"Started Raids: {CompletedRaids}";
        }
    }
}