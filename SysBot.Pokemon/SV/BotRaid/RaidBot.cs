using Newtonsoft.Json;
using PKHeX.Core;
using SysBot.Base;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using static SysBot.Base.SwitchButton;

namespace SysBot.Pokemon
{
    public class RaidSV : PokeRoutineExecutor9SV, ICountBot
    {
        private readonly PokeTradeHub<PK9> Hub;
        private readonly RaidSettingsSV Settings;
        public ICountSettings Counts => Settings;

        public static CancellationTokenSource RaidSVEmbedSource = new();
        public static bool RaidSVEmbedsInitialized;
        public static ConcurrentQueue<(byte[]?, string, string, string)> EmbedQueue = new();

        public RaidSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : base(cfg)
        {
            Hub = hub;
            Settings = hub.Config.RaidSV;
        }

        public string TID7 { get; set; } = string.Empty;
        public string SID7 { get; set; } = string.Empty;
        public string TrainerName { get; set; } = string.Empty;
        public ulong TrainerNID;
        public ulong HostNID;
        public List<string> initialTrainers = new List<string>();
        public List<ulong> initialNIDs = new List<ulong>();
        public string RaidCode { get; set; } = string.Empty;
        private const string Player1 = "[[[main+437ECE0]+48]+B0]";
        private const string Player2 = "[[[main+437ECE0]+48]+E0]";
        private const string Player3 = "[[[main+437ECE0]+48]+110]";
        private const string Player4 = "[[[main+437ECE0]+48]+140]";
        private const string ConnectionStatus = "[main+437E280]+30";
        private const string OverworldPointer = "[[[[main+43A7848]+348]+10]+D8]+28";
        private const string PlayerNIDs = "[[main+43A28F0]+F8]"; // length 32
        private const string RaidCodePointer = "[[[[[[main+437DEC0]+98]]+10]+30]+10]+1A9";
        private uint RaidLobby = 0x0403F4B0;
        private int RaidCount;
        private int ResetCount;
        private int RaidPenaltyCount;
        public RemoteControlAccessList RaiderBanList => Settings.RaiderBanList;
        public bool BannedRaider(ulong uid) => RaiderBanList.Contains(uid);

        private Dictionary<ulong, int> RaidTracker = new();

        private class EmbedInfo
        {
            public string EmbedString { get; set; } = string.Empty;
            public string EmbedFooter { get; set; } = string.Empty;
            public string EmbedTitle { get; set; } = string.Empty;
        }

        public override async Task MainLoop(CancellationToken token)
        {
            if (Settings.TimeToWaitPerSlot is < 0 or > 180)
            {
                Log("Time to wait must be between 0 and 180 seconds.");
                return;
            }

            try
            {
                Log("Identifying trainer data of the host console.");
                await IdentifyTrainer(token).ConfigureAwait(false);
                await InitializeHardware(Settings, token).ConfigureAwait(false);

                Log("Starting main RaidBot loop.");
                await InnerLoop(token).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Log(e.Message);
            }

            Log($"Ending {nameof(RaidBot)} loop.");
            await HardStop().ConfigureAwait(false);
        }

        private async Task InnerLoop(CancellationToken token)
        {
            RaidCount = 0;
            ResetCount = 0;
            while (!token.IsCancellationRequested)
            {
                await ClearPlayerHistory(token).ConfigureAwait(false);
                await InitialTrainerNIDRead(token).ConfigureAwait(false);
                await PrepareForRaid(token).ConfigureAwait(false);
                await ReadTrainers(token).ConfigureAwait(false);
                await CompleteRaid(token).ConfigureAwait(false);

                initialTrainers = new();
                await Task.Delay(2_000, token).ConfigureAwait(false);
            }
        }

        public override async Task HardStop()
        {
            RaidSVEmbedsInitialized = false;
            RaidSVEmbedSource.Cancel();
            await CleanExit(Settings, CancellationToken.None).ConfigureAwait(false);
        }

        public async Task CompleteRaid(CancellationToken token)
        {
            bool isReady = await IsConnectedToLobby(token).ConfigureAwait(false);

            if (isReady)
            {
                int b = 0;
                Log("Preparing for battle!");
                while (await IsConnectedToLobby(token).ConfigureAwait(false))
                {
                    b++;
                    await Click(A, 3_000, token).ConfigureAwait(false);
                    if (b == 10)
                    {
                        Log("Still in battle...");
                        b = 0;
                    }
                }
            }

            Log("Raid Boss defeated!");

            await Click(B, 0_500, token).ConfigureAwait(false);
            await Click(B, 0_500, token).ConfigureAwait(false);
            await Click(DDOWN, 0_500, token).ConfigureAwait(false);

            Log("Returning to overworld...");

            while (!await IsOnOverworld(token).ConfigureAwait(false))
                await Click(A, 1_000, token).ConfigureAwait(false);

            Log("Back in the overworld, raid completed.");
            Settings.AddCompletedRaids();

            await Task.Delay(1_500, token).ConfigureAwait(false);

            ResetCount++;
            await CloseGame(Hub.Config, token).ConfigureAwait(false);

            await Task.Delay(1_000, token).ConfigureAwait(false);

            if (ResetCount == Settings.RollbackTimeAfterThisManyRaids && Settings.RollbackTime)
            {
                Log("Applying rollover correction.");
                await RolloverCorrectionSV(token).ConfigureAwait(false);
                ResetCount = 0;
            }
            await StartGame(Hub.Config, token).ConfigureAwait(false);
        }

        public async Task PrepareForRaid(CancellationToken token)
        {
            Log("Preparing lobby...");
            if (!await IsOnline(token).ConfigureAwait(false))
            {
                await Click(X, 2_500, token).ConfigureAwait(false);
                await Click(L, 5_000, token).ConfigureAwait(false);
                while (!await IsOnOverworld(token).ConfigureAwait(false))
                    await Click(B, 0_500, token).ConfigureAwait(false);
            }

            if (await IsOnline(token).ConfigureAwait(false))
            {
                await Click(B, 1_000, token).ConfigureAwait(false);
                await Click(A, 2_500, token).ConfigureAwait(false);
                await Click(A, 2_500, token).ConfigureAwait(false);
            }

            if (!Settings.CodeTheRaid)
                await Click(DDOWN, 1_000, token).ConfigureAwait(false);

            await Click(A, 6_000, token).ConfigureAwait(false);

        }

        private async Task<string> GetRaidCode(CancellationToken token)
        {
            bool isReady = await IsConnectedToLobby(token).ConfigureAwait(false);
            var x = 0;
            if (!isReady)
            {
                Log("Connecting to lobby...");
                while (!await IsConnectedToLobby(token).ConfigureAwait(false))
                {
                    await Click(A, 1_000, token).ConfigureAwait(false);
                    x++;
                    if (x == 30)
                    {
                        Log("Failed to connect to lobby, restarting game incase we were in battle/bad connection.");
                        await Click(A, 1_000, token).ConfigureAwait(false);
                        await CloseGame(Hub.Config, token).ConfigureAwait(false);
                        await StartGame(Hub.Config, token).ConfigureAwait(false);
                        Log("Attempting to restart routine!");
                        await Task.Delay(1_000, token).ConfigureAwait(false);
                        await InnerLoop(token).ConfigureAwait(false);
                    }
                }
            }

            await Task.Delay(1_500, token).ConfigureAwait(false);

            var ofs = await GetPointerAddress(RaidCodePointer, token).ConfigureAwait(false);
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(ofs, 6, token).ConfigureAwait(false);
            RaidCode = Encoding.ASCII.GetString(data);
            Log("Raid Code: " + RaidCode);

            await Task.Delay(1_000, token).ConfigureAwait(false);

            return $"\nRaid Code: {RaidCode}";
        }

        private async Task InitialTrainerNIDRead(CancellationToken token)
        {
            var ofs = await GetPointerAddress(PlayerNIDs, token).ConfigureAwait(false);
            var Data = await SwitchConnection.ReadBytesAbsoluteAsync(ofs, 32, token).ConfigureAwait(false);
            for (int i = 0; i < 4; i++)
            {
                TrainerNID = BitConverter.ToUInt64(Data.Slice(0 + (i * 8), 8), 0);
                if (RaidCount != 0)
                {
                    initialNIDs.Remove(initialNIDs[i]);
                }
                initialNIDs.Add(TrainerNID);
            }
            RaidCount++;
        }

        private async Task ReadTrainers(CancellationToken token)
        {
            var info = new EmbedInfo()
            {
                EmbedString = string.Empty,
            };
            info.EmbedString += Settings.RaidDescription;
            info.EmbedFooter += Settings.RaidFooterDescription;
            info.EmbedTitle = Settings.RaidTitleDescription;

            string value = string.Empty;
            string NID = string.Empty;
            bool PartyReady = false;

            while (PartyReady == false)
            {
                for (int i = 0; i < 4; i++)
                {
                    switch (i)
                    {
                        case 0:
                            value = Player1; NID = PlayerNIDs; info.EmbedString += await GetRaidCode(token).ConfigureAwait(false);
                            if (RaidSVEmbedsInitialized)
                            {
                                var bytes = await SwitchConnection.Screengrab(token).ConfigureAwait(false);
                                EmbedQueue.Enqueue((bytes, info.EmbedString, info.EmbedFooter, info.EmbedTitle));
                            }
                            break;
                        case 1: value = Player2; break;
                        case 2: value = Player3; break;
                        case 3: value = Player4; break;
                    }

                    var nidofs = await GetPointerAddress(NID, token).ConfigureAwait(false);
                    var nidData = await SwitchConnection.ReadBytesAbsoluteAsync(nidofs, 32, token).ConfigureAwait(false);
                    TrainerNID = BitConverter.ToUInt64(nidData.Slice(0 + (i * 8), 8), 0);

                    if (i == 0)
                        HostNID = TrainerNID;

                    var tries = 0;
                    if (i != 0)
                    {
                        while (initialNIDs[i] == TrainerNID && TrainerNID == 0)
                        {
                            await Task.Delay(1_000, token).ConfigureAwait(false);
                            nidData = await SwitchConnection.ReadBytesAbsoluteAsync(nidofs, 32, token).ConfigureAwait(false);
                            TrainerNID = BitConverter.ToUInt64(nidData.Slice(0 + (i * 8), 8), 0);
                            tries++;

                            if (tries == Settings.TimeToWaitPerSlot)
                                break;
                        }
                    }

                    await Task.Delay(4_000, token).ConfigureAwait(false); // Allow trainers to load into lobby

                    var ofs = await GetPointerAddress(value, token).ConfigureAwait(false);
                    var Data = await SwitchConnection.ReadBytesAbsoluteAsync(ofs, 32, token).ConfigureAwait(false);
                    var DisplayTID = BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(0)) % 1_000_000;
                    TID7 = DisplayTID.ToString("D6");
                    TrainerName = StringConverter8.GetString(Data.AsSpan(8, 24));
                    initialTrainers.Add(TrainerName);

                    if (TrainerNID != 0)
                    {
                        Log($"Player {i + 1} - " + TrainerName + " | TID: " + TID7 + " | NID: {TrainerNID}");
                        info.EmbedString += $"\nPlayer {i + 1} - " + TrainerName;

                        RaidPenaltyCount = 0;

                        if (i == 0)
                            HostNID = TrainerNID;

                        if (!RaidTracker.ContainsKey(TrainerNID) && HostNID != TrainerNID)
                            RaidTracker.Add(TrainerNID, RaidPenaltyCount);


                        if (RaidTracker.ContainsKey(TrainerNID))
                        {
                            RaidPenaltyCount = RaidTracker[TrainerNID] + RaidPenaltyCount + 1;
                            RaidTracker.Remove(TrainerNID);
                            RaidTracker.Add(TrainerNID, RaidPenaltyCount);

                            if (RaidPenaltyCount > Settings.MaxJoinsPerRaider && !RaiderBanList.Contains(TrainerNID) && Settings.MaxJoinsPerRaider != 0)
                            {
                                Log($"{TrainerName} has been banned for joining {RaidPenaltyCount}x this raid session on {DateTime.Now}.");
                                RaiderBanList.List.Add(new() { ID = TrainerNID, Name = initialTrainers[i], Comment = $"Exceeded max joins on {DateTime.Now}." });
                            }
                        }

                        if (BannedRaider(TrainerNID))
                        {
                            var msg = $"Raid Canceled Due to Banned User\nBanned User: {initialTrainers[i]} was found in the lobby.\nRecreating raid team.";
                            Log(msg);
                            if (RaidSVEmbedsInitialized)
                            {
                                var bytes = await SwitchConnection.Screengrab(token).ConfigureAwait(false);
                                EmbedQueue.Enqueue((bytes, "", "", msg));
                            }
                            await Task.Delay(1_000, token).ConfigureAwait(false);
                            await RegroupFromBannedUser(token).ConfigureAwait(false);
                            await Task.Delay(1_000, token).ConfigureAwait(false);
                            await PrepareForRaid(token).ConfigureAwait(false);
                            await ReadTrainers(token).ConfigureAwait(false);
                        }
                    }
                }
                PartyReady = true;
            }

            await Task.Delay(2_500, token).ConfigureAwait(false);

            Log($"Raid #{RaidCount} is starting!");
            if (!string.IsNullOrEmpty(initialTrainers[1]) && !string.IsNullOrEmpty(initialTrainers[2]) && !string.IsNullOrEmpty(initialTrainers[3]))
            {
                if (string.Equals(initialTrainers[1], initialTrainers[2]) && string.Equals(initialTrainers[2], initialTrainers[3]))
                {
                    info.EmbedTitle = $" 🌟🎩🌟 {initialTrainers[1]} Hat Trick 🌟🎩🌟\n\n" + Settings.RaidTitleDescription;
                }
            }

            if (RaidSVEmbedsInitialized)
            {
                var bytes = await SwitchConnection.Screengrab(token).ConfigureAwait(false);
                EmbedQueue.Enqueue((bytes, info.EmbedString, info.EmbedFooter, info.EmbedTitle));
            }
        }

        public async Task ClearPlayerHistory(CancellationToken token)
        {
            var trainerdata = new byte[32];
            var ofs = await GetPointerAddress(PlayerNIDs, token).ConfigureAwait(false);
            await SwitchConnection.WriteBytesAbsoluteAsync(trainerdata, ofs, token).ConfigureAwait(false);

        }

        private async Task<bool> IsOnline(CancellationToken token)
        {
            var ofs = await GetPointerAddress(ConnectionStatus, token).ConfigureAwait(false);
            var Data = await SwitchConnection.ReadBytesAbsoluteAsync(ofs, 1, token).ConfigureAwait(false);
            return Data[0] == 1;
        }

        private async Task<bool> IsConnectedToLobby(CancellationToken token)
        {
            var Data = await SwitchConnection.ReadBytesMainAsync(RaidLobby, 1, token).ConfigureAwait(false);
            return Data[0] != 0x00; // 0 when in lobby but not connected
        }

        private async Task<bool> IsOnOverworld(CancellationToken token)
        {
            var ofs = await GetPointerAddress(OverworldPointer, token).ConfigureAwait(false);
            var Data = await SwitchConnection.ReadBytesAbsoluteAsync(ofs, 1, token).ConfigureAwait(false);
            return Data[0] == 0x11;
        }

        private async Task RolloverCorrectionSV(CancellationToken token)
        {
            for (int i = 0; i < 2; i++)
                await Click(DRIGHT, 0_150, token).ConfigureAwait(false);
            await Click(DDOWN, 0_150, token).ConfigureAwait(false);
            await Click(DRIGHT, 0_150, token).ConfigureAwait(false);
            await Click(A, 1_250, token).ConfigureAwait(false); // Enter settings

            await PressAndHold(DDOWN, 2_000, 0_250, token).ConfigureAwait(false); // Scroll to system settings
            await Click(A, 1_250, token).ConfigureAwait(false);

            await PressAndHold(DDOWN, Settings.TimeToScrollDownForRollover, 0, token).ConfigureAwait(false);
            await Click(DUP, 0_500, token).ConfigureAwait(false);
            await Click(DUP, 0_500, token).ConfigureAwait(false);

            await Click(A, 1_250, token).ConfigureAwait(false);
            for (int i = 0; i < 2; i++)
                await Click(DDOWN, 0_150, token).ConfigureAwait(false);
            await Click(A, 0_500, token).ConfigureAwait(false);
            for (int i = 0; i < 3; i++)
                await Click(DRIGHT, 0_150, token).ConfigureAwait(false);
            await Click(DDOWN, 0_150, token).ConfigureAwait(false);
            for (int i = 0; i < 4; i++)
                await Click(A, 0_200, token).ConfigureAwait(false);

            await Click(HOME, 1_000, token).ConfigureAwait(false); // Back to title screen
        }

        private async Task RegroupFromBannedUser(CancellationToken token)
        {
            await Click(B, 1_250, token).ConfigureAwait(false);
            await Click(A, 3_000, token).ConfigureAwait(false);
            await Click(A, 1_500, token).ConfigureAwait(false);
            await Click(B, 1_000, token).ConfigureAwait(false);
        }
    }
}
