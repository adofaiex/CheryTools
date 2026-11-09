using System;

namespace CheryTools.Sonnet.Shared
{
    // Version-independent judgement kinds. The first twelve members follow the 3.3.x HitMargin order,
    // which is also the bit layout of persisted judgement masks (Overlayer trigger nodes).
    // Perfect is the exact hit: plain Perfect on 3.3.x, XPerfect on 3.4.0+.
    internal enum SonnetJudgement
    {
        TooEarly,
        VeryEarly,
        EarlyPerfect,
        Perfect,
        LatePerfect,
        VeryLate,
        TooLate,
        Multipress,
        FailMiss,
        FailOverload,
        Auto,
        OverPress,
        Midspin,
        FailedFloor,
        PerfectMinus,
        PerfectPlus,
        Unknown
    }

    // Shared source file linked into the Overlayer and Tools projects (see SonnetWindowChrome for how
    // shared sources are compiled). HitMargin members are compiled into IL as integers, and 3.4.0 split
    // Perfect into PerfectMinus/XPerfect/PerfectPlus, shifting every later member by two. Code must
    // therefore never name a HitMargin member: game values are translated here by name at startup.
    internal static class SonnetJudgements
    {
        private static readonly SonnetJudgement[] FromGame;
        private static readonly int[] ToGame;

        static SonnetJudgements()
        {
            int kinds = (int)SonnetJudgement.Unknown;
            ToGame = new int[kinds];
            for (int i = 0; i < kinds; i++) ToGame[i] = -1;

            Array values = Enum.GetValues(typeof(HitMargin));
            int max = -1;
            foreach (object value in values) max = Math.Max(max, Convert.ToInt32(value));
            FromGame = new SonnetJudgement[max + 1];
            for (int i = 0; i < FromGame.Length; i++) FromGame[i] = SonnetJudgement.Unknown;

            foreach (object value in values)
            {
                int gameValue = Convert.ToInt32(value);
                SonnetJudgement kind = Parse(Enum.GetName(typeof(HitMargin), value));
                FromGame[gameValue] = kind;
                if (kind != SonnetJudgement.Unknown) ToGame[(int)kind] = gameValue;
            }
        }

        private static SonnetJudgement Parse(string name)
        {
            if (name == "XPerfect") return SonnetJudgement.Perfect;
            return Enum.TryParse(name, false, out SonnetJudgement kind) ? kind : SonnetJudgement.Unknown;
        }

        internal static SonnetJudgement Of(HitMargin margin)
        {
            int value = (int)margin;
            return value >= 0 && value < FromGame.Length ? FromGame[value] : SonnetJudgement.Unknown;
        }

        // Hit count of one kind; zero when the running game has no such judgement.
        internal static int Count(scrMarginTracker tracker, SonnetJudgement kind)
        {
            int[] counts = tracker?.hitMarginsCount;
            if (counts == null || kind == SonnetJudgement.Unknown) return 0;
            int gameValue = ToGame[(int)kind];
            return gameValue >= 0 && gameValue < counts.Length ? counts[gameValue] : 0;
        }

        internal static bool IsPerfect(SonnetJudgement kind)
        {
            return kind == SonnetJudgement.Perfect || kind == SonnetJudgement.PerfectMinus
                || kind == SonnetJudgement.PerfectPlus;
        }

        // Bit index in a judgement mask; signed perfects share the Perfect bit. -1 for unknown kinds.
        internal static int MaskBit(SonnetJudgement kind)
        {
            if (kind == SonnetJudgement.PerfectMinus || kind == SonnetJudgement.PerfectPlus) return (int)SonnetJudgement.Perfect;
            return kind == SonnetJudgement.Unknown ? -1 : (int)kind;
        }
    }
}
