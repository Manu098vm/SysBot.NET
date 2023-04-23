using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using static SysBot.Pokemon.PokeDataOffsetsSV;
using static SysBot.Base.SwitchButton;
using static System.Buffers.Binary.BinaryPrimitives;

namespace SysBot.Pokemon
{
    public abstract class PokeRoutineExecutor9SV : PokeRoutineExecutor<PK9>
    {
        protected PokeDataOffsetsSV Offsets { get; } = new();
        protected PokeRoutineExecutor9SV(PokeBotState cfg) : base(cfg)
        {
        }

        public override async Task<PK9> ReadPokemon(ulong offset, CancellationToken token) => await ReadPokemon(offset, BoxFormatSlotSize, token).ConfigureAwait(false);

        public override async Task<PK9> ReadPokemon(ulong offset, int size, CancellationToken token)
        {
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, size, token).ConfigureAwait(false);
            return new PK9(data);
        }

        public async Task<PK9> ReadPokemonMain(uint offset, int size, CancellationToken token)
        {
            var data = await SwitchConnection.ReadBytesMainAsync(offset, size, token).ConfigureAwait(false);
            var pk = new PK9(data);
            return pk;
        }

        public override async Task<PK9> ReadPokemonPointer(IEnumerable<long> jumps, int size, CancellationToken token)
        {
            var (valid, offset) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
            if (!valid)
                return new PK9();
            return await ReadPokemon(offset, token).ConfigureAwait(false);
        }

        public async Task<bool> ReadIsChanged(uint offset, byte[] original, CancellationToken token)
        {
            var result = await Connection.ReadBytesAsync(offset, original.Length, token).ConfigureAwait(false);
            return !result.SequenceEqual(original);
        }

        public override async Task<PK9> ReadBoxPokemon(int box, int slot, CancellationToken token)
        {
            // Shouldn't be reading anything but box1slot1 here. Slots are not consecutive.
            var jumps = Offsets.BoxStartPokemonPointer.ToArray();
            return await ReadPokemonPointer(jumps, BoxFormatSlotSize, token).ConfigureAwait(false);
        }

        public async Task SetBoxPokemonAbsolute(ulong offset, PK9 pkm, CancellationToken token, ITrainerInfo? sav = null)
        {
            if (sav != null)
            {
                // Update PKM to the current save's handler data
                pkm.Trade(sav);
                pkm.RefreshChecksum();
            }

            pkm.ResetPartyStats();
            await SwitchConnection.WriteBytesAbsoluteAsync(pkm.EncryptedBoxData, offset, token).ConfigureAwait(false);
        }

        public async Task SetCurrentBox(byte box, CancellationToken token)
        {
            await SwitchConnection.PointerPoke(new[] { box }, Offsets.CurrentBoxPointer, token).ConfigureAwait(false);
        }

        public async Task<byte> GetCurrentBox(CancellationToken token)
        {
            var data = await SwitchConnection.PointerPeek(1, Offsets.CurrentBoxPointer, token).ConfigureAwait(false);
            return data[0];
        }

        public async Task<SAV9SV> IdentifyTrainer(CancellationToken token)
        {
            // Check if botbase is on the correct version or later.
            await VerifyBotbaseVersion(token).ConfigureAwait(false);

            // Check title so we can warn if mode is incorrect.
            string title = await SwitchConnection.GetTitleID(token).ConfigureAwait(false);
            if (title is not (ScarletID or VioletID))
                throw new Exception($"{title} is not a valid SV title. Is your mode correct?");

            // Verify the game version.
            var game_version = await SwitchConnection.GetGameInfo("version", token).ConfigureAwait(false);
            if (!game_version.SequenceEqual(SVGameVersion))
                throw new Exception($"Game version is not supported. Expected version {SVGameVersion}, and current game version is {game_version}.");

            var sav = await GetFakeTrainerSAV(token).ConfigureAwait(false);
            InitSaveData(sav);

            if (!IsValidTrainerData())
                throw new Exception("Trainer data is not valid. Refer to the SysBot.NET wiki (https://github.com/kwsch/SysBot.NET/wiki/Troubleshooting) to fix this error.");
            if (await GetTextSpeed(token).ConfigureAwait(false) < TextSpeedOption.Fast)
                throw new Exception("Text speed should be set to FAST. Fix this for correct operation.");

            return sav;
        }

        public async Task<SAV9SV> GetFakeTrainerSAV(CancellationToken token)
        {
            var sav = new SAV9SV();
            var info = sav.MyStatus;
            var read = await SwitchConnection.PointerPeek(info.Data.Length, Offsets.MyStatusPointer, token).ConfigureAwait(false);
            read.CopyTo(info.Data, 0);
            return sav;
        }

        public async Task<TradeMyStatus> GetTradePartnerMyStatus(IReadOnlyList<long> pointer, CancellationToken token)
        {
            var info = new TradeMyStatus();
            var read = await SwitchConnection.PointerPeek(info.Data.Length, pointer, token).ConfigureAwait(false);
            read.CopyTo(info.Data, 0);
            return info;
        }

        public async Task InitializeHardware(IBotStateSettings settings, CancellationToken token)
        {
            Log("Detaching on startup.");
            await DetachController(token).ConfigureAwait(false);
            if (settings.ScreenOff)
            {
                Log("Turning off screen.");
                await SetScreen(ScreenState.Off, token).ConfigureAwait(false);
            }
        }

        public async Task CleanExit(CancellationToken token)
        {
            await SetScreen(ScreenState.On, token).ConfigureAwait(false);
            Log("Detaching controllers on routine exit.");
            await DetachController(token).ConfigureAwait(false);
        }

        protected virtual async Task EnterLinkCode(int code, PokeTradeHubConfig config, CancellationToken token)
        {
            // Default implementation to just press directional arrows. Can do via Hid keys, but users are slower than bots at even the default code entry.
            var keys = TradeUtil.GetPresses(code);
            foreach (var key in keys)
            {
                int delay = config.Timings.KeypressTime;
                await Click(key, delay, token).ConfigureAwait(false);
            }
            // Confirm Code outside of this method (allow synchronization)
        }

        public async Task ReOpenGame(PokeTradeHubConfig config, CancellationToken token)
        {
            await CloseGame(config, token).ConfigureAwait(false);
            await StartGame(config, token).ConfigureAwait(false);
        }

        public async Task CloseGame(PokeTradeHubConfig config, CancellationToken token)
        {
            var timing = config.Timings;
            // Close out of the game
            await Click(B, 0_500, token).ConfigureAwait(false);
            await Click(HOME, 2_000 + timing.ExtraTimeReturnHome, token).ConfigureAwait(false);
            await Click(X, 1_000, token).ConfigureAwait(false);
            await Click(A, 5_000 + timing.ExtraTimeCloseGame, token).ConfigureAwait(false);
            Log("Closed out of the game!");
        }

        public async Task StartGame(PokeTradeHubConfig config, CancellationToken token)
        {
            var timing = config.Timings;
            // Open game.
            await Click(A, 1_000 + timing.ExtraTimeLoadProfile, token).ConfigureAwait(false);

            // Menus here can go in the order: Update Prompt -> Profile -> DLC check -> Unable to use DLC.
            //  The user can optionally turn on the setting if they know of a breaking system update incoming.
            if (timing.AvoidSystemUpdate)
            {
                await Click(DUP, 0_600, token).ConfigureAwait(false);
                await Click(A, 1_000 + timing.ExtraTimeLoadProfile, token).ConfigureAwait(false);
            }

            await Click(A, 1_000 + timing.ExtraTimeCheckDLC, token).ConfigureAwait(false);
            // If they have DLC on the system and can't use it, requires an UP + A to start the game.
            // Should be harmless otherwise since they'll be in loading screen.
            await Click(DUP, 0_600, token).ConfigureAwait(false);
            await Click(A, 0_600, token).ConfigureAwait(false);

            Log("Restarting the game!");

            // Switch Logo and game load screen
            await Task.Delay(12_000 + timing.ExtraTimeLoadGame, token).ConfigureAwait(false);

            for (int i = 0; i < 8; i++)
                await Click(A, 1_000, token).ConfigureAwait(false);

            var timer = 60_000;
            while (!await IsOnOverworldTitle(token).ConfigureAwait(false))
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
                timer -= 1_000;
                // We haven't made it back to overworld after a minute, so press A every 6 seconds hoping to restart the game.
                // Don't risk it if hub is set to avoid updates.
                if (timer <= 0 && !timing.AvoidSystemUpdate)
                {
                    Log("Still not in the game, initiating rescue protocol!");
                    while (!await IsOnOverworldTitle(token).ConfigureAwait(false))
                        await Click(A, 6_000, token).ConfigureAwait(false);
                    break;
                }
            }

            await Task.Delay(5_000 + timing.ExtraTimeLoadOverworld, token).ConfigureAwait(false);
            Log("Back in the overworld!");
        }

        public async Task<bool> IsConnectedOnline(ulong offset, CancellationToken token)
        {
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
            return data[0] == 1;
        }

        public async Task<ulong> GetTradePartnerNID(ulong offset, CancellationToken token)
        {
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 8, token).ConfigureAwait(false);
            return BitConverter.ToUInt64(data, 0);
        }

        public async Task ClearTradePartnerNID(ulong offset, CancellationToken token)
        {
            var data = new byte[8];
            await SwitchConnection.WriteBytesAbsoluteAsync(data, offset, token).ConfigureAwait(false);
        }

        public async Task<bool> IsOnOverworld(ulong offset, CancellationToken token)
        {
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
            return data[0] == 0x11;
        }

        // Only used to check if we made it off the title screen; the pointer isn't viable until a few seconds after clicking A.
        private async Task<bool> IsOnOverworldTitle(CancellationToken token)
        {
            var (valid, offset) = await ValidatePointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
            if (!valid)
                return false;
            return await IsOnOverworld(offset, token).ConfigureAwait(false);
        }

        // Usually 0x9-0xA if fully loaded into Poké Portal.
        public async Task<bool> IsInPokePortal(ulong offset, CancellationToken token)
        {
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
            return data[0] >= 9;
        }

        // Usually 4-6 in a box.
        public async Task<bool> IsInBox(ulong offset, CancellationToken token)
        {
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
            return data[0] < 8;
        }

        public async Task<TextSpeedOption> GetTextSpeed(CancellationToken token)
        {
            var data = await SwitchConnection.PointerPeek(1, Offsets.ConfigPointer, token).ConfigureAwait(false);
            return (TextSpeedOption)(data[0] & 3);
        }


        // Zyro additions
        public async Task<ulong> GetPointerAddress(string pointer, CancellationToken token, bool heaprealtive = false) //Code from LiveHex
        {
            var ptr = pointer;
            if (string.IsNullOrWhiteSpace(ptr) || ptr.IndexOfAny(new char[] { '-', '/', '*' }) != -1)
                return 0;
            while (ptr.Contains("]]"))
                ptr = ptr.Replace("]]", "]+0]");
            uint finadd = 0;
            if (!ptr.EndsWith("]"))
            {
                finadd = Util.GetHexValue(ptr.Split('+').Last());
                ptr = ptr[..ptr.LastIndexOf('+')];
            }
            var jumps = ptr.Replace("main", "").Replace("[", "").Replace("]", "").Split(new[] { "+" }, StringSplitOptions.RemoveEmptyEntries);
            if (jumps.Length == 0)
                return 0;

            var initaddress = Util.GetHexValue(jumps[0].Trim());
            ulong address = BitConverter.ToUInt64(await SwitchConnection.ReadBytesMainAsync(initaddress, 0x8, token).ConfigureAwait(false), 0);
            foreach (var j in jumps)
            {
                var val = Util.GetHexValue(j.Trim());
                if (val == initaddress)
                    continue;
                address = BitConverter.ToUInt64(await SwitchConnection.ReadBytesAbsoluteAsync(address + val, 0x8, token).ConfigureAwait(false), 0);
            }
            address += finadd;
            if (heaprealtive)
            {
                ulong heap = await SwitchConnection.GetHeapBaseAsync(token).ConfigureAwait(false);
                address -= heap;
            }
            return address;
        }

        public async Task SetBoxPokemonEgg(PK9 pkm, int box, int slot, CancellationToken token)
        {
            var ofs = await GetPointerAddress("[[[main+44BFBA8]+130]+9B0]", token).ConfigureAwait(false);
            pkm.ResetPartyStats();
            await SwitchConnection.WriteBytesAbsoluteAsync(pkm.EncryptedPartyData, ofs, token).ConfigureAwait(false);
        }

        public async Task SVSaveGameOverworld(CancellationToken token)
        {
            Log("Saving the game...");
            await Click(X, 2_000, token).ConfigureAwait(false);
            await Click(R, 1_800, token).ConfigureAwait(false);
            await Click(A, 7_000, token).ConfigureAwait(false);
            await Click(B, 1_500, token).ConfigureAwait(false);
        }

        // Save Block Additions from TeraFinder/RaidCrawler/sv-livemap

        public class DataBlock
        {
            public string? Name { get; set; }
            public uint Key { get; set; }
            public SCTypeCode Type { get; set; }
            public SCTypeCode SubType { get; set; }
            public IReadOnlyList<long>? Pointer { get; set; }
            public bool IsEncrypted { get; set; }
            public int Size { get; set; }
        }

        public async Task<byte[]> ReadBlock(ulong baseBlock, DataBlock block, CancellationToken token)
        {
            return await ReadEncryptedBlock(baseBlock, block, token).ConfigureAwait(false);
        }

        private async Task<byte[]> ReadEncryptedBlock(ulong baseBlock, DataBlock block, CancellationToken token)
        {
            var address = await SearchSaveKey(baseBlock, block.Key, token).ConfigureAwait(false);
            address = BitConverter.ToUInt64(await SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
            var header = await SwitchConnection.ReadBytesAbsoluteAsync(address, 5, token).ConfigureAwait(false);
            header = DecryptBlock(block.Key, header);
            var size = ReadUInt32LittleEndian(header.AsSpan()[1..]);
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(address, 5 + (int)size, token).ConfigureAwait(false);
            var res = DecryptBlock(block.Key, data)[5..];

            return res;
        }

        public async Task<ulong> SearchSaveKey(ulong baseBlock, uint key, CancellationToken token)
        {
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(baseBlock + 8, 16, token).ConfigureAwait(false);
            var start = BitConverter.ToUInt64(data.AsSpan()[..8]);
            var end = BitConverter.ToUInt64(data.AsSpan()[8..]);

            while (start < end)
            {
                var block_ct = (end - start) / 48;
                var mid = start + (block_ct >> 1) * 48;

                data = await SwitchConnection.ReadBytesAbsoluteAsync(mid, 4, token).ConfigureAwait(false);
                var found = BitConverter.ToUInt32(data);
                if (found == key)
                    return mid;

                if (found >= key)
                    end = mid;
                else start = mid + 48;
            }
            return start;
        }

        private static byte[] DecryptBlock(uint key, byte[] block)
        {
            var rng = new SCXorShift32(key);
            for (int i = 0; i < block.Length; i++)
                block[i] = (byte)(block[i] ^ rng.Next());
            return block;
        }

        public static class Blocks
        {
            public static DataBlock Overworld = new()
            {
                Name = "Overworld",
                Key = 0x173304D8,
                Type = SCTypeCode.Object,
                IsEncrypted = true,
                Size = 2490,
            };
        }
    }
}
