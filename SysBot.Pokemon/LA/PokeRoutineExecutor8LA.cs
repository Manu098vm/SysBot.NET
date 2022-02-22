using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using SysBot.Base;
using static SysBot.Pokemon.PokeDataOffsetsLA;
using static SysBot.Base.SwitchButton;

namespace SysBot.Pokemon
{
    public abstract class PokeRoutineExecutor8LA : PokeRoutineExecutor<PA8>
    {
        protected PokeDataOffsetsLA Offsets { get; } = new();
        protected PokeRoutineExecutor8LA(PokeBotState cfg) : base(cfg)
        {
        }

        public override async Task<PA8> ReadPokemon(ulong offset, CancellationToken token) => await ReadPokemon(offset, BoxFormatSlotSize, token).ConfigureAwait(false);

        public override async Task<PA8> ReadPokemon(ulong offset, int size, CancellationToken token)
        {
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, size, token).ConfigureAwait(false);
            return new PA8(data);
        }

        public override async Task<PA8> ReadPokemonPointer(IEnumerable<long> jumps, int size, CancellationToken token)
        {
            var (valid, offset) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
            if (!valid)
                return new PA8();
            return await ReadPokemon(offset, token).ConfigureAwait(false);
        }

        public async Task<bool> ReadIsChanged(uint offset, byte[] original, CancellationToken token)
        {
            var result = await Connection.ReadBytesAsync(offset, original.Length, token).ConfigureAwait(false);
            return !result.SequenceEqual(original);
        }

        public override async Task<PA8> ReadBoxPokemon(int box, int slot, CancellationToken token)
        {
            // Shouldn't be reading anything but box1slot1 here. Slots are not consecutive.
            var jumps = Offsets.BoxStartPokemonPointer.ToArray();
            return await ReadPokemonPointer(jumps, BoxFormatSlotSize, token).ConfigureAwait(false);
        }

        public async Task SetBoxPokemonAbsolute(ulong offset, PA8 pkm, CancellationToken token, ITrainerInfo? sav = null)
        {
            if (sav != null)
            {
                // Update PKM to the current save's handler data
                DateTime Date = DateTime.Now;
                pkm.Trade(sav, Date.Day, Date.Month, Date.Year);
                pkm.RefreshChecksum();
            }

            pkm.ResetPartyStats();
            await SwitchConnection.WriteBytesAbsoluteAsync(pkm.EncryptedBoxData, offset, token).ConfigureAwait(false);
        }

        public async Task SetCurrentBox(byte box, CancellationToken token)
        {
            await SwitchConnection.PointerPoke(BitConverter.GetBytes(box), Offsets.CurrentBoxPointer, token).ConfigureAwait(false);
        }

        public async Task<byte> GetCurrentBox(CancellationToken token)
        {
            var data = await SwitchConnection.PointerPeek(1, Offsets.CurrentBoxPointer, token).ConfigureAwait(false);
            return data[0];
        }

        public async Task<SAV8LA> IdentifyTrainer(CancellationToken token)
        {
            // Check title so we can warn if mode is incorrect.
            string title = await SwitchConnection.GetTitleID(token).ConfigureAwait(false);
            if (title != LegendsArceusID)
                throw new Exception($"{title} is not a valid Pokémon Legends: Arceus title. Is your mode correct?");

            var sav = await GetFakeTrainerSAV(token).ConfigureAwait(false);
            InitSaveData(sav);

            if (!IsValidTrainerData())
                throw new Exception("Trainer data is not valid. Refer to the SysBot.NET wiki for bad or no trainer data.");
            if (await GetTextSpeed(token).ConfigureAwait(false) < TextSpeedOption.Fast)
                throw new Exception("Text speed should be set to FAST. Fix this for correct operation.");

            return sav;
        }

        public async Task<SAV8LA> GetFakeTrainerSAV(CancellationToken token)
        {
            var sav = new SAV8LA();
            var info = sav.MyStatus;
            var read = await SwitchConnection.PointerPeek(info.Data.Length, Offsets.MyStatusPointer, token).ConfigureAwait(false);
            read.CopyTo(info.Data, 0);
            return sav;
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

        public async Task CleanExit(IBotStateSettings settings, CancellationToken token)
        {
            if (settings.ScreenOff)
            {
                Log("Turning on screen.");
                await SetScreen(ScreenState.On, token).ConfigureAwait(false);
            }
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
            Log("Error detected, restarting the game!!");
            await CloseGame(config, token).ConfigureAwait(false);
            await StartGame(config, token).ConfigureAwait(false);
        }

        public async Task UnSoftBan(CancellationToken token)
        {
            Log("Soft ban detected, unbanning.");
            // Write the value to 0.
            var data = BitConverter.GetBytes(0);
            await SwitchConnection.PointerPoke(data, Offsets.SoftbanPointer, token).ConfigureAwait(false);
        }

        public async Task<bool> CheckIfSoftBanned(ulong offset, CancellationToken token)
        {
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 4, token).ConfigureAwait(false);
            return BitConverter.ToUInt32(data, 0) != 0;
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

        public async Task<ulong> GetTradePartnerNID(ulong offset, CancellationToken token)
        {
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 8, token).ConfigureAwait(false);
            return BitConverter.ToUInt64(data, 0);
        }

        public async Task<bool> IsOnOverworld(ulong offset, CancellationToken token)
        {
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
            return data[0] == 1;
        }

        // Only used to check if we made it off the title screen; the pointer isn't viable until a few seconds after clicking A.
        private async Task<bool> IsOnOverworldTitle(CancellationToken token)
        {
            var (valid, offset) = await ValidatePointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
            if (!valid)
                return false;
            return await IsOnOverworld(offset, token).ConfigureAwait(false);
        }

        public async Task<TextSpeedOption> GetTextSpeed(CancellationToken token)
        {
            var data = await SwitchConnection.PointerPeek(1, Offsets.TextSpeedPointer, token).ConfigureAwait(false);
            return (TextSpeedOption)data[0];
        }

        public async Task Sleep(CancellationToken token, bool gameClosed = false)
        {
            Log($"Sleep Mode Activated!");
            await PressAndHold(HOME, 2_000, 0, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);
            await DetachController(token).ConfigureAwait(false);
        }


        // Arceus Bot Additions

        public async Task<ulong> NewParsePointer(string pointer, CancellationToken token, bool heaprealtive = false) //Code from LiveHex
        {
            var ptr = pointer;
            if (string.IsNullOrWhiteSpace(ptr) || ptr.IndexOfAny(new char[] { '-', '/', '*' }) != -1)
                return 0;
            while (ptr.Contains("]]"))
                ptr = ptr.Replace("]]", "]+0]");
            uint? finadd = null;
            if (!ptr.EndsWith("]"))
            {
                finadd = Util.GetHexValue(ptr.Split('+').Last());
                ptr = ptr.Substring(0, ptr.LastIndexOf('+'));
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
            if (finadd != null) address += (ulong)finadd;
            if (heaprealtive)
            {
                ulong heap = await SwitchConnection.GetHeapBaseAsync(token);
                address -= heap;
            }
            return address;
        }
        public async Task ResetStick(CancellationToken token)
        {
            // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
            await SetStick(SwitchStick.LEFT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
        }

        public async Task ArceusSaveGame(CancellationToken token)
        {
            Log("Saving the game...");
            await Click(DUP, 1_000, token).ConfigureAwait(false);
            await Click(A, 1_800, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);
            await Click(B, 1_000, token).ConfigureAwait(false);
        }

        public (bool shiny, string shinyxor, ulong EC, ulong PID, int[] IVs, ulong ability, ulong gender, Nature nature, ulong shinyseed) GenerateFromSeed(ulong seed, int rolls, int guranteedivs)
        {
            bool shiny = false;
            ulong EC;
            ulong pid = 0;
            int[] ivs;
            ulong ability;
            ulong gender;
            Nature nature;
            ulong newseed = 0;
            uint shinyXor = 0;
            var rng = new Xoroshiro128Plus(seed);
            EC = rng.Next() & GetMask(0xFFFFFFFF);
            var sidtid = rng.Next() & GetMask(0xFFFFFFFF);
            for (int i = 0; i < rolls; i++)
            {
                pid = rng.Next() & GetMask(0xFFFFFFFF);
                shiny = ((pid >> 16) ^ (sidtid >> 16) ^ (pid & 0xFFFF) ^ (sidtid & 0xFFFF)) < 0x10;
                shinyXor = (uint)((pid & 0xFFFF) ^ (sidtid & 0xFFFF) ^ (pid >> 16) ^ (sidtid >> 16));
                if (shiny)
                {
                    newseed = rng.GetState().s0;
                    break;
                }
            }
            string shinytype = string.Empty;
            if (shinyXor == 0)
                shinytype = $"■ - Square Shiny";
            if (shinyXor != 0 && shinyXor <= 16)
                shinytype = $"★ - Star Shiny";

            ivs = new int[] { -1, -1, -1, -1, -1, -1 };
            for (int i = 0; i < guranteedivs; i++)
            {
                var index = rng.Next() & GetMask(6);
                while ((int)index >= 6)
                    index = rng.Next() & GetMask(6);


                while (ivs[index] != -1)
                {
                    index = rng.Next() & GetMask(6);
                    while ((int)index >= 6)
                        index = rng.Next() & GetMask(6);

                }

                ivs[index] = 31;
            }
            for (int i = 0; i < 6; i++)
            {
                if (ivs[i] == -1)
                    ivs[i] = (int)(rng.Next() & GetMask(32));
            }
            ability = rng.Next() & GetMask(2);
            gender = (rng.Next() & GetMask(252)) + 1;
            nature = (Nature)(rng.NextInt(25));
            return (shiny, shinytype, EC, pid, ivs, ability, gender, nature, newseed);
        }

        public uint GetMask(uint maximum)
        {
            maximum -= 1;
            for (int i = 0; i < 6; i++)
            {
                maximum |= maximum >> (1 << i);
            }
            return maximum;
        }
    }
}
