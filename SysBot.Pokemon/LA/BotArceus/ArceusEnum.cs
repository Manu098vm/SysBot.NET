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

    public enum ArceusMap
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

    public enum ShinyRolls
    {
        MMOPerfectCharm = 19,
        MMOLv10Charm = 17,
        MMOPerfectNoCharm = 16,
        MMOLv10NoCharm = 14,
        MMOOnly = 13,
        OutbreakPerfectCharm = 32,
        OutbreakLv10Charm = 30,
        OutbreakPerfectNoCharm = 29,
        OutbreakLv10NoCharm = 27,
        OutbreakOnly = 26,
        PerfectCharm = 7,
        CharmLv10 = 5,
        Perfect = 4,
        Lv10 = 2,
        NoBonusRolls = 1,
    }
}