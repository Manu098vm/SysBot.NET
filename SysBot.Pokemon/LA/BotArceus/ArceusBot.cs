using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using System.Linq;
using System.Collections.Generic;

namespace SysBot.Pokemon
{
    public sealed class ArceusBot : PokeRoutineExecutor8LA
    {
        private readonly PokeTradeHub<PA8> Hub;
        private readonly IDumper DumpSetting;
        private readonly ArceusBotSettings Settings;

        public static CancellationTokenSource EmbedSource = new();
        public static bool EmbedsInitialized;
        public static (PA8?, string, byte[])? EmbedInfo;
        public ArceusBot(PokeBotState cfg, PokeTradeHub<PA8> hub) : base(cfg)
        {
            Hub = hub;
            Settings = Hub.Config.Arceus;
            DumpSetting = Hub.Config.Folder;
        }

        private ulong ofs;
        private ulong MainNsoBase;
        private ulong OverworldOffset;
        private int alphacount;
        public int SpawnerID;
        string SpawnerSpecies = string.Empty;
        string x = string.Empty;
        string y = string.Empty;
        string z = string.Empty;

        public static readonly string[] ObsidianTitle =
        {
            "Rapidash","Snorlax","Luxio","Floatzel","Staravia","Parasect","Kricketune","Stantler","Bibarel","Scyther","Lopunny","Graveler","Blissey","Heracross","Magikarp","Infernape","Alakazam","Gyarados",
        };

        public static readonly string[] CrimsonTitle =
        {
            "Tangrowth","Hippowdon","Skuntank","Onix","Rhyhorn","Honchkrow","Roserade","Lickilicky","Pachirisu","Carnivine","Vespiquen","Yanmega","Ursaring","Toxicroak","Torterra","Sliggoo","Raichu","Ursaring","Whiscash",
        };

        public static readonly string[] CoronetTitle =
        {
            "Mothim","Bronzong","Carnivine","Gligar","Gabite","Luxray","Electivire","Goodra","Steelix","Clefable","Golem","Mismagius","Rhyperior","Probopass","Gliscor",
        };

        public static readonly string[] CobaltTitle =
        {
            "Walrein","Drapion","Purugly","Ambipom","Golduck","Dusknoir","Machoke","Octillery","Mantine","Tentacruel","Ninetales","Chansey","Lumineon","Gyarados","Gastrodon","Qwilfish","Empoleon","Mothim",
        };

        public static readonly string[] AlabasterTitle =
        {
            "Glalie","Abomasnow","Mamoswine","Gardevoir","Sneasel","Chimecho","Machamp","Swinub","Piloswine","Lucario","Electabuzz","Froslass","Garchomp",
        };

        public override async Task MainLoop(CancellationToken token)
        {
            Log("Identifying trainer data of the host console.");
            await IdentifyTrainer(token).ConfigureAwait(false);
            await InitializeHardware(Settings, token).ConfigureAwait(false);
            try
            {
                Log($"Starting main {GetType().Name} loop.");
                Config.IterateNextRoutine();

                // Clear out any residual stick weirdness.
                await ResetStick(token).ConfigureAwait(false);
                MainNsoBase = await SwitchConnection.GetMainNsoBaseAsync(token).ConfigureAwait(false);
                var task = Hub.Config.Arceus.BotType switch
                {
                    ArceusMode.PlayerCoordScan => PlayerCoordScan(token),
                    ArceusMode.SeedAdvancer => SeedAdvancer(token),
                    ArceusMode.TimeSeedAdvancer => TimeSeedAdvancer(token),
                    ArceusMode.StaticAlphaScan => ScanForAlphas(token),
                    ArceusMode.OutbreakHunter => OutbreakHunter(token),
                    ArceusMode.DistortionSpammer => DistortionSpammer(token),
                    _ => PlayerCoordScan(token),
                };
                await task.ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Log(e.Message);
            }

            Log($"Ending {GetType().Name} loop.");
            await HardStop().ConfigureAwait(false);
        }

        public override async Task HardStop()
        {
            await ResetStick(CancellationToken.None).ConfigureAwait(false);
            await CleanExit(Settings, CancellationToken.None).ConfigureAwait(false);
        }

        public async Task TimeTest(CancellationToken token)
        {
            var timeofs = await NewParsePointer("[[[[main+42963A0]+18]+100]+18]+28", token).ConfigureAwait(false);
            var timeVal = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(timeofs, 2, token).ConfigureAwait(false), 0);
            if (timeVal <= 7)
                timeVal = 15;
            else timeVal = 00;
            byte[] timeByte = BitConverter.GetBytes(timeVal);
            await SwitchConnection.WriteBytesAbsoluteAsync(timeByte, timeofs, token).ConfigureAwait(false);
        }

        private async Task PlayerCoordScan(CancellationToken token)
        {
            var ofs = await NewParsePointer("[[[[[[main+42B3558]+88]+90]+1F0]+18]+80]+90", token).ConfigureAwait(false);
            var coord = BitConverter.ToUInt32(await SwitchConnection.ReadBytesAbsoluteAsync(ofs, 4, token).ConfigureAwait(false), 0);
            var coord2 = BitConverter.ToUInt32(await SwitchConnection.ReadBytesAbsoluteAsync(ofs + 0x4, 4, token).ConfigureAwait(false), 0);
            var coord3 = BitConverter.ToUInt32(await SwitchConnection.ReadBytesAbsoluteAsync(ofs + 0x8, 4, token).ConfigureAwait(false), 0);

            Log($"Current Player Coords - X: {coord:X8} Y: {coord2:X8} Z: {coord3:X8}");

            if (Settings.AutoFillCoords == ArceusAutoFill.CampZone)
            {
                Log($"Autofilling CampZone XYZ");
                Settings.SpecialConditions.CampZoneX = $"{coord:X8}";
                Settings.SpecialConditions.CampZoneY = $"{coord2:X8}";
                Settings.SpecialConditions.CampZoneZ = $"{coord3:X8}";
            }

            if (Settings.AutoFillCoords == ArceusAutoFill.SpawnZone)
            {
                Log($"Autofilling SpawnZone XYZ");
                Settings.SpecialConditions.SpawnZoneX = $"{coord:X8}";
                Settings.SpecialConditions.SpawnZoneY = $"{coord2:X8}";
                Settings.SpecialConditions.SpawnZoneZ = $"{coord3:X8}";
            }
        }

        private async Task Reposition(CancellationToken token)
        {
            uint offset = 0x04296764;
            var menucheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(offset, 2, token).ConfigureAwait(false), 0);
            while (!token.IsCancellationRequested)
            {
                Log("Not in camp, repositioning and trying again.");
                await TeleportToCampZone(token);
                for (int i = 0; i < 2; i++)
                    await Click(B, 0_500, token).ConfigureAwait(false);

                await Click(A, 1_000, token).ConfigureAwait(false);

                menucheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(offset, 2, token).ConfigureAwait(false), 0);
                if (menucheck == 66)
                    return;

                Log("Attempting face up");
                await TeleportToCampZone(token);
                for (int i = 0; i < 2; i++)
                    await Click(B, 0_500, token).ConfigureAwait(false);
                await SetStick(LEFT, 0, 5_000, 0_500, token).ConfigureAwait(false); // reset face forward
                await ResetStick(token).ConfigureAwait(false); // reset
                for (int i = 0; i < 2; i++)
                    await Click(B, 0_500, token).ConfigureAwait(false);

                await Click(A, 1_000, token).ConfigureAwait(false);

                menucheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(offset, 2, token).ConfigureAwait(false), 0);
                if (menucheck == 66)
                    return;

                Log("Attempting face down");
                await TeleportToCampZone(token);
                for (int i = 0; i < 2; i++)
                    await Click(B, 0_500, token).ConfigureAwait(false);
                await SetStick(LEFT, 0, -5_000, 0_500, token).ConfigureAwait(false); // reset face forward
                await ResetStick(token).ConfigureAwait(false); // reset                
                for (int i = 0; i < 2; i++)
                    await Click(B, 0_500, token).ConfigureAwait(false);

                await Click(A, 1_000, token).ConfigureAwait(false);

                menucheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(offset, 2, token).ConfigureAwait(false), 0);
                if (menucheck == 66)
                    return;
                Log("Attempting face right");
                await TeleportToCampZone(token);
                for (int i = 0; i < 2; i++)
                    await Click(B, 0_500, token).ConfigureAwait(false);
                await SetStick(LEFT, 5_000, 0, 0_500, token).ConfigureAwait(false); // reset face forward
                await ResetStick(token).ConfigureAwait(false); // reset
                for (int i = 0; i < 2; i++)
                    await Click(B, 0_500, token).ConfigureAwait(false);

                await Click(A, 1_000, token).ConfigureAwait(false);

                menucheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(offset, 2, token).ConfigureAwait(false), 0);
                if (menucheck == 66)
                    return;
                Log("Attempting face left");
                await TeleportToCampZone(token);
                for (int i = 0; i < 2; i++)
                    await Click(B, 0_500, token).ConfigureAwait(false);
                await SetStick(LEFT, -5_000, 0, 0_500, token).ConfigureAwait(false); // reset face forward
                await ResetStick(token).ConfigureAwait(false); // reset
                for (int i = 0; i < 2; i++)
                    await Click(B, 0_500, token).ConfigureAwait(false);

                await Click(A, 1_000, token).ConfigureAwait(false);

                menucheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(offset, 2, token).ConfigureAwait(false), 0);
                if (menucheck == 66)
                    return;
            }
        }

        public async Task DistortionSpammer(CancellationToken token)
        {
            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0x7100052A), MainNsoBase + 0x024672A4, token).ConfigureAwait(false);
            while (!token.IsCancellationRequested)
            {
                Log("Whipping up a distortion...");
                // Activates distortions
                await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0x5280010A), MainNsoBase + 0x024672A4, token).ConfigureAwait(false);
                await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0x7100052A), MainNsoBase + 0x024672A4, token).ConfigureAwait(false);
                var delta = DateTime.Now;
                var cd = TimeSpan.FromMinutes(Settings.SpecialConditions.WaitTimeDistortion);
                Log($"Waiting {Settings.SpecialConditions.WaitTimeDistortion} minutes then starting the next one...");
                while (DateTime.Now - delta < cd)
                {
                    await Task.Delay(5_000).ConfigureAwait(false);
                    Log($"Elapsed Time: {DateTime.Now - delta}");
                }

                await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0x5280010A), MainNsoBase + 0x024672A4, token).ConfigureAwait(false);
                await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0x7100052A), MainNsoBase + 0x024672A4, token).ConfigureAwait(false);
                await Task.Delay(5_000).ConfigureAwait(false);
            }
        }

        public async Task TimeSeedAdvancer(CancellationToken token)
        {
            int success = 0;
            string[] coords = { Settings.SpecialConditions.SpawnZoneX, Settings.SpecialConditions.SpawnZoneY, Settings.SpecialConditions.SpawnZoneZ };
            for (int a = 0; a < coords.Length; a++)
            {
                if (string.IsNullOrEmpty(coords[a]))
                {
                    Log($"One of your coordinates is empty, please fill it accordingly!");
                    return;
                }
            }
            OverworldOffset = await NewParsePointer($"main+4284E78]+1A9", token).ConfigureAwait(false);
            // Activates invcincible trainer cheat so we don't faint from teleporting or a Pokemon attacking and infinite PP
            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0xD65F03C0), MainNsoBase + 0x024B02E4, token).ConfigureAwait(false);
            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0xD65F03C0), MainNsoBase + 0x024B04EC, token).ConfigureAwait(false);

            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0xD28008AA), MainNsoBase + 0x007AB30C, token).ConfigureAwait(false);
            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0xD28008A9), MainNsoBase + 0x007AB31C, token).ConfigureAwait(false);

            while (!token.IsCancellationRequested)
            {
                string alpha = string.Empty;
                for (int adv = 0; adv < Settings.Advances; adv++)
                {
                    Log($"Advancing {Settings.Advances - adv} times...");
                    await Click(B, 1_000, token).ConfigureAwait(false);// Random B incase of button miss
                    await Click(A, 1_000, token).ConfigureAwait(false);
                    await TimeTest(token).ConfigureAwait(false);
                    await Task.Delay(2_000).ConfigureAwait(false);
                    if (Settings.AlphaScanConditions.SpawnIsStaticAlpha)
                    {
                        await TimeTest(token).ConfigureAwait(false);
                        await Task.Delay(2_000).ConfigureAwait(false);
                        await TimeTest(token).ConfigureAwait(false);
                        await Task.Delay(2_000).ConfigureAwait(false);
                        await TimeTest(token).ConfigureAwait(false);
                        await Task.Delay(2_000).ConfigureAwait(false);
                    }
                    await TeleportToSpawnZone(token).ConfigureAwait(false);
                    Log("Trying to enter battle!");
                    await PressAndHold(ZL, 1_000, 0, token).ConfigureAwait(false);
                    await Click(ZR, 2_000, token).ConfigureAwait(false);
                    await ResetStick(token).ConfigureAwait(false);
                    var overworldcheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(OverworldOffset, 2, token).ConfigureAwait(false), 0);
                    if (overworldcheck == 1)
                    {
                        Log("Not in battle, trying again!");
                        int tries = 0;
                        while (overworldcheck == 1)
                        {
                            if (tries == 20)
                            {
                                Log("Tried 20 times, is the encounter present? Changing time to try again.");
                                await TimeTest(token).ConfigureAwait(false);
                                await TeleportToSpawnZone(token).ConfigureAwait(false);
                                break;
                            }
                            await PressAndHold(ZL, 0_800, 0, token).ConfigureAwait(false);
                            await Click(ZR, 1_000, token).ConfigureAwait(false);
                            await ResetStick(token).ConfigureAwait(false); // reset
                            overworldcheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(OverworldOffset, 2, token).ConfigureAwait(false), 0);
                            if (overworldcheck != 1)
                                break;
                            await SetStick(LEFT, 0, -5_000, 0_500, token).ConfigureAwait(false); // turn around check encounter
                            await ResetStick(token).ConfigureAwait(false); // reset
                            await Click(ZL, 1_000, token).ConfigureAwait(false);
                            overworldcheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(OverworldOffset, 2, token).ConfigureAwait(false), 0);
                            if (overworldcheck != 1)
                                break;
                            await PressAndHold(ZL, 0_800, 0, token).ConfigureAwait(false);
                            await Click(ZR, 1_000, token).ConfigureAwait(false);
                            await ResetStick(token).ConfigureAwait(false); // reset
                            overworldcheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(OverworldOffset, 2, token).ConfigureAwait(false), 0);
                            if (overworldcheck != 1)
                                break;
                            await SetStick(LEFT, 0, 5_000, 0_500, token).ConfigureAwait(false); // turn around check encounter
                            await ResetStick(token).ConfigureAwait(false); // reset
                            await Click(ZL, 1_000, token).ConfigureAwait(false);
                            tries++;
                        }
                        tries = 0;
                    }
                    if (overworldcheck == 0)
                    {
                        ulong ofs = await NewParsePointer($"[[[[[main+4268F00]+D0]+B8]+300]+70]+60]+98]+10]", token).ConfigureAwait(false);
                        var pk = await ReadPokemon(ofs, 0x168, token).ConfigureAwait(false);
                        if (pk != null)
                        {
                            success++;
                            var print = Hub.Config.StopConditions.GetAlphaPrintName(pk);
                            if (pk.IsAlpha) alpha = "Alpha - ";
                            if (pk.IsShiny)
                            {
                                Log($"In battle with {print}!");
                                string match = $"A match has been found!\nIn battle with {print}";
                                var arr = Connection.Screengrab(token).Result.ToArray();
                                EmbedInfo = new(pk, match, arr);

                                if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
                                    DumpPokemon(DumpSetting.DumpFolder, "advances", pk);

                                return;
                            }
                            Log($"Mashing A to knockout {alpha}{(Species)pk.Species}!");
                            while (overworldcheck != 1 && pk != null)
                            {
                                overworldcheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(OverworldOffset, 2, token).ConfigureAwait(false), 0);
                                pk = await ReadPokemon(ofs, 0x168, token).ConfigureAwait(false);
                                for (int i = 0; i < 3; i++)
                                    await Click(A, 0_500, token).ConfigureAwait(false);

                                if (overworldcheck == 1 || pk == null)
                                    break;
                            }
                            Log($"Defeated {alpha}{(Species)pk.Species}! Returning to spawn point.");
                            alpha = string.Empty;
                        }
                    }
                    if (adv == Settings.Advances)
                        return;
                    if (success == 50)
                    {
                        Log("Saving in case of a game crash!");
                        await ArceusSaveGame(token).ConfigureAwait(false);
                        success = 0;
                    }
                }
            }
            Log("Advances reached! Shiny should be on the next respawn! Refer to the live map if unsure of frame!");
            await Click(HOME, 1_000, token).ConfigureAwait(false);
        }
        public async Task SeedAdvancer(CancellationToken token)
        {
            string[] coords = { Settings.SpecialConditions.CampZoneX, Settings.SpecialConditions.CampZoneY, Settings.SpecialConditions.CampZoneZ, Settings.SpecialConditions.SpawnZoneX, Settings.SpecialConditions.SpawnZoneY, Settings.SpecialConditions.SpawnZoneZ };
            for (int a = 0; a < coords.Length; a++)
            {
                if (string.IsNullOrEmpty(coords[a]))
                {
                    Log($"One of your coordinates is empty, please fill it accordingly!");
                    return;
                }
            }
            uint offset = 0x04296764;
            OverworldOffset = await NewParsePointer($"main+4284E78]+1A9", token).ConfigureAwait(false);
            // Activates invcincible trainer cheat so we don't faint from teleporting or a Pokemon attacking
            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0xD65F03C0), MainNsoBase + 0x024B02E4, token).ConfigureAwait(false);
            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0xD65F03C0), MainNsoBase + 0x024B04EC, token).ConfigureAwait(false);

            while (!token.IsCancellationRequested)
            {
                await GetDefaultCampCoords(token).ConfigureAwait(false);
                string alpha = string.Empty;
                for (int adv = 0; adv < Settings.Advances; adv++)
                {
                    Log($"Advancing {Settings.Advances - adv} times...");
                    await Click(B, 1_000, token).ConfigureAwait(false);// Random B incase of button miss
                    await Click(A, 1_000, token).ConfigureAwait(false);
                    var menucheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(offset, 2, token).ConfigureAwait(false), 0);
                    Log($"Menu check: {menucheck}");
                    if (menucheck == 66)
                        await Click(A, 0_800, token).ConfigureAwait(false);
                    while (menucheck == 64 || menucheck == 0)
                    {
                        Log("Wrong menu opened? Backing out now and trying to reposition.");
                        await Click(B, 1_500, token).ConfigureAwait(false);
                        await Reposition(token).ConfigureAwait(false);
                        await Click(B, 1_500, token).ConfigureAwait(false);
                        menucheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(offset, 2, token).ConfigureAwait(false), 0);
                        if (menucheck == 66)
                            break;
                    }
                    if (adv % 2 == 0)
                        await Click(DDOWN, 0_500, token).ConfigureAwait(false);
                    if (adv % 2 != 0)
                    {
                        await Click(DUP, 0_500, token).ConfigureAwait(false);
                        await Click(DUP, 0_500, token).ConfigureAwait(false);
                    }
                    Log("Resting...");
                    await Click(A, 12_000, token).ConfigureAwait(false);
                    Log("Rested once!");
                    await Click(A, 1_000, token).ConfigureAwait(false);
                    if (Settings.AlphaScanConditions.SpawnIsStaticAlpha)
                    {
                        // Rest a second time for Alphas
                        await SetStick(LEFT, 0, 10_000, 0_500, token).ConfigureAwait(false); // reset face forward
                        for (int i = 0; i < 2; i++)
                            await Click(A, 1_000, token).ConfigureAwait(false);
                        if (adv % 2 != 0)
                            await Click(DDOWN, 0_500, token).ConfigureAwait(false);
                        if (adv % 2 == 0)
                        {
                            for (int i = 0; i < 2; i++)
                                await Click(DUP, 0_500, token).ConfigureAwait(false);
                        }
                        Log("Resting...");
                        await Click(A, 12_000, token).ConfigureAwait(false);
                        Log("Rested twice!");
                        await Click(A, 1_000, token).ConfigureAwait(false);
                        // Rest a third time for Alphas
                        await SetStick(LEFT, 0, 10_000, 0_500, token).ConfigureAwait(false); // reset face forward
                        await ResetStick(token).ConfigureAwait(false);
                        for (int i = 0; i < 2; i++)
                            await Click(A, 1_000, token).ConfigureAwait(false);
                        if (adv % 2 == 0)
                            await Click(DDOWN, 0_500, token).ConfigureAwait(false);
                        if (adv % 2 != 0)
                        {
                            await Click(DUP, 0_500, token).ConfigureAwait(false);
                            await Click(DUP, 0_500, token).ConfigureAwait(false);
                        }
                        Log("Resting...");
                        await Click(A, 12_000, token).ConfigureAwait(false);
                        Log("Rested thrice!");
                        await Click(A, 1_000, token).ConfigureAwait(false);
                        //Night spawn only
                        if (Settings.AlphaScanConditions.NightSpawn)
                        {
                            await SetStick(LEFT, 0, 10_000, 0_500, token).ConfigureAwait(false); // reset face forward
                            await ResetStick(token).ConfigureAwait(false);
                            for (int i = 0; i < 2; i++)
                                await Click(A, 1_000, token).ConfigureAwait(false);
                            if (adv % 2 != 0)
                                await Click(DDOWN, 0_500, token).ConfigureAwait(false);
                            if (adv % 2 == 0)
                            {
                                await Click(DUP, 0_500, token).ConfigureAwait(false);
                                await Click(DUP, 0_500, token).ConfigureAwait(false);
                            }
                            Log("Resting...");
                            await Click(A, 12_000, token).ConfigureAwait(false);
                            Log("Rested for the night spawn!");
                            await Click(A, 1_000, token).ConfigureAwait(false);
                        }

                    }
                    await Click(DUP, 1_000, token).ConfigureAwait(false);
                    await SetStick(LEFT, 0, 10_000, 0_500, token).ConfigureAwait(false); // reset face forward
                    await ResetStick(token).ConfigureAwait(false);
                    await TeleportToSpawnZone(token).ConfigureAwait(false);
                    for (int i = 0; i < 2; i++)
                        await Click(B, 1_000, token).ConfigureAwait(false);

                    if (Settings.AlphaScanConditions.IsSpawnInWater)
                        await Click(PLUS, 1_000, token).ConfigureAwait(false);

                    Log("Trying to enter battle!");
                    await PressAndHold(ZL, 1_000, 0, token).ConfigureAwait(false);
                    await Click(ZR, 2_000, token).ConfigureAwait(false);
                    await ResetStick(token).ConfigureAwait(false);
                    var overworldcheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(OverworldOffset, 2, token).ConfigureAwait(false), 0);
                    if (overworldcheck == 1)
                    {
                        Log("Not in battle, trying again!");
                        int tries = 0;
                        while (overworldcheck == 1)
                        {
                            if (tries == 20)
                            {
                                Log("Tried 20 times, is the encounter present? Going back to camp to try again.");
                                await TeleportToCampZone(token).ConfigureAwait(false);
                                break;
                            }
                            await PressAndHold(ZL, 0_800, 0, token).ConfigureAwait(false);
                            await Click(ZR, 1_000, token).ConfigureAwait(false);
                            await ResetStick(token).ConfigureAwait(false); // reset
                            overworldcheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(OverworldOffset, 2, token).ConfigureAwait(false), 0);
                            if (overworldcheck != 1)
                                break;
                            await SetStick(LEFT, 0, -5_000, 0_500, token).ConfigureAwait(false); // turn around check encounter
                            await ResetStick(token).ConfigureAwait(false); // reset
                            await Click(ZL, 1_000, token).ConfigureAwait(false);
                            overworldcheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(OverworldOffset, 2, token).ConfigureAwait(false), 0);
                            if (overworldcheck != 1)
                                break;
                            await PressAndHold(ZL, 0_800, 0, token).ConfigureAwait(false);
                            await Click(ZR, 1_000, token).ConfigureAwait(false);
                            await ResetStick(token).ConfigureAwait(false); // reset
                            overworldcheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(OverworldOffset, 2, token).ConfigureAwait(false), 0);
                            if (overworldcheck != 1)
                                break;
                            await SetStick(LEFT, 0, 5_000, 0_500, token).ConfigureAwait(false); // turn around check encounter
                            await ResetStick(token).ConfigureAwait(false); // reset
                            await Click(ZL, 1_000, token).ConfigureAwait(false);
                            tries++;
                        }
                        tries = 0;
                    }
                    if (overworldcheck == 0)
                    {
                        ulong ofs = await NewParsePointer($"[[[[[main+4268F00]+D0]+B8]+300]+70]+60]+98]+10]", token).ConfigureAwait(false);
                        var pk = await ReadPokemon(ofs, 0x168, token).ConfigureAwait(false);
                        if (pk != null)
                        {
                            var print = Hub.Config.StopConditions.GetAlphaPrintName(pk);
                            if (pk.IsAlpha) alpha = "Alpha - ";
                            if (pk.IsShiny)
                            {
                                Log($"In battle with {print}!");
                                string match = $"A match has been found!\nIn battle with {print}";
                                var arr = Connection.Screengrab(token).Result.ToArray();
                                EmbedInfo = new(pk, match, arr);

                                if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
                                    DumpPokemon(DumpSetting.DumpFolder, "advances", pk);

                                return;
                            }
                            Log($"Mashing A to knockout {alpha}{(Species)pk.Species}!");
                            while (overworldcheck != 1)
                            {
                                overworldcheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(OverworldOffset, 2, token).ConfigureAwait(false), 0);
                                await Click(A, 1_000, token).ConfigureAwait(false);
                            }
                            Log($"Defeated {alpha}{(Species)pk.Species}! Returning to spawn point.");
                            alpha = string.Empty;
                        }
                    }
                    Log($"Returning to camp...");
                    if (Settings.AlphaScanConditions.IsSpawnInWater)
                        await Click(PLUS, 1_000, token).ConfigureAwait(false);

                    await TeleportToCampZone(token);
                    await SetStick(LEFT, 0, 5_000, 0_500, token).ConfigureAwait(false); // reset face forward
                    await ResetStick(token).ConfigureAwait(false); // reset
                    await ArceusSaveGame(token).ConfigureAwait(false);
                }
            }
            Log("Advances reached! Shiny should be on the next respawn! Refer to the live map if unsure of frame!");
            await Click(HOME, 1_000, token).ConfigureAwait(false);
        }

        private async Task GetDefaultCoords(CancellationToken token)
        {
            var mode = Settings.ScanLocation;
            switch (mode)
            {
                case ArceupMap.ObsidianFieldlands: x = "43AC18AC"; y = "4242DEC9"; z = "4305C37C"; break;
                case ArceupMap.CrimsonMirelands: x = "436208ED"; y = "425FD612"; z = "43DBCD87"; break;
                case ArceupMap.CoronetHighlands: x = "44636F1B"; y = "420FDC78"; z = "446A1DE5"; break;
                case ArceupMap.CobaltCoastlands: x = "425E5D04"; y = "4237E63D"; z = "44207689"; break;
                case ArceupMap.AlabasterIcelands: x = "440B8982"; y = "41F37461"; z = "4467EAF7"; break;
            }

            Settings.SpecialConditions.SpawnZoneX = "4408C60D";
            Settings.SpecialConditions.SpawnZoneY = "4270D7B3";
            Settings.SpecialConditions.SpawnZoneZ = "43E52E0D";

            await Task.Delay(0_100).ConfigureAwait(false);

            Settings.SpecialConditions.CampZoneX = x;
            Settings.SpecialConditions.CampZoneY = y;
            Settings.SpecialConditions.CampZoneZ = z;
        }

        private async Task GetDefaultCampCoords(CancellationToken token)
        {
            var mode = Settings.ScanLocation;
            switch (mode)
            {
                case ArceupMap.ObsidianFieldlands: x = "43B7F23A"; y = "424FF99B"; z = "4308306A"; break;
                case ArceupMap.CrimsonMirelands: x = "43751CD5"; y = "425E7B13"; z = "43D92BA9"; break;
                case ArceupMap.CoronetHighlands: x = "445E8412"; y = "4211C885"; z = "4466973A"; break;
                case ArceupMap.CobaltCoastlands: x = "4291B987"; y = "4234AEB4"; z = "441BF96B"; break;
                case ArceupMap.AlabasterIcelands: x = "4404BA24"; y = "41F91B54"; z = "446417D3"; break;
            }
            await Task.Delay(0_100).ConfigureAwait(false);

            Settings.SpecialConditions.CampZoneX = x;
            Settings.SpecialConditions.CampZoneY = y;
            Settings.SpecialConditions.CampZoneZ = z;
        }

        private async Task ScanForAlphas(CancellationToken token)
        {
            Settings.StopOnMatch = false;
            int attempts = 1;
            for (int i = 0; i < 2; i++)
                await Click(B, 0_500, token).ConfigureAwait(false);
            Log("Searching for an Alpha shiny!");
            await GetDefaultCoords(token);
            await TeleportToCampZone(token);
            while (!token.IsCancellationRequested)
            {
                Log($"Search #{attempts}");
                await SpawnerScan(token);
                if (Settings.StopOnMatch)
                {
                    Log($"{Hub.Config.StopConditions.MatchFoundEchoMention} a match has been found!");
                    return;
                }
                await TeleportToCampZone(token);
                await SetStick(LEFT, -30_000, 0, 1_000, token).ConfigureAwait(false); // reset face forward
                await ResetStick(token).ConfigureAwait(false); // reset
                await Click(A, 1_000, token).ConfigureAwait(false);
                await Click(A, 1_000, token).ConfigureAwait(false);
                await Click(A, 10_000, token).ConfigureAwait(false);
                await TeleportToSpawnZone(token);
                await SetStick(LEFT, 0, -30_000, 1_000, token).ConfigureAwait(false); // reset face downward
                await ResetStick(token).ConfigureAwait(false); // reset
                for (int i = 0; i < 2; i++)
                    await Click(A, 1_000, token).ConfigureAwait(false);
                await Click(A, 10_000, token).ConfigureAwait(false);
                attempts++;
            }
        }

        public async Task TeleportToCampZone(CancellationToken token)
        {
            ofs = await NewParsePointer($"[[[[[[main+42B3558]+88]+90]+1F0]+18]+80]+90", token).ConfigureAwait(false);
            uint coordX1 = uint.Parse(Settings.SpecialConditions.CampZoneX, System.Globalization.NumberStyles.AllowHexSpecifier);
            byte[] X1 = BitConverter.GetBytes(coordX1);
            uint coordY1 = uint.Parse(Settings.SpecialConditions.CampZoneY, System.Globalization.NumberStyles.AllowHexSpecifier);
            byte[] Y1 = BitConverter.GetBytes(coordY1);
            uint coordZ1 = uint.Parse(Settings.SpecialConditions.CampZoneZ, System.Globalization.NumberStyles.AllowHexSpecifier);
            byte[] Z1 = BitConverter.GetBytes(coordZ1);

            await SwitchConnection.WriteBytesAbsoluteAsync(X1, ofs, token).ConfigureAwait(false);
            await SwitchConnection.WriteBytesAbsoluteAsync(Y1, ofs + 0x4, token).ConfigureAwait(false);
            await SwitchConnection.WriteBytesAbsoluteAsync(Z1, ofs + 0x8, token).ConfigureAwait(false);

            await Task.Delay(Settings.SpecialConditions.WaitMsBetweenTeleports, token).ConfigureAwait(false);
        }

        public async Task TeleportToSpawnZone(CancellationToken token)
        {
            ofs = await NewParsePointer($"[[[[[[main+42B3558]+88]+90]+1F0]+18]+80]+90", token).ConfigureAwait(false);
            uint coordX1 = uint.Parse(Settings.SpecialConditions.SpawnZoneX, System.Globalization.NumberStyles.AllowHexSpecifier);
            byte[] X1 = BitConverter.GetBytes(coordX1);
            uint coordY1 = uint.Parse(Settings.SpecialConditions.SpawnZoneY, System.Globalization.NumberStyles.AllowHexSpecifier);
            byte[] Y1 = BitConverter.GetBytes(coordY1);
            uint coordZ1 = uint.Parse(Settings.SpecialConditions.SpawnZoneZ, System.Globalization.NumberStyles.AllowHexSpecifier);
            byte[] Z1 = BitConverter.GetBytes(coordZ1);

            await SwitchConnection.WriteBytesAbsoluteAsync(X1, ofs, token).ConfigureAwait(false);
            await SwitchConnection.WriteBytesAbsoluteAsync(Y1, ofs + 0x4, token).ConfigureAwait(false);
            await SwitchConnection.WriteBytesAbsoluteAsync(Z1, ofs + 0x8, token).ConfigureAwait(false);

            await Task.Delay(Settings.SpecialConditions.WaitMsBetweenTeleports, token).ConfigureAwait(false);
        }

        private async Task SpawnerScan(CancellationToken token)
        {
            await Task.Delay(0_500).ConfigureAwait(false);
            Log($"Starting Alpha Scanner for {Settings.ScanLocation}...");
            var mode = Settings.ScanLocation;
            switch (mode)
            {
                case ArceupMap.ObsidianFieldlands: alphacount = 18; break;
                case ArceupMap.CrimsonMirelands: alphacount = 19; break;
                case ArceupMap.CoronetHighlands: alphacount = 15; break;
                case ArceupMap.CobaltCoastlands: alphacount = 18; break;
                case ArceupMap.AlabasterIcelands: alphacount = 13; break;
            }
            for (int i = 0; i < alphacount; i++)
            {
                var SpawnerOffpoint = new long[] { 0x4268ee0, 0x330, 0x70 + i * 0x440 + 0x20 };
                var SpawnerOff = SwitchConnection.PointerAll(SpawnerOffpoint, token).Result;
                var GeneratorSeed = SwitchConnection.ReadBytesAbsoluteAsync(SpawnerOff, 8, token).Result;
                Log($"Generator Seed: {BitConverter.ToString(GeneratorSeed).Replace("-", "")}");
                var group_seed = (BitConverter.ToUInt64(GeneratorSeed, 0) - 0x82A2B175229D6A5B) & 0xFFFFFFFFFFFFFFFF;
                Log($"Group Seed: {string.Format("0x{0:X}", group_seed)}");
                SpawnerID = i;
                GenerateNextShiny(SpawnerSpecies, i, group_seed);
            }
        }

        public async Task OutbreakHunter(CancellationToken token)
        {
            int attempts = 1;
            await GetDefaultCoords(token);
            Log("Reading map for active outbreaks...");
            while (!token.IsCancellationRequested)
            {
                Log($"Search #{attempts}");
                await TeleportToSpawnZone(token);
                await SetStick(LEFT, 0, -30_000, 1_000, token).ConfigureAwait(false);
                await ResetStick(token).ConfigureAwait(false); // reset

                for (int i = 0; i < 2; i++)
                    await Click(A, 1_000, token).ConfigureAwait(false);
                for (int i = 0; i < 4; i++)
                {
                    ofs = await NewParsePointer($"[[[[main+427C470]+2B0]+58]+18]+{20 + (i * 50)}", token).ConfigureAwait(false);
                    Species species = (Species)BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(ofs, 2, token).ConfigureAwait(false), 0);
                    if (species != Species.None)
                    {
                        var spawncount = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(ofs + 0x40, 2, token).ConfigureAwait(false), 0);
                        Log($"Outbreak found for: {species} | Total Spawn Count: {spawncount}.");
                        if (Settings.SpecialConditions.SpeciesToHunt.Contains($"{species}"))
                        {
                            Log($"Outbreak for {species} has been found! Stopping routine execution!");
                            Array.Clear(Settings.SpecialConditions.SpeciesToHunt, 0, Settings.SpecialConditions.SpeciesToHunt.Length);
                            List<string> list = new List<string>(Settings.SpecialConditions.SpeciesToHunt.ToList());
                            list.Add($"{species}");
                            Settings.SpecialConditions.SpeciesToHunt = list.ToArray();
                            Log($"Clearing out Species list and setting it to {species}");
                            return;
                        }
                    };
                }
                await Click(A, 1_000, token).ConfigureAwait(false);
                await Click(A, 10_000, token).ConfigureAwait(false);
                // Loading screen
                await TeleportToCampZone(token).ConfigureAwait(false);
                await SetStick(LEFT, -30_000, 0, 1_000, token).ConfigureAwait(false); // reset face forward
                await ResetStick(token).ConfigureAwait(false); // reset

                await Click(A, 1_000, token).ConfigureAwait(false);
                await Click(A, 10_000, token).ConfigureAwait(false);
                attempts++;
            }
        }

        public ulong GenerateNextShiny(string species, int spawnerid, ulong seed, ulong seed1 = 0x82A2B175229D6A5B)
        {
            var mode = Settings.ScanLocation;
            ulong newseed = 0;
            var mainrng = new Xoroshiro128Plus(seed);
            int givs = 0;
            if (Settings.AlphaScanConditions.SpawnIsStaticAlpha)
                givs = 3;
            if (!Settings.AlphaScanConditions.InItSpawn)
            {
                mainrng.Next();
                mainrng.Next();
                mainrng = new Xoroshiro128Plus(mainrng.Next());
            }
            switch (mode)
            {
                case ArceupMap.ObsidianFieldlands: SpawnerSpecies = ObsidianTitle[SpawnerID]; break;
                case ArceupMap.CrimsonMirelands: SpawnerSpecies = CrimsonTitle[SpawnerID]; break;
                case ArceupMap.CoronetHighlands: SpawnerSpecies = CoronetTitle[SpawnerID]; break;
                case ArceupMap.CobaltCoastlands: SpawnerSpecies = CobaltTitle[SpawnerID]; break;
                case ArceupMap.AlabasterIcelands: SpawnerSpecies = AlabasterTitle[SpawnerID]; break;
            }
            for (int i = 0; i < Settings.AlphaScanConditions.MaxAdvancesToSearch; i++)
            {
                var generator_seed = mainrng.Next();
                mainrng.Next();
                var rng = new Xoroshiro128Plus(generator_seed);
                rng.Next();
                var (shiny, shinytype, encryption_constant, pid, ivs, ability, gender, nature, shinyseed) = GenerateFromSeed(rng.Next(), Settings.AlphaScanConditions.ShinyRolls, givs);

                if (shiny)
                {
                    Log($"\nAdvances: {i}\nAlpha: {SpawnerSpecies} - {shinytype} | SpawnerID: {spawnerid}\nEC: {encryption_constant:X8}\nPID: {pid:X8}\nIVs: {ivs[0]}/{ivs[1]}/{ivs[2]}/{ivs[3]}/{ivs[4]}/{ivs[5]}\nSeed: {generator_seed:X16}");
                    newseed = generator_seed;
                    if (Settings.SpecialConditions.SpeciesToHunt.Length != 0 && Settings.SpecialConditions.SpeciesToHunt.Contains($"{SpawnerSpecies}") || Settings.SpecialConditions.SpeciesToHunt.Length == 0)
                    {
                        Log("Desired Species found!");
                        Settings.StopOnMatch = true;
                    }
                    break;
                }
                mainrng = new Xoroshiro128Plus(mainrng.Next());
                if (i == Settings.AlphaScanConditions.MaxAdvancesToSearch - 1 && !shiny)
                    Log($"No results within {Settings.AlphaScanConditions.MaxAdvancesToSearch} advances for {SpawnerSpecies} | SpawnerID: {spawnerid}.");
            }

            return newseed;
        }
    }
}