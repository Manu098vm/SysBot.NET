using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using SysBot.Base;

namespace SysBot.Pokemon
{
    public class OverworldSettingsSV : IBotStateSettings, ICountSettings
    {
        private const string FeatureToggle = nameof(FeatureToggle);
        private const string Counts = nameof(Counts);
        public override string ToString() => "OverworldSV Bot Settings";

        [Category(FeatureToggle), Description("When enabled, the bot will continue after finding a suitable match.")]
        public ContinueAfterMatch ContinueAfterMatch { get; set; } = ContinueAfterMatch.PauseWaitAcknowledge;

        [Category(FeatureToggle), Description("When enabled, the bot will attempt to set up a picnic to reset encounters. Set to true if hunting in Area Zero.")]
        public bool AreaZeroHunting { get; set; } = false;

        [Category(FeatureToggle), Description("Select which research station to go to if hunting in Area Zero.")]
        public ResearchStation StationSelection { get; set; } = ResearchStation.Station1;

        [Category(FeatureToggle), Description("When enabled, the bot will attempt to set up a picnic to reset encounters. Set to false if hunting in Area Zero.")]
        public bool CanWePicnic { get; set; } = true;

        [Category(FeatureToggle), Description("When enabled, the bot will make a sandwich on start.")]
        public bool EatFirst { get; set; } = true;

        [Category(FeatureToggle), Description("When enabled, the bot will click DUP on Item 1.")]
        public bool Item1DUP { get; set; } = false;

        //[Category(FeatureToggle), Description("Which item to use for ingredient 1.")]
      //  public PicnicIngredients Ingredient1 { get; set; } = PicnicIngredients.Baguette;

        [Category(FeatureToggle), Description("Amount of clicks to get to Item 1.")]
        public int Item1Clicks { get; set; } = 0;

        [Category(FeatureToggle), Description("When enabled, the bot will click DUP on Item 2.")]
        public bool Item2DUP { get; set; } = true;

      //  [Category(FeatureToggle), Description("Which item to use for ingredient 2.")]
       // public PicnicIngredients Ingredient2 { get; set; } = PicnicIngredients.Baguette;

        [Category(FeatureToggle), Description("Amount of clicks to get to Item 2.")]
        public int Item2Clicks { get; set; } = 0;

        [Category(FeatureToggle), Description("When enabled, the bot will click DUP on Item 3.")]
        public bool Item3DUP { get; set; } = true;

      //  [Category(FeatureToggle), Description("Which item to use for ingredient 3.")]
       // public PicnicIngredients Ingredient3 { get; set; } = PicnicIngredients.Baguette;

        [Category(FeatureToggle), Description("Amount of clicks to get to Item 3.")]
        public int Item3Clicks { get; set; } = 0;

        [Category(FeatureToggle), Description("Amount of ingredients to hold.")]
        public int AmountOfIngredientsToHold { get; set; } = 1;

        [Category(FeatureToggle), Description("Amount of time to hold L stick up to ingredients for sandwich. [Default: 700ms]")]
        public int HoldUpToIngredients { get; set; } = 700;

        [Category(FeatureToggle), Description("Set to true if we have a union circle active.")]
        public bool UnionCircleActive { get; set; } = false;

        [Category(FeatureToggle), Description("When enabled, the bot will only stop when encounter has a Scale of XXXS or XXXL.")]
        public bool MinMaxScaleOnly { get; set; } = false;

        [Category(FeatureToggle), Description("When enabled, the screen will be turned off during normal bot loop operation to save power.")]
        public bool ScreenOff { get; set; }

        private int _completedScans;

        [Category(Counts), Description("Encounters Scanned")]
        public int CompletedScans
        {
            get => _completedScans;
            set => _completedScans = value;
        }

        [Category(Counts), Description("When enabled, the counts will be emitted when a status check is requested.")]
        public bool EmitCountsOnStatusCheck { get; set; }

        public int AddCompletedEggs() => Interlocked.Increment(ref _completedScans);

        public IEnumerable<string> GetNonZeroCounts()
        {
            if (!EmitCountsOnStatusCheck)
                yield break;
            if (CompletedScans != 0)
                yield return $"Encounters Scanned: {CompletedScans}";
        }

        public enum ResearchStation
        {
            Station1 = 1,
            Station2 = 2,
            Station3 = 3,
            Station4 = 4,
        }
    }
}
