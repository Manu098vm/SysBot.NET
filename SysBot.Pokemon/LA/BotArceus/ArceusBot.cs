using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using System.Linq;
using System.Collections.Generic;
using static SysBot.Pokemon.PokeDataOffsetsLA;
using System.Net;
using Newtonsoft.Json;

namespace SysBot.Pokemon
{
    public sealed class ArceusBot : PokeRoutineExecutor8LA, IEncounterBot, IArceusBot
    {
        private readonly PokeTradeHub<PA8> Hub;
        private readonly IDumper DumpSetting;
        private readonly ArceusBotSettings Settings;
        public static CancellationTokenSource EmbedSource = new();
        public static bool EmbedsInitialized;
        public static (PA8?, bool) EmbedMon;
        public ICountSettings Counts => (ICountSettings)Settings;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public ArceusBot(PokeBotState cfg, PokeTradeHub<PA8> hub) : base(cfg)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            Hub = hub;
            Settings = Hub.Config.Arceus;
            DumpSetting = Hub.Config.Folder;
        }

        private ulong ofs;
        private long[] disofs;
        private ulong MainNsoBase;
        private ulong OverworldOffset;
        private int alphacount;
        private int SpawnerID;
        private int attempts = 1;
        private int mapcount = 0;
        private int count = 0;
        string SpawnerSpecies = string.Empty;
        string x = string.Empty;
        string y = string.Empty;
        string z = string.Empty;
        List<PA8> monlist = new();
        List<PA8> bonuslist = new();
        List<PA8> shinylist = new();
        List<string> loglist = new();

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

        public class SpawnerMMO
        {
            public int Slot { get; set; }
            public Species Name { get; set; }

            public int Form { get; set; }
            public bool Alpha { get; set; }
            public int[] Levels { get; set; } = Array.Empty<int>();
            public int IVs { get; set; }
        }

        public Dictionary<string, SpawnerMMO[]> mmoslots { get; set; } = new();

        public override async Task MainLoop(CancellationToken token)
        {
            Log("Identifying trainer data of the host console.");
            await IdentifyTrainer(token).ConfigureAwait(false);
            await InitializeHardware(Settings, token).ConfigureAwait(false);
            try
            {
                Log($"Starting main {GetType().Name} loop.");
                Config.IterateNextRoutine();

                if (Hub.Config.Arceus.BotType == ArceusMode.MassiveOutbreakHunter)
                {
                    var Spawnersjson = new WebClient().DownloadString($"https://raw.githubusercontent.com/zyro670/JS-Finder/notabranch/Resources/pla_spawners/jsons/massivemassoutbreaks.json");
                    mmoslots = JsonConvert.DeserializeObject<Dictionary<string, SpawnerMMO[]>>(Spawnersjson) ?? new();
                }

                // Clear out any residual stick weirdness.
                await ResetStick(token).ConfigureAwait(false);
                MainNsoBase = await SwitchConnection.GetMainNsoBaseAsync(token).ConfigureAwait(false);
                var task = Hub.Config.Arceus.BotType switch
                {
                    ArceusMode.PlayerCoordScan => PlayerCoordScan(token),
                    ArceusMode.SeedAdvancer => SeedAdvancer(token),
                    ArceusMode.TimeSeedAdvancer => TimeSeedAdvancer(token),
                    ArceusMode.StaticAlphaScan => ScanForAlphas(token),
                    ArceusMode.DistortionSpammer => DistortionSpammer(token),
                    ArceusMode.DistortionReader => DistortionReader(token),
                    ArceusMode.MassiveOutbreakHunter => MassiveOutbreakHunter(token),
                    _ => PlayerCoordScan(token),
                };
                await task.ConfigureAwait(false);
            }
            catch (Exception e)
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

        private bool IsWaiting;
        private bool IsWaitingConfirmation;
        public void Acknowledge() => IsWaiting = false;
        public void AcknowledgeConfirmation() => IsWaitingConfirmation = false;

        public async Task TimeTest(CancellationToken token)
        {
            var timeofs = await NewParsePointer(TimePtrLA, token).ConfigureAwait(false);
            var timeVal = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(timeofs, 2, token).ConfigureAwait(false), 0);
            if (timeVal <= 7)
                timeVal = 15;
            else timeVal = 00;
            byte[] timeByte = BitConverter.GetBytes(timeVal);
            await SwitchConnection.WriteBytesAbsoluteAsync(timeByte, timeofs, token).ConfigureAwait(false);
        }

        private async Task PlayerCoordScan(CancellationToken token)
        {
            var ofs = await NewParsePointer(PlayerCoordPtrLA, token).ConfigureAwait(false);
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
            var menucheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(MenuOffset, 2, token).ConfigureAwait(false), 0);
            while (!token.IsCancellationRequested)
            {
                Log("Not in camp, repositioning and trying again.");
                await TeleportToCampZone(token);
                for (int i = 0; i < 2; i++)
                    await Click(B, 0_500, token).ConfigureAwait(false);

                await Click(A, 1_000, token).ConfigureAwait(false);

                menucheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(MenuOffset, 2, token).ConfigureAwait(false), 0);
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

                menucheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(MenuOffset, 2, token).ConfigureAwait(false), 0);
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

                menucheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(MenuOffset, 2, token).ConfigureAwait(false), 0);
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

                menucheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(MenuOffset, 2, token).ConfigureAwait(false), 0);
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

                menucheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(MenuOffset, 2, token).ConfigureAwait(false), 0);
                if (menucheck == 66)
                    return;
            }
        }

        public async Task DistortionReader(CancellationToken token)
        {
            List<PA8> matchlist = new();
            List<string> loglist = new();
            Log($"Starting Distortion Scanner for {Settings.ScanLocation}...");
            var mode = Settings.ScanLocation;
            int count = 0;
            int tries = 1;
            int encounter_slot_sum = 0;
            switch (mode)
            {
                case ArceupMap.ObsidianFieldlands: count = 16; break;
                case ArceupMap.CrimsonMirelands: count = 25; break;
                case ArceupMap.CobaltCoastlands: count = 20; break;
                case ArceupMap.CoronetHighlands: count = 20; break;
                case ArceupMap.AlabasterIcelands: count = 24; break;
            }
            while (!token.IsCancellationRequested)
            {
                Log($"Scan #{tries}...");
                for (int i = 0; i < count; i++)
                {
                    switch (mode)
                    {
                        case ArceupMap.ObsidianFieldlands: disofs = new long[] { 0x42CC4D8, 0xC0, 0x1C0, 0x990 + i * 0x8, 0x18, 0x430, 0xC0 }; encounter_slot_sum = 112; break;
                        case ArceupMap.CrimsonMirelands: disofs = new long[] { 0x42CC4D8, 0xC0, 0x1C0, 0xC70 + i * 0x8, 0x18, 0x430, 0xC0 }; encounter_slot_sum = 276; break;
                        case ArceupMap.CobaltCoastlands: disofs = new long[] { 0x42CC4D8, 0xC0, 0x1C0, 0xCC0 + i * 0x8, 0x18, 0x430, 0xC0 }; encounter_slot_sum = 163; break;
                        case ArceupMap.CoronetHighlands: disofs = new long[] { 0x42CC4D8, 0xC0, 0x1C0, 0x818 + i * 0x8, 0x18, 0x430, 0xC0 }; encounter_slot_sum = 382; break;
                        case ArceupMap.AlabasterIcelands: disofs = new long[] { 0x42CC4D8, 0xC0, 0x1C0, 0x948 + i * 0x8, 0x18, 0x430, 0xC0 }; encounter_slot_sum = 259; break;
                    }
                    var SpawnerOff = SwitchConnection.PointerAll(disofs, token).Result;
                    var GeneratorSeed = SwitchConnection.ReadBytesAbsoluteAsync(SpawnerOff, 8, token).Result;
                    //Log($"GroupID: {i} | Generator Seed: {BitConverter.ToString(GeneratorSeed).Replace("-", "")}");
                    var group_seed = (BitConverter.ToUInt64(GeneratorSeed, 0) - 0x82A2B175229D6A5B) & 0xFFFFFFFFFFFFFFFF;
                    if (group_seed != 0)
                    {
                        //Log($"Group Seed: {string.Format("0x{0:X}", group_seed)}");
                        if (i >= 13 && i <= 15 && Settings.ScanLocation == ArceupMap.CrimsonMirelands)
                        {
                            encounter_slot_sum = 118;
                        }
                        var (match, shiny, logs) = ReadDistortionSeed(i, group_seed, encounter_slot_sum);
                        loglist.Add(logs);
                        if (shiny)
                        {
                            string[] monlist = Settings.SpeciesToHunt.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (monlist.Length != 0)
                            {
                                bool huntedspecies = monlist.Contains($"{(Species)match.Species}");
                                if (!huntedspecies)
                                {
                                    EmbedMon = (match, false);
                                    Log(logs);
                                    break;
                                }
                            }
                            matchlist.Add(match);
                        }
                    }
                    foreach (PA8 match in matchlist)
                    {
                        if (match.IsShiny)
                        {
                            if (Settings.DistortionConditions.DistortionAlphaOnly)
                            {
                                if (!match.IsAlpha)
                                {
                                    EmbedMon = (match, false);
                                    break;
                                }
                            }
                            Log(loglist.Last());

                            EmbedMon = (match, true);
                            await Click(HOME, 1_000, token).ConfigureAwait(false);
                            IsWaiting = true;
                            while (IsWaiting)
                                await Task.Delay(1_000, token).ConfigureAwait(false);

                            if (!IsWaiting)
                                await Click(HOME, 1_000, token).ConfigureAwait(false);
                        }
                    }
                    matchlist.Clear();
                    new List<PA8>(matchlist);
                }
                string report = string.Join("\n", loglist);
                Log(report);
                loglist.Clear();
                new List<string>(loglist);
                tries++;
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await StartGame(Hub.Config, token).ConfigureAwait(false);
            }
        }

        public string GetDistortionSpeciesLocation(int id)
        {
            string location = string.Empty;
            var mode = Settings.ScanLocation;
            switch (mode)
            {
                case ArceupMap.ObsidianFieldlands:
                    {
                        if (id <= 4) location = "Horseshoe Plains";
                        if (id > 4 && id <= 8) location = "Windswept Run";
                        if (id > 8 && id <= 12) location = "Nature's Pantry";
                        if (id > 12 && id <= 16) location = "Sandgem Flats";
                    }
                    break;
                case ArceupMap.CrimsonMirelands:
                    {
                        if (id <= 4) location = "Droning Meadow";
                        if (id > 4 && id <= 8) location = "Holm of Trials";
                        if (id > 8 && id <= 12) location = "Unknown";
                        if (id > 12 && id <= 16) location = "Ursa's Ring";
                        if (id > 16 && id <= 20) location = "Prairie";
                        if (id > 20 && id <= 24) location = "Gapejaw Bog";
                    }
                    break;
                case ArceupMap.CobaltCoastlands:
                    {
                        if (id <= 4) location = "Ginko Landing";
                        if (id > 4 && id <= 8) location = "Aipom Hill";
                        if (id > 8 && id <= 12) location = "Deadwood Haunt";
                        if (id > 12 && id <= 16) location = "Spring Path";
                        if (id > 16 && id <= 20) location = "Windbreak Stand";
                    }
                    break;
                case ArceupMap.CoronetHighlands:
                    {
                        if (id <= 4) location = "Sonorous Path";
                        if (id > 4 && id <= 8) location = "Ancient Quarry";
                        if (id > 8 && id <= 12) location = "Celestica Ruins";
                        if (id > 12 && id <= 16) location = "Primeval Grotto";
                        if (id > 16 && id <= 20) location = "Boulderoll Ravine";
                    }
                    break;
                case ArceupMap.AlabasterIcelands:
                    {
                        if (id <= 4) location = "Bonechill Wastes North";
                        if (id > 4 && id <= 8) location = "Avalugg's Legacy";
                        if (id > 8 && id <= 12) location = "Bonechill Wastes South";
                        if (id > 12 && id <= 16) location = "Southeast of Arena";
                        if (id > 16 && id <= 20) location = "Heart's Crag";
                        if (id > 20 && id <= 24) location = "Arena's Approach";
                    }
                    break;
            }
            return location;
        }

        public PA8 GetDistortionSpecies(double encslot)
        {
            var pk = new PA8();
            var mode = Settings.ScanLocation;
            switch (mode)
            {
                case ArceupMap.ObsidianFieldlands:
                    {
                        if (encslot < 100) pk.Species = (int)Species.Sneasel;
                        if (encslot > 100 && encslot < 101)
                        {
                            pk.Species = (int)Species.Sneasel;
                            pk.IsAlpha = true;
                        }
                        if (encslot > 101 && encslot < 111) pk.Species = (int)Species.Weavile;
                        if (encslot > 111 && encslot < 112)
                        {
                            pk.Species = (int)Species.Weavile;
                            pk.IsAlpha = true;
                        }
                    }
                    break;
                case ArceupMap.CrimsonMirelands:
                    {
                        if (encslot < 100) pk.Species = (int)Species.Porygon;
                        if (encslot > 100 && encslot < 101)
                        {
                            pk.Species = (int)Species.Porygon;
                            pk.IsAlpha = true;
                        }
                        if (encslot > 101 && encslot < 111) pk.Species = (int)Species.Porygon2;
                        if (encslot > 111 && encslot < 112)
                        {
                            pk.Species = (int)Species.Porygon2;
                            pk.IsAlpha = true;
                        }
                        if (encslot > 112 && encslot < 117) pk.Species = (int)Species.PorygonZ;
                        if (encslot > 117 && encslot < 118)
                        {
                            pk.Species = (int)Species.PorygonZ;
                            pk.IsAlpha = true;
                        }
                        if (encslot > 118 && encslot < 218) pk.Species = (int)Species.Cyndaquil;
                        if (encslot > 218 && encslot < 219)
                        {
                            pk.Species = (int)Species.Cyndaquil;
                            pk.IsAlpha = true;
                        }
                        if (encslot > 219 && encslot < 269) pk.Species = (int)Species.Quilava;
                        if (encslot > 269 && encslot < 270)
                        {
                            pk.Species = (int)Species.Quilava;
                            pk.IsAlpha = true;
                        }
                        if (encslot > 270 && encslot < 275) pk.Species = (int)Species.Typhlosion;
                        if (encslot >= 275)
                        {
                            pk.Species = (int)Species.Typhlosion;
                            pk.IsAlpha = true;
                        }
                    }
                    break;
                case ArceupMap.CobaltCoastlands:
                    {
                        if (encslot < 100) pk.Species = (int)Species.Magnemite;
                        if (encslot > 100 && encslot < 101)
                        {
                            pk.Species = (int)Species.Magnemite;
                            pk.IsAlpha = true;
                        }
                        if (encslot > 101 && encslot < 151) pk.Species = (int)Species.Magneton;
                        if (encslot > 151 && encslot < 152)
                        {
                            pk.Species = (int)Species.Magneton;
                            pk.IsAlpha = true;
                        }
                        if (encslot > 152 && encslot < 162) pk.Species = (int)Species.Magnezone;
                        if (encslot >= 162)
                        {
                            pk.Species = (int)Species.Magnezone;
                            pk.IsAlpha = true;
                        }
                    }
                    break;
                case ArceupMap.CoronetHighlands:
                    {
                        if (encslot < 100) pk.Species = (int)Species.Cranidos;
                        if (encslot > 100 && encslot < 101)
                        {
                            pk.Species = (int)Species.Cranidos;
                            pk.IsAlpha = true;
                        }
                        if (encslot > 101 && encslot < 111) pk.Species = (int)Species.Rampardos;
                        if (encslot > 111 && encslot < 112)
                        {
                            pk.Species = (int)Species.Rampardos;
                            pk.IsAlpha = true;
                        }
                        if (encslot > 112 && encslot < 212) pk.Species = (int)Species.Shieldon;
                        if (encslot > 212 && encslot < 213)
                        {
                            pk.Species = (int)Species.Shieldon;
                            pk.IsAlpha = true;
                        }
                        if (encslot > 213 && encslot < 223) pk.Species = (int)Species.Bastiodon;
                        if (encslot > 223 && encslot < 224)
                        {
                            pk.Species = (int)Species.Bastiodon;
                            pk.IsAlpha = true;
                        }
                        if (encslot > 224 && encslot < 324) pk.Species = (int)Species.Rowlet;
                        if (encslot > 324 && encslot < 325)
                        {
                            pk.Species = (int)Species.Rowlet;
                            pk.IsAlpha = true;
                        }
                        if (encslot > 325 && encslot < 375) pk.Species = (int)Species.Dartrix;
                        if (encslot > 375 && encslot < 376)
                        {
                            pk.Species = (int)Species.Dartrix;
                            pk.IsAlpha = true;
                        }
                        if (encslot > 376 && encslot < 381) pk.Species = (int)Species.Decidueye;
                        if (encslot >= 381)
                        {
                            pk.Species = (int)Species.Decidueye;
                            pk.IsAlpha = true;
                        }
                    }
                    break;
                case ArceupMap.AlabasterIcelands:
                    {
                        if (encslot < 100) pk.Species = (int)Species.Scizor;
                        if (encslot > 100 && encslot < 101)
                        {
                            pk.Species = (int)Species.Scizor;
                            pk.IsAlpha = true;
                        }
                        if (encslot > 101 && encslot < 201) pk.Species = (int)Species.Oshawott;
                        if (encslot > 201 && encslot < 202)
                        {
                            pk.Species = (int)Species.Oshawott;
                            pk.IsAlpha = true;
                        }
                        if (encslot > 202 && encslot < 252) pk.Species = (int)Species.Dewott;
                        if (encslot > 252 && encslot < 253)
                        {
                            pk.Species = (int)Species.Dewott;
                            pk.IsAlpha = true;
                        }
                        if (encslot > 253 && encslot < 258) pk.Species = (int)Species.Samurott;
                        if (encslot > 258)
                        {
                            pk.Species = (int)Species.Samurott;
                            pk.IsAlpha = true;
                        }
                    }
                    break;
            }
            return pk;
        }
        public async Task DistortionSpammer(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Log("Whipping up a distortion...");
                // Activates distortions
                await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0x5280010A), MainNsoBase + ActivateDistortion, token).ConfigureAwait(false);
                await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0x7100052A), MainNsoBase + ActivateDistortion, token).ConfigureAwait(false);
                var delta = DateTime.Now;
                var cd = TimeSpan.FromMinutes(Settings.DistortionConditions.WaitTimeDistortion);
                Log($"Waiting {Settings.DistortionConditions.WaitTimeDistortion} minutes then starting the next one...");
                while (DateTime.Now - delta < cd && !token.IsCancellationRequested)
                {
                    await Task.Delay(5_000).ConfigureAwait(false);
                    Log($"Time Remaining: {cd - (DateTime.Now - delta)}");
                }

                await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0x5280010A), MainNsoBase + ActivateDistortion, token).ConfigureAwait(false);
                await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0x7100052A), MainNsoBase + ActivateDistortion, token).ConfigureAwait(false);
                await Task.Delay(5_000).ConfigureAwait(false);
            }
        }

        public async Task TimeSeedAdvancer(CancellationToken token)
        {
            int success = 0;
            int heal = 0;
            uint offset = 0x04296764;
            string[] coords = { Settings.SpecialConditions.SpawnZoneX, Settings.SpecialConditions.SpawnZoneY, Settings.SpecialConditions.SpawnZoneZ };
            for (int a = 0; a < coords.Length; a++)
            {
                if (string.IsNullOrEmpty(coords[a]))
                {
                    Log($"One of your coordinates is empty, please fill it accordingly!");
                    return;
                }
            }
            OverworldOffset = await SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
            // Activates invcincible trainer cheat so we don't faint from teleporting or a Pokemon attacking and infinite PP
            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0xD65F03C0), MainNsoBase + InvincibleTrainer1, token).ConfigureAwait(false);
            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0xD65F03C0), MainNsoBase + InvincibleTrainer2, token).ConfigureAwait(false);

            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0xD28008AA), MainNsoBase + InfPP1, token).ConfigureAwait(false);
            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0xD28008A9), MainNsoBase + InfPP2, token).ConfigureAwait(false);

            await GetDefaultCampCoords(token).ConfigureAwait(false);
            while (!token.IsCancellationRequested)
            {
                string alpha = string.Empty;
                for (int adv = 0; adv < Settings.AlphaScanConditions.Advances; adv++)
                {
                    Log($"Advancing {Settings.AlphaScanConditions.Advances - adv} times...");
                    await Click(B, 1_000, token).ConfigureAwait(false);// Random B incase of button miss
                    await Click(A, 1_000, token).ConfigureAwait(false);
                    await TimeTest(token).ConfigureAwait(false);
                    await Task.Delay(2_000).ConfigureAwait(false);
                    if (Settings.AlphaScanConditions.SpawnIsStaticAlpha)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            await TimeTest(token).ConfigureAwait(false);
                            await Task.Delay(2_000).ConfigureAwait(false);
                        }
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
                            if (tries == 3)
                            {
                                Log("Tried 3 times, is the encounter present? Changing time to try again.");
                                await TimeTest(token).ConfigureAwait(false);
                                await TeleportToSpawnZone(token).ConfigureAwait(false);
                                adv = adv - 1;
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
                    }
                    if (overworldcheck == 0)
                    {
                        ulong ofs = await NewParsePointer(WildPokemonPtrLA, token).ConfigureAwait(false);
                        var pk = await ReadPokemon(ofs, 0x168, token).ConfigureAwait(false);
                        if (pk != null)
                        {
                            success++;
                            heal++;
                            var print = Hub.Config.StopConditions.GetAlphaPrintName(pk);
                            if (pk.IsAlpha) alpha = "Alpha - ";
                            if (pk.IsShiny)
                            {
                                Log($"In battle with {print}!");
                                EmbedMon = (pk, true);

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
                    if (adv == Settings.AlphaScanConditions.Advances)
                        return;
                    if (heal == 3)
                    {
                        Log("Returning to camp to heal our party!");
                        await TeleportToCampZone(token).ConfigureAwait(false);
                        var menucheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(offset, 2, token).ConfigureAwait(false), 0);
                        //Log($"Menu check: {menucheck}");
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
                        await Click(A, 1_000, token).ConfigureAwait(false);
                        Log("Resting for a little while!");
                        await Click(A, 12_000, token).ConfigureAwait(false);
                        await Click(A, 1_000, token).ConfigureAwait(false);
                        await TeleportToSpawnZone(token).ConfigureAwait(false);

                    }
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
            OverworldOffset = await SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
            // Activates invcincible trainer cheat so we don't faint from teleporting or a Pokemon attacking
            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0xD65F03C0), MainNsoBase + InvincibleTrainer1, token).ConfigureAwait(false);//invi
            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0xD65F03C0), MainNsoBase + InvincibleTrainer2, token).ConfigureAwait(false);//invi

            while (!token.IsCancellationRequested)
            {
                await GetDefaultCampCoords(token).ConfigureAwait(false);
                string alpha = string.Empty;
                for (int adv = 0; adv < Settings.AlphaScanConditions.Advances; adv++)
                {
                    Log($"Advancing {Settings.AlphaScanConditions.Advances - adv} times...");
                    await Click(B, 1_000, token).ConfigureAwait(false);// Random B incase of button miss
                    await Click(A, 1_000, token).ConfigureAwait(false);
                    var menucheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(MenuOffset, 2, token).ConfigureAwait(false), 0);
                    if (menucheck == 66)
                        await Click(A, 0_800, token).ConfigureAwait(false);
                    while (menucheck == 64 || menucheck == 0)
                    {
                        Log("Wrong menu opened? Backing out now and trying to reposition.");
                        await Click(B, 1_500, token).ConfigureAwait(false);
                        await Reposition(token).ConfigureAwait(false);
                        await Click(B, 1_500, token).ConfigureAwait(false);
                        menucheck = BitConverter.ToUInt16(await SwitchConnection.ReadBytesMainAsync(MenuOffset, 2, token).ConfigureAwait(false), 0);
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
                            if (tries == 3)
                            {
                                Log("Tried 3 times, is the encounter present? Going back to camp to try again.");
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
                    }
                    if (overworldcheck == 0)
                    {
                        ulong ofs = await NewParsePointer(WildPokemonPtrLA, token).ConfigureAwait(false);
                        var pk = await ReadPokemon(ofs, 0x168, token).ConfigureAwait(false);
                        if (pk != null)
                        {
                            var print = Hub.Config.StopConditions.GetAlphaPrintName(pk);
                            if (pk.IsAlpha) alpha = "Alpha - ";
                            if (pk.IsShiny)
                            {
                                Log($"In battle with {print}!");
                                EmbedMon = (pk, true);

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
            Settings.AlphaScanConditions.StopOnMatch = false;
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
                if (Settings.AlphaScanConditions.StopOnMatch)
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
            ofs = await NewParsePointer(PlayerCoordPtrLA, token).ConfigureAwait(false);
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
            ofs = await NewParsePointer(PlayerCoordPtrLA, token).ConfigureAwait(false);
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

        public async Task TeleportToMMOGroupZone(CancellationToken token)
        {
            ofs = await NewParsePointer(PlayerCoordPtrLA, token).ConfigureAwait(false);
            uint coordX1 = uint.Parse(Settings.OutbreakConditions.GroupZoneX, System.Globalization.NumberStyles.AllowHexSpecifier);
            byte[] X1 = BitConverter.GetBytes(coordX1);
            uint coordY1 = uint.Parse(Settings.OutbreakConditions.GroupZoneY, System.Globalization.NumberStyles.AllowHexSpecifier);
            byte[] Y1 = BitConverter.GetBytes(coordY1);
            uint coordZ1 = uint.Parse(Settings.OutbreakConditions.GroupZoneZ, System.Globalization.NumberStyles.AllowHexSpecifier);
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
                var SpawnerOffpoint = new long[] { 0x42a6ee0, 0x330, 0x70 + i * 0x440 + 0x20 };
                var SpawnerOff = SwitchConnection.PointerAll(SpawnerOffpoint, token).Result;
                var GeneratorSeed = SwitchConnection.ReadBytesAbsoluteAsync(SpawnerOff, 8, token).Result;
                Log($"Generator Seed: {BitConverter.ToString(GeneratorSeed).Replace("-", "")}");
                var group_seed = (BitConverter.ToUInt64(GeneratorSeed, 0) - 0x82A2B175229D6A5B) & 0xFFFFFFFFFFFFFFFF;
                Log($"Group Seed: {string.Format("0x{0:X}", group_seed)}");
                SpawnerID = i;
                GenerateNextShiny(SpawnerSpecies, i, group_seed);
            }
        }

        public (List<PA8>, List<PA8>) ReadMMOSeed(int totalspawn, ulong group_seed, int bonuscount, ulong encslot, ulong bonusencslot)
        {
            List<PA8> monlist = new();
            PA8 pk = new();
            List<PA8> bonuslist = new();
            var groupseed = group_seed;
            int givs = 0;
            int encsum;
            int bonusum;
            var mainrng = new Xoroshiro128Plus(groupseed);
            (encsum, bonusum) = GrabEncounterSum(encslot, bonusencslot);
            for (int i = 0; i < 4; i++)
            {
                var spawner_seed = mainrng.Next();
                var spawner_rng = new Xoroshiro128Plus(spawner_seed);
                var encounter_slot = spawner_rng.Next() / Math.Pow(2, 64) * encsum;
                var fixedseed = spawner_rng.Next();
                mainrng.Next();
                (pk, givs) = GrabMMOSpecies(encounter_slot, encslot);
                var (shiny, shinytype, encryption_constant, pid, ivs, ability, gender, nature, shinyseed) = GenerateFromSeed(fixedseed, Settings.ShinyRolls, givs);
                pk.IV_HP = ivs[0]; pk.IV_ATK = ivs[1]; pk.IV_DEF = ivs[2]; pk.IV_SPA = ivs[3]; pk.IV_SPD = ivs[4]; pk.IV_SPE = ivs[5]; pk.Nature = (int)nature; pk.EncryptionConstant = (uint)encryption_constant; pk.PID = (uint)pid;
                if (shiny == true)
                {
                    if (shinytype.Contains("★"))
                        CommonEdits.SetShiny(pk, Shiny.AlwaysStar);
                    if (shinytype.Contains("■"))
                        CommonEdits.SetShiny(pk, Shiny.AlwaysSquare);
                }
                monlist.Add(pk);
            }
            groupseed = mainrng.Next();
            mainrng = new Xoroshiro128Plus(groupseed);
            var respawnrng = new Xoroshiro128Plus(groupseed);
            for (int r = 0; r < totalspawn - 4; r++)
            {
                var spawner_seed = respawnrng.Next();
                respawnrng.Next();
                respawnrng = new Xoroshiro128Plus(respawnrng.Next());
                var fixed_rng = new Xoroshiro128Plus(spawner_seed);
                var encounter_slot = fixed_rng.Next() / Math.Pow(2, 64) * encsum;
                var fixed_seed = fixed_rng.Next();
                (pk, givs) = GrabMMOSpecies(encounter_slot, encslot);
                var (shiny, shinytype, encryption_constant, pid, ivs, ability, gender, nature, shinyseed) = GenerateFromSeed(fixed_seed, Settings.ShinyRolls, givs);
                pk.IV_HP = ivs[0]; pk.IV_ATK = ivs[1]; pk.IV_DEF = ivs[2]; pk.IV_SPA = ivs[3]; pk.IV_SPD = ivs[4]; pk.IV_SPE = ivs[5]; pk.Nature = (int)nature; pk.EncryptionConstant = (uint)encryption_constant; pk.PID = (uint)pid;
                if (shiny == true)
                {
                    if (shinytype.Contains("★"))
                        CommonEdits.SetShiny(pk, Shiny.AlwaysStar);
                    if (shinytype.Contains("■"))
                        CommonEdits.SetShiny(pk, Shiny.AlwaysSquare);
                }
                monlist.Add(pk);
            }
            //Bonus round
            if (bonusum != 0)
            {
                var bonus_seed = respawnrng.Next() - 0x82A2B175229D6A5B & 0xFFFFFFFFFFFFFFFF;
                mainrng = new Xoroshiro128Plus(bonus_seed);
                for (int i = 0; i < 4; i++)
                {
                    var spawner_seed = mainrng.Next();
                    var spawner_rng = new Xoroshiro128Plus(spawner_seed);
                    var encounter_slot = spawner_rng.Next() / Math.Pow(2, 64) * bonusum;
                    var fixedseed = spawner_rng.Next();
                    mainrng.Next();
                    (pk, givs) = GrabMMOSpecies(encounter_slot, bonusencslot);
                    var (shiny, shinytype, encryption_constant, pid, ivs, ability, gender, nature, shinyseed) = GenerateFromSeed(fixedseed, Settings.ShinyRolls, givs);
                    pk.IV_HP = ivs[0]; pk.IV_ATK = ivs[1]; pk.IV_DEF = ivs[2]; pk.IV_SPA = ivs[3]; pk.IV_SPD = ivs[4]; pk.IV_SPE = ivs[5]; pk.Nature = (int)nature; pk.EncryptionConstant = (uint)encryption_constant; pk.PID = (uint)pid;

                    if (shiny == true)
                    {
                        if (shinytype.Contains("★"))
                            CommonEdits.SetShiny(pk, Shiny.AlwaysStar);
                        if (shinytype.Contains("■"))
                            CommonEdits.SetShiny(pk, Shiny.AlwaysSquare);
                    }
                    bonuslist.Add(pk);
                }
                bonus_seed = mainrng.Next();
                mainrng = new Xoroshiro128Plus(bonus_seed);
                var bonusrng = new Xoroshiro128Plus(bonus_seed);
                for (int r = 0; r < bonuscount - 4; r++)
                {
                    var bonusspawner_seed = bonusrng.Next();
                    bonusrng.Next();
                    bonusrng = new Xoroshiro128Plus(bonusrng.Next());
                    var fixed_rng = new Xoroshiro128Plus(bonusspawner_seed);
                    var encounter_slot = fixed_rng.Next() / Math.Pow(2, 64) * bonusum;
                    var fixed_seed = fixed_rng.Next();
                    (pk, givs) = GrabMMOSpecies(encounter_slot, bonusencslot);
                    var (shiny, shinytype, encryption_constant, pid, ivs, ability, gender, nature, shinyseed) = GenerateFromSeed(fixed_seed, Settings.ShinyRolls, givs);
                    pk.IV_HP = ivs[0]; pk.IV_ATK = ivs[1]; pk.IV_DEF = ivs[2]; pk.IV_SPA = ivs[3]; pk.IV_SPD = ivs[4]; pk.IV_SPE = ivs[5]; pk.Nature = (int)nature; pk.EncryptionConstant = (uint)encryption_constant; pk.PID = (uint)pid;

                    if (shiny == true)
                    {
                        if (shinytype.Contains("★"))
                            CommonEdits.SetShiny(pk, Shiny.AlwaysStar);
                        if (shinytype.Contains("■"))
                            CommonEdits.SetShiny(pk, Shiny.AlwaysSquare);
                    }
                    bonuslist.Add(pk);
                }
            }
            return (monlist, bonuslist);
        }

        public List<PA8> ReadOutbreakSeed(Species species, int totalspawn, ulong groupseed)
        {
            List<PA8> monlist = new();
            PA8 pk = new();
            int givs = 0;
            var mainrng = new Xoroshiro128Plus(groupseed);
            for (int i = 0; i < 4; i++)
            {
                var spawner_seed = mainrng.Next();
                mainrng.Next();
                var spawner_rng = new Xoroshiro128Plus(spawner_seed);
                var slot = spawner_rng.Next() / Math.Pow(2, 64) * 101;
                var alpha = slot >= 100;
                var fixedseed = spawner_rng.Next();
                //mainrng.Next();
                if (alpha)
                    givs = 3;
                var (shiny, shinytype, encryption_constant, pid, ivs, ability, gender, nature, shinyseed) = GenerateFromSeed(fixedseed, Settings.OutbreakConditions.ShinyRollsForRegularOutbreaks, givs);
                pk.Species = (int)species;
                pk.IV_HP = ivs[0]; pk.IV_ATK = ivs[1]; pk.IV_DEF = ivs[2]; pk.IV_SPA = ivs[3]; pk.IV_SPD = ivs[4]; pk.IV_SPE = ivs[5]; pk.Nature = (int)nature; pk.EncryptionConstant = (uint)encryption_constant; pk.PID = (uint)pid;
                if (alpha)
                    pk.IsAlpha = true;
                if (shiny == true)
                {
                    if (shinytype.Contains("★"))
                        CommonEdits.SetShiny(pk, Shiny.AlwaysStar);
                    if (shinytype.Contains("■"))
                        CommonEdits.SetShiny(pk, Shiny.AlwaysSquare);
                }
                monlist.Add(pk);
                pk = new();
                givs = 0;
            }
            groupseed = mainrng.Next();
            mainrng = new Xoroshiro128Plus(groupseed);
            var respawnrng = new Xoroshiro128Plus(groupseed);
            for (int r = 0; r < totalspawn - 4; r++)
            {
                var spawner_seed = respawnrng.Next();
                respawnrng.Next();
                respawnrng = new Xoroshiro128Plus(respawnrng.Next());
                var fixed_rng = new Xoroshiro128Plus(spawner_seed);
                var slot = fixed_rng.Next() / Math.Pow(2, 64) * 101;
                var alpha = slot >= 100;
                //fixed_rng.Next();
                var fixed_seed = fixed_rng.Next();
                if (alpha)
                    givs = 3;
                var (shiny, shinytype, encryption_constant, pid, ivs, ability, gender, nature, shinyseed) = GenerateFromSeed(fixed_seed, Settings.OutbreakConditions.ShinyRollsForRegularOutbreaks, givs);

                pk.Species = (int)species;
                pk.IV_HP = ivs[0]; pk.IV_ATK = ivs[1]; pk.IV_DEF = ivs[2]; pk.IV_SPA = ivs[3]; pk.IV_SPD = ivs[4]; pk.IV_SPE = ivs[5]; pk.Nature = (int)nature; pk.EncryptionConstant = (uint)encryption_constant; pk.PID = (uint)pid;
                if (alpha)
                    pk.IsAlpha = true;
                if (shiny == true)
                {
                    if (shinytype.Contains("★"))
                        CommonEdits.SetShiny(pk, Shiny.AlwaysStar);
                    if (shinytype.Contains("■"))
                        CommonEdits.SetShiny(pk, Shiny.AlwaysSquare);
                }
                monlist.Add(pk);
                pk = new();
                givs = 0;
            }

            return monlist;
        }

        public (PA8 match, bool shiny, string log) ReadDistortionSeed(int id, ulong group_seed, int encslotsum)
        {
            string logs = string.Empty;
            var groupseed = group_seed;
            var mainrng = new Xoroshiro128Plus(groupseed);
            var generator_seed = mainrng.Next();
            var rng = new Xoroshiro128Plus(generator_seed);
            var encounter_slot = rng.Next() / Math.Pow(2, 64) * encslotsum;
            var fixedseed = rng.Next();
            var (shiny, shinytype, encryption_constant, pid, ivs, ability, gender, nature, shinyseed) = GenerateFromSeed(fixedseed, Settings.ShinyRolls, 0);
            var pk = GetDistortionSpecies(encounter_slot);
            string location = GetDistortionSpeciesLocation(id);
            if (id == 0 || id == 4 || id == 8 || id == 12 || id == 16 || id == 20)
            {
                logs += $"Ignoring Common Spawner from GroupID: {id}.";
                return (pk, false, logs);
            }
            if (id >= 9 && id <= 12 && Settings.ScanLocation == ArceupMap.CrimsonMirelands)
            {
                logs += $"Ignoring Spawner from GroupID: {id} as location currently unknown.";
                return (pk, false, logs);
            }

            pk.IV_HP = ivs[0]; pk.IV_ATK = ivs[1]; pk.IV_DEF = ivs[2]; pk.IV_SPA = ivs[3]; pk.IV_SPD = ivs[4]; pk.IV_SPE = ivs[5]; pk.Nature = (int)nature; pk.EncryptionConstant = (uint)encryption_constant; pk.PID = (uint)pid;
            if (shinytype.Contains("Star"))
                CommonEdits.SetShiny(pk, Shiny.AlwaysStar);
            if (shinytype.Contains("Square"))
                CommonEdits.SetShiny(pk, Shiny.AlwaysSquare);
            var print = Hub.Config.StopConditions.GetAlphaPrintName(pk);
            logs += $"Generator Seed: {(group_seed + 0x82A2B175229D6A5B & 0xFFFFFFFFFFFFFFFF):X16}\nGroup: {id}\n{shinytype}\n{print}\nEncounter Slot: {encounter_slot}\nLocation: {location}";
            mainrng.Next();
            mainrng.Next();
            _ = new Xoroshiro128Plus(mainrng.Next());
            return (pk, shiny, logs);
        }
        public ulong GenerateNextShiny(string species, int spawnerid, ulong seed, ulong seed1 = 0x82A2B175229D6A5B)
        {
            int hits = 0;
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
                var (shiny, shinytype, encryption_constant, pid, ivs, ability, gender, nature, shinyseed) = GenerateFromSeed(rng.Next(), Settings.ShinyRolls, givs);

                if (shiny)
                {
                    if (Settings.SpeciesToHunt.Length != 0 && !Settings.SpeciesToHunt.Contains(SpawnerSpecies))
                        break;

                    if (Settings.SearchForIVs.Length != 0)
                    {
                        if (Settings.SearchForIVs[0] == ivs[0] && Settings.SearchForIVs[1] == ivs[1] && Settings.SearchForIVs[2] == ivs[2] && Settings.SearchForIVs[3] == ivs[3] && Settings.SearchForIVs[4] == ivs[4] && Settings.SearchForIVs[5] == ivs[5])
                        {
                            Log($"\nAdvances: {i}\nAlpha: {SpawnerSpecies} - {shinytype} | SpawnerID: {spawnerid}\nEC: {encryption_constant:X8}\nPID: {pid:X8}\nIVs: {ivs[0]}/{ivs[1]}/{ivs[2]}/{ivs[3]}/{ivs[4]}/{ivs[5]}\nNature: {nature}\nSeed: {shinyseed:X16}");
                            newseed = generator_seed;
                            Settings.AlphaScanConditions.StopOnMatch = true;
                            hits++;

                            if (hits == 3)
                            {
                                Log($"First three shiny results for {SpawnerSpecies} found.");
                                break;
                            }
                        }
                    }
                    if (Settings.SearchForIVs.Length == 0)
                    {
                        Log($"\nAdvances: {i}\nAlpha: {SpawnerSpecies} - {shinytype} | SpawnerID: {spawnerid}\nEC: {encryption_constant:X8}\nPID: {pid:X8}\nIVs: {ivs[0]}/{ivs[1]}/{ivs[2]}/{ivs[3]}/{ivs[4]}/{ivs[5]}\nNature: {nature}\nSeed: {shinyseed:X16}");
                        newseed = generator_seed;
                        Settings.AlphaScanConditions.StopOnMatch = true;
                        hits++;

                        if (hits == 3)
                        {
                            Log($"First three shiny results for {SpawnerSpecies} found.");
                            break;
                        }
                    }
                }
                mainrng = new Xoroshiro128Plus(mainrng.Next());
                if (i == Settings.AlphaScanConditions.MaxAdvancesToSearch - 1 && !shiny)
                    Log($"No results within {Settings.AlphaScanConditions.MaxAdvancesToSearch} advances for {SpawnerSpecies} | SpawnerID: {spawnerid}.");
            }

            return newseed;
        }

        public async Task PerformOutbreakScan(CancellationToken token)
        {
            for (int i = 0; i < 4; i++)
            {
                var ofs = new long[] { 0x42BA6B0, 0x2B0, 0x58, 0x18, 0x20 + i * 0x50 };
                var outbreakptr = SwitchConnection.PointerAll(ofs, token).Result;
                Species outbreakspecies = (Species)BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(outbreakptr, 2, token).ConfigureAwait(false), 0);
                if (outbreakspecies != Species.None)
                {
                    var outbreakseed = BitConverter.ToUInt64(await SwitchConnection.ReadBytesAbsoluteAsync(outbreakptr + 0x38, 8, token).ConfigureAwait(false), 0);
                    var spawncount = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(outbreakptr + 0x40, 2, token).ConfigureAwait(false), 0);
                    Log($"Outbreak found for: {outbreakspecies} | Total Spawn Count: {spawncount}.");
                    monlist = ReadOutbreakSeed(outbreakspecies, spawncount, outbreakseed);
                    foreach (PA8 pk in monlist)
                    {
                        count++;
                        var print = Hub.Config.StopConditions.GetAlphaPrintName(pk);
                        loglist.Add($"\nOutbreak Spawn: #{count}" + print);

                        if (pk.IsShiny)
                            shinylist.Add(pk);
                    }
                    if (Settings.SpeciesToHunt.Contains($"{outbreakspecies}"))
                    {
                        Log($"Outbreak for {outbreakspecies} has been found! Stopping routine execution!");
                        IsWaiting = true;
                        while (IsWaiting)
                            await Task.Delay(1_000, token).ConfigureAwait(false);
                        if (!IsWaiting)
                            break;
                    }
                };
                string report = string.Join("\n", loglist);
                Log(report);
                loglist.Clear();
                loglist = new();
            }
        }

        public (int, int) GrabEncounterSum(ulong encslot, ulong bonusslot)
        {
            int encmin;
            int encmax = 0;
            int bonusmin;
            int bonusmax = 0;
            foreach (var keyValuePair in mmoslots)
            {
                if (keyValuePair.Key == "0x" + $"{encslot:X16}")
                {
                    int i = 0;
                    foreach (var keyValue in keyValuePair.Value)
                    {
                        encmax += keyValue.Slot;
                        if (i == 0)
                            encmin = keyValue.Slot;
                        i++;
                    }
                }
                if (keyValuePair.Key == "0x" + $"{bonusslot:X16}")
                {
                    int i = 0;
                    foreach (var keyValue in keyValuePair.Value)
                    {
                        bonusmax += keyValue.Slot;
                        if (i == 0)
                            bonusmin = keyValue.Slot;
                        i++;
                    }
                }
            }
            return (encmax, bonusmax);
        }
        public (PA8, int) GrabMMOSpecies(double encounter_slot, ulong encslot)
        {
            PA8 pk = new();
            var encmax = 0;
            int form = 0;
            Species name = Species.None;
            int ivs = 0;
            bool isalpha = false;
            foreach (var keyValuePair in mmoslots)
            {
                if (keyValuePair.Key == "0x" + $"{encslot:X16}")
                {
                    foreach (var keyValue in keyValuePair.Value)
                    {
                        encmax += keyValue.Slot;
                        if (encounter_slot < encmax)
                        {
                            name = keyValue.Name;
                            form = keyValue.Form;
                            ivs = keyValue.IVs;
                            isalpha = keyValue.Alpha;
                            break;
                        }
                    }
                    pk.Species = (int)name;
                    pk.Form = form;
                    if (isalpha == true)
                        pk.IsAlpha = true;
                }
            }
            return (pk, ivs);
        }
        public async Task PerformMMOScan(CancellationToken token)
        {
            byte[] info;
            string map = string.Empty;
            string[] list = Settings.SpeciesToHunt.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Species species;
            for (int i = 0; i < 5; i++)
            {
                Log($"Checking map #{mapcount + 1}...");
                var groupcount = 0;
                do
                {
                    var ofs = new long[] { 0x42BA6B0, 0x2B0, 0x58, 0x18, 0x1D4 + (groupcount * 0x90) + (mapcount * 0xB80) };
                    var outbreakptr = SwitchConnection.PointerAll(ofs, token).Result;
                    info = SwitchConnection.ReadBytesAbsoluteAsync(outbreakptr, 100, token).Result;
                    if (groupcount == 0)
                    {
                        var location = SwitchConnection.ReadBytesAbsoluteAsync(outbreakptr - 0x24, 2, token).Result;
                        map = BitConverter.ToString(location);
                        switch (map)
                        {
                            case "B7-56": map = " in the Cobalt Coastlands"; break;
                            case "04-55": map = " in the Crimson Mirelands"; break;
                            case "51-53": map = " in the Alabaster Icelands"; break;
                            case "9E-51": map = " in the Coronet Highlands"; break;
                            case "1D-5A": map = " in the Obsidian Fieldlands"; break;
                            case "45-26": map = " in an outbreak"; break;
                            case "00-00": map = ""; break;
                        }
                    }
                    species = (Species)BitConverter.ToUInt16(info.Slice(0, 2), 0);
                    if (species != Species.None && species <= Species.MAX_COUNT)
                    {
                        var spawncount = BitConverter.ToUInt16(info.Slice(76, 2), 0);
                        var encslot = BitConverter.ToUInt64(info.Slice(36, 8), 0);
                        var bonusencslot = BitConverter.ToUInt64(info.Slice(44, 8), 0);
                        var bonuscount = BitConverter.ToUInt16(info.Slice(96, 2), 0);
                        var group_seed = BitConverter.ToUInt64(info.Slice(68, 8), 0);
                        var spawncoordx = BitConverter.ToUInt32(await SwitchConnection.ReadBytesAbsoluteAsync(outbreakptr - 0x14, 4, token).ConfigureAwait(false), 0);
                        var spawncoordy = BitConverter.ToUInt32(await SwitchConnection.ReadBytesAbsoluteAsync(outbreakptr - 0x10, 4, token).ConfigureAwait(false), 0);
                        var spawncoordz = BitConverter.ToUInt32(await SwitchConnection.ReadBytesAbsoluteAsync(outbreakptr - 0x0C, 4, token).ConfigureAwait(false), 0);
                        Log($"Group Seed: {string.Format("0x{0:X}", group_seed)}");
                        Log($"Massive Mass Outbreak found for: {species}{map} | Total Spawn Count: {spawncount} | Group ID: {groupcount}");
                        bool huntedspecies = list.Contains($"{species}");
                        (monlist, bonuslist) = ReadMMOSeed(spawncount, group_seed, bonuscount, encslot, bonusencslot);
                        foreach (PA8 pk in monlist)
                        {
                            count++;
                            var print = Hub.Config.StopConditions.GetAlphaPrintName(pk);
                            loglist.Add($"\nRegular Spawn: #{count}" + $"\nSpawner Coords: X - {spawncoordx:X8} Y - {spawncoordy:X8} Z - {spawncoordz:X8}" + print);

                            if (pk.IsShiny && pk.Species != (int)Species.None)
                            {
                                shinylist.Add(pk);
                                Log($"Autofilling SpawnZone XYZ for last shiny found.");
                                Settings.OutbreakConditions.GroupZoneX = $"{spawncoordx:X8}";
                                Settings.OutbreakConditions.GroupZoneY = $"{spawncoordy:X8}";
                                Settings.OutbreakConditions.GroupZoneZ = $"{spawncoordz:X8}";
                            }
                        }
                        count = 0;
                        foreach (PA8 pk in bonuslist)
                        {
                            count++;
                            var print = Hub.Config.StopConditions.GetAlphaPrintName(pk);
                            loglist.Add($"\nBonus Spawn: #{count}" + $"\nSpawner Coords: X - {spawncoordx:X8} Y - {spawncoordy:X8} Z - {spawncoordz:X8}" + print);

                            if (pk.IsShiny && pk.Species != (int)Species.None)
                            {
                                shinylist.Add(pk);
                                Log($"Autofilling SpawnZone XYZ for last shiny found.");
                                Settings.OutbreakConditions.GroupZoneX = $"{spawncoordx:X8}";
                                Settings.OutbreakConditions.GroupZoneY = $"{spawncoordy:X8}";
                                Settings.OutbreakConditions.GroupZoneZ = $"{spawncoordz:X8}";
                            }
                        }
                        count = 0;
                        if (huntedspecies && !Settings.OutbreakConditions.HuntAndScan)
                        {
                            PA8 pk = new();
                            pk.Species = (int)species;
                            EmbedMon = (pk, true);
                            Log($"{Hub.Config.StopConditions.MatchFoundEchoMention} Massive Mass Outbreak for {species} has been found{map}! Waiting for next command...");
                            IsWaiting = true;
                            while (IsWaiting)
                                await Task.Delay(1_000, token).ConfigureAwait(false);
                            if (!IsWaiting)
                                break;
                        }
                    };
                    string report = string.Join("\n", loglist);
                    Log(report);
                    loglist.Clear();
                    loglist = new();
                    groupcount++;
                    monlist.Clear();
                    monlist = new();
                    bonuslist.Clear();
                    bonuslist = new();
                } while (species != Species.None);
                foreach (PA8 pk in shinylist)
                {
                    bool huntedspecies = list.Contains($"{(Species)pk.Species}");
                    if (list.Length != 0)
                    {
                        if (!huntedspecies)
                            EmbedMon = (pk, false);
                        if (!huntedspecies && Settings.OutbreakConditions.MMOAlphaShinyOnly && pk.IsAlpha)
                            EmbedMon = (pk, true);
                    }
                    if (list.Length == 0 || huntedspecies)
                    {
                        if (Settings.OutbreakConditions.MMOAlphaShinyOnly && !pk.IsAlpha)
                            EmbedMon = (pk, false);
                        else
                            EmbedMon = (pk, true);
                    }
                    await Task.Delay(2_000, token).ConfigureAwait(false);
                    var print = Hub.Config.StopConditions.GetAlphaPrintName(pk);
                    Log(print);
                    if (EmbedMon.Item2 == true)
                    {
                        IsWaiting = true;
                        IsWaitingConfirmation = true;
                        Log($"Enter the desired map, type $continue and I'll teleport you to the location of {(Species)pk.Species}{map}!");
                        while (IsWaiting)
                        {
                            await Task.Delay(1_000, token).ConfigureAwait(false);
                            if (!IsWaitingConfirmation)
                            {
                                await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0xD65F03C0), MainNsoBase + InvincibleTrainer1, token).ConfigureAwait(false);
                                await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(0xD65F03C0), MainNsoBase + InvincibleTrainer2, token).ConfigureAwait(false);

                                await TeleportToMMOGroupZone(token).ConfigureAwait(false);
                                await Click(HOME, 1_000, token).ConfigureAwait(false);
                                Log($"Teleported to the location of {(Species)pk.Species}! Pressing HOME incase you weren't ready in game.");
                                while (IsWaiting)
                                    await Task.Delay(1_000, token).ConfigureAwait(false);
                            }
                        }
                    }
                }
                if (!IsWaiting)
                {
                    shinylist.Clear();
                    shinylist = new();
                    EmbedMon = (null, false);
                }
                mapcount++;
            }
        }
        public async Task MassiveOutbreakHunter(CancellationToken token)
        {
            await GetDefaultCoords(token);
            Log("Reading map for active outbreaks...");
            while (!token.IsCancellationRequested)
            {
                Log($"Search #{attempts}");
                if (Settings.OutbreakConditions.TeleportToHunt)
                {
                    await TeleportToSpawnZone(token);
                    await SetStick(LEFT, 0, -30_000, 1_000, token).ConfigureAwait(false);
                    await ResetStick(token).ConfigureAwait(false); // reset
                }
                if (!Settings.OutbreakConditions.TeleportToHunt)
                {
                    await SetStick(LEFT, 0, -30_000, 1_000, token).ConfigureAwait(false);
                    await Click(Y, 1_000, token).ConfigureAwait(false);
                    await ResetStick(token).ConfigureAwait(false); // reset
                }
                for (int i = 0; i < 1; i++)
                    await Click(A, 1_000, token).ConfigureAwait(false);

                var type = Settings.OutbreakConditions.TypeOfScan;
                switch (type)
                {
                    case OutbreakScanType.Both: await PerformOutbreakScan(token).ConfigureAwait(false); await PerformMMOScan(token).ConfigureAwait(false); break;
                    case OutbreakScanType.OutbreakOnly: await PerformOutbreakScan(token).ConfigureAwait(false); break;
                    case OutbreakScanType.MMOOnly: await PerformMMOScan(token).ConfigureAwait(false); break;
                }

                Log($"No match found, resetting.");
                await Click(A, 1_000, token).ConfigureAwait(false);
                await Click(A, 10_000, token).ConfigureAwait(false);
                mapcount = 0;
                // Loading screen
                if (Settings.OutbreakConditions.TeleportToHunt)
                {
                    await TeleportToCampZone(token).ConfigureAwait(false);
                    await SetStick(LEFT, -30_000, 0, 1_000, token).ConfigureAwait(false); // reset face forward
                    await ResetStick(token).ConfigureAwait(false); // reset
                }
                if (!Settings.OutbreakConditions.TeleportToHunt)
                {
                    await SetStick(LEFT, -10_000, 0, 0_500, token).ConfigureAwait(false);
                    await ResetStick(token).ConfigureAwait(false); // reset
                    await SetStick(RIGHT, -5_000, 0, 0_500, token).ConfigureAwait(false);
                    await SetStick(RIGHT, 0, 0, 0_500, token).ConfigureAwait(false);
                    await Click(ZL, 1_000, token).ConfigureAwait(false);
                    await Click(PLUS, 1_000, token).ConfigureAwait(false);
                    await PressAndHold(B, Settings.OutbreakConditions.HoldBMs, 0, token).ConfigureAwait(false);
                }
                await Click(A, 1_000, token).ConfigureAwait(false);
                await Click(A, 10_000, token).ConfigureAwait(false);
                attempts++;
            }
        }
    }
}