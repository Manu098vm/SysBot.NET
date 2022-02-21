using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using SysBot.Base;

namespace SysBot.Pokemon
{
    public class ArceusBotSettings : IBotStateSettings
    {
        private const string Arceus = nameof(Arceus);
        private const string Counts = nameof(Counts);
        public override string ToString() => "Arceus Bot Settings";

        [Category(Arceus), Description("The method by which the bot will scan overworld Pokémon.")]
        public ArceusMode BotType { get; set; } = ArceusMode.PlayerCoordScan;

        [Category(Arceus), Description("Select the Location to Autofill Coords upon running PlayerCoordScan.")]
        public ArceusAutoFill AutoFillCoords { get; set; } = ArceusAutoFill.CampZone;

        [Category(Arceus), Description("Select the Location of the map you are hunting for.")]
        public ArceupMap ScanLocation { get; set; } = ArceupMap.ObsidianFieldlands;

        [Category(Arceus), Description("Enter number of advances to do.")]
        public int Advances { get; set; } = 1;             

        [Category(Arceus), Description("When enabled, the bot will stop the routine when match found.")]
        public bool StopOnMatch { get; set; } = false;

        [Category(Arceus), Description("When enabled, the screen will be turned off during normal bot loop operation to save power.")]
        public bool ScreenOff { get; set; } = false;

        [Category(Arceus), Description("Special Conditions"), DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public SpecialFiltersCategory SpecialConditions { get; set; } = new();

        [Category(Arceus), Description("AlphaScan Conditions"), DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public AlphaScanFiltersCategory AlphaScanConditions { get; set; } = new();

        [Category(Arceus), Description("Enter Discord channel ID(s) to post Arceus embeds to. Feature has to be initialized via \"$arceusEmbed\" after every client restart.")]
        public string ArceusEmbedChannels { get; set; } = string.Empty;

        [Category(Arceus)]
        [TypeConverter(typeof(AlphaScanFiltersCategoryConverter))]
        public class AlphaScanFiltersCategory
        {
            public override string ToString() => "AlphaScan Conditions";

            [Category(Arceus), Description("Enter number of advances to search.")]
            public int MaxAdvancesToSearch { get; set; } = 50;

            [Category(Arceus), Description("Enter number of shiny rolls.")]
            public int ShinyRolls { get; set; } = 2;

            [Category(Arceus), Description("Toggle true if you just entered the map and didn't spawn the Pokémon")]
            public bool InItSpawn { get; set; } = true;

            [Category(Arceus), Description("Toggle true if the spawn comes out at night only")]
            public bool NightSpawn { get; set; } = true;

            [Category(Arceus), Description("Toggle true if spawn is 100% alpha.")]
            public bool SpawnIsStaticAlpha { get; set; } = false;

            [Category(Arceus), Description("Toggle true if spawn is in water.")]
            public bool IsSpawnInWater { get; set; } = false;
        }

        [Category(Arceus)]
        [TypeConverter(typeof(SpecialFiltersCategoryConverter))]
        public class SpecialFiltersCategory
        {
            public override string ToString() => "Special Conditions";

            [Category(Arceus), Description("Will hunt for the desired outbreak species if not empty.")]
            public string[] SpeciesToHunt { get; set; } = { };

            [Category(Arceus), Description("Wait time between teleporting and scanning.")]
            public int WaitMsBetweenTeleports { get; set; } = 1000;

            [Category(Arceus), Description("Enter your player coordinates for Camp Location X Coordinate. Should be 8 characters long.")]
            public string CampZoneX { get; set; } = "";

            [Category(Arceus), Description("Enter your player coordinates for Camp Location Y Coordinate. Should be 8 characters long.")]
            public string CampZoneY { get; set; } = "";

            [Category(Arceus), Description("Enter your player coordinates for Camp Location Z Coordinate. Should be 8 characters long.")]
            public string CampZoneZ { get; set; } = "";

            [Category(Arceus), Description("Enter your player coordinates for Spawn Location X Coordinate. Should be 8 characters long.")]
            public string SpawnZoneX { get; set; } = "";

          [Category(Arceus), Description("Enter your player coordinates for Spawn Location Y Coordinate. Should be 8 characters long.")]
            public string SpawnZoneY { get; set; } = "";

            [Category(Arceus), Description("Enter your player coordinates for Spawn Location Z Coordinate. Should be 8 characters long.")]
            public string SpawnZoneZ { get; set; } = "";

            [Category(Arceus), Description("Wait time in minutes before starting a new distortion.")]
            public int WaitTimeDistortion { get; set; } = 2;
        }
        
        public class SpecialFiltersCategoryConverter : TypeConverter
        {
            public override bool GetPropertiesSupported(ITypeDescriptorContext context) => true;

            public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes) => TypeDescriptor.GetProperties(typeof(SpecialFiltersCategory));

            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType) => destinationType != typeof(string) && base.CanConvertTo(context, destinationType);
        }

        public class AlphaScanFiltersCategoryConverter : TypeConverter
        {
            public override bool GetPropertiesSupported(ITypeDescriptorContext context) => true;

            public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes) => TypeDescriptor.GetProperties(typeof(AlphaScanFiltersCategory));

            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType) => destinationType != typeof(string) && base.CanConvertTo(context, destinationType);
        }
    }
}
