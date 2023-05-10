namespace SysBot.Pokemon
{
    public enum OverworldMode
    {
        OffsetScan,
        OverworldScan,
        PlayerCoordScan,
        BallTosser,
    }
    public enum NavigationType
    {
        Run = 1,
        Teleportation = 2,
        RunDownTpUp = 3,
        FlyInPlace = 4,   
        Fishing = 5,
    }
    public enum CoordsAutoFill
    {
        NoAutoFill = 0,
        ScanZone = 1,
        DespawnZone = 2,
    }
    public enum UnwantedCorrection
    {
        ResetGame = 1,
        FleeBattle = 2,
        KnockOut = 3,
    }
    public enum NavigationOrder
    {
        DownUp = 1,
        DownDown = 2,
        RightLeft = 3,       
        //DownTpUp = 4,
    }

    public enum RefreshAndReplace
    {
        PartySlot1,
        PartySlot2,
        PartySlot3,
        PartySlot4,
        PartySlot5,
        PartySlot6,
    }

    public enum PokemonType
    {
        Normal = 0,
        Fight = 1,
        Flying = 2,
        Poison = 3,
        Ground = 4,
        Rock = 5,
        Bug = 6,
        Ghost = 7,
        Steel = 8,
        Fire = 9,
        Water = 10,
        Grass = 11,
        Electric = 12,
        Psychic = 13,
        Ice = 14,
        Dragon = 15,
        Dark = 16,
        Fairy = 17,
    }
    public enum BattleInjector
    {
        Gallade,
        Gardevoir,
    }
    public enum MovementToSpawn
    {
        No,
        WalkUp,
        Fishing,
    }

    public enum DisplaySeedMode
    {
        /// <summary>
        /// Copy out the global RNG state as a 128-bit value.
        /// </summary>
        Bit128,

        /// <summary>
        /// Copy out the global RNG state as 2 64-bit values.
        /// </summary>
        Bit64,

        /// <summary>
        /// Copy out the global RNG state as 2 64-bit values in the order PokeFinder expects.
        /// </summary>
        Bit64PokeFinder,

        /// <summary>
        /// Copy out the global RNG state as 4 32-bit values in the order CaptureSight displays.
        /// </summary>
        Bit32,
    }
}