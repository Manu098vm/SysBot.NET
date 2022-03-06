namespace SysBot.Pokemon
{
    public enum ArceusMode
    {
        MassiveOutbreakHunter,
        SeedAdvancer,
        TimeSeedAdvancer,
        StaticAlphaScan,
        DistortionSpammer,
        DistortionReader,
        PlayerCoordScan,
    }
    public enum ArceusAutoFill
    {
        CampZone = 0,
        SpawnZone = 1,
    }

    public enum ArceupMap
    {
        ObsidianFieldlands = 0,
        CrimsonMirelands = 1,
        CobaltCoastlands = 2,
        CoronetHighlands = 3,
        AlabasterIcelands = 4,
    }

    public enum OutbreakScanType
    {
        Both = 0,
        OutbreakOnly = 1,
        MMOOnly = 2,
    }
}