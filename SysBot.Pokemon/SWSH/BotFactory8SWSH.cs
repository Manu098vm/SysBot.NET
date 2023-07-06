using System;
using PKHeX.Core;

namespace SysBot.Pokemon
{
    public sealed class BotFactory8SWSH : BotFactory<PK8>
    {
        public override PokeRoutineExecutorBase CreateBot(PokeTradeHub<PK8> Hub, PokeBotState cfg) => cfg.NextRoutineType switch
        {
            PokeRoutineType.FlexTrade or PokeRoutineType.Idle
                or PokeRoutineType.SurpriseTrade
                or PokeRoutineType.LinkTrade
                or PokeRoutineType.Clone
                or PokeRoutineType.Dump
                or PokeRoutineType.SeedCheck
                or PokeRoutineType.FixOT
                => new PokeTradeBotSWSH(Hub, cfg),

            PokeRoutineType.RemoteControl => new RemoteControlBotSWSH(cfg),
            _ => throw new ArgumentException(nameof(cfg.NextRoutineType)),
        };

        public override bool SupportsRoutine(PokeRoutineType type) => type switch
        {
            PokeRoutineType.FlexTrade or PokeRoutineType.Idle
                or PokeRoutineType.SurpriseTrade
                or PokeRoutineType.LinkTrade
                or PokeRoutineType.Clone
                or PokeRoutineType.Dump
                or PokeRoutineType.SeedCheck
                or PokeRoutineType.FixOT
                => true,

            PokeRoutineType.RemoteControl => true,

            _ => false,
        };
    }
}
