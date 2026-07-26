// filename: Services/MissionProgressService.cs
// Calculates standing progression, mission counts, LP/ISK estimates,
// and tracks mission history for the Faction Standing Guide.

using System;
using System.Collections.Generic;
using System.Linq;

namespace OverlayMVP.Services
{
    // ── Key standing thresholds in EVE ────────────────────────────────────
    public static class StandingThresholds
    {
        // Verified vs EVE University (Agents): access uses EFFECTIVE standing
        // with the agent's corp OR faction. L1 needs no standing; L2 = 1.0,
        // L3 = 3.0, L4 = 5.0, L5 = 7.0 (L5 agents are in lowsec).
        public static readonly (double value, string label, string description)[] All =
        {
            (1.0,  "L2 Agents",    "Access to Level 2 agents (L1 needs no standing)"),
            (3.0,  "L3 Agents",    "Access to Level 3 agents"),
            (5.0,  "L4 Agents",    "Access to Level 4 agents"),
            (7.0,  "L5 Agents",    "Access to Level 5 agents (lowsec)"),
            (10.0, "Maximum",      "Maximum faction standing"),
        };

        // LP per mission by level — APPROXIMATE. Only the L4 range is sourced
        // (EVE Uni / player reports: a normal highsec L4 pays ~450–1,050 LP; burner
        // "Anomic" L4s are the outlier at ~20,000). The other levels are rough
        // estimates and are labelled as such in the UI.
        //
        // Actual payout follows:
        //   LP = Base LP × (1.6288 − system security) × (1 + Division Connections × 0.1)
        // so a 0.5-sec agent pays ~80% more than a 1.0-sec agent, and the
        // Security/Distribution/Mining Connections skills add up to +50% at level V.
        // Neither factor is modelled here — treat these as a conservative floor.
        public static readonly Dictionary<int, (int minLP, int maxLP, double iskPerLP)> LpByLevel = new()
        {
            [1] = (15,    40,    900),
            [2] = (50,    120,   900),
            [3] = (150,   350,   900),
            [4] = (450,   1050,  900),   // sourced
            [5] = (1400,  3200,  900),
        };
    }

    public sealed class StandingThresholdInfo
    {
        public double Value          { get; init; }
        public string Label          { get; init; } = "";
        public string Description    { get; init; } = "";
        public bool   IsUnlocked     { get; init; }
        public bool   IsNext         { get; init; }
        public double ProgressToThis { get; init; }  // 0-1
        public int    MissionsNeeded { get; init; }
        public string MissionsLabel  { get; init; } = "";
    }

    public sealed class MissionRecord
    {
        public string   CharacterName { get; init; } = "";
        public string   MissionName   { get; init; } = "";
        public string   AgentName     { get; init; } = "";
        public string   FactionKey    { get; init; } = "";
        public int      MissionLevel  { get; init; } = 4;
        public DateTime CompletedAt   { get; init; } = DateTime.UtcNow;
        public double   StandingGain  { get; init; }
        public int      EstimatedLP   { get; init; }
        public bool      SentToBot     { get; set; }
    }

    public sealed class LpEstimate
    {
        public int    Level     { get; init; }
        public int    MinLP     { get; init; }
        public int    MaxLP     { get; init; }
        public long   MinISK    { get; init; }
        public long   MaxISK    { get; init; }
        public string Label     { get; init; } = "";
    }

    public static class MissionProgressService
    {
        // ── Standing gain per mission (approximation) ────────────────────
        // Faction standing approaches 10 asymptotically, so we model the per-
        // step gain as base = 0.04 × (10 − currentStanding). The SOCIAL skill
        // boosts ALL standing gains by +5% per level. NOTE: Connections and
        // Diplomacy do NOT affect gains — they only raise effective standing
        // for access checks, so they must NOT be passed here.
        // (In real EVE, regular missions raise CORP standing; faction standing
        // arrives via the storyline offered every 16 missions — this folds that
        // into an averaged per-mission figure.)
        public static double StandingGainPerMission(double currentStanding, int socialLevel = 0)
        {
            double baseGain = 0.04 * (10.0 - currentStanding);
            return baseGain * (1.0 + 0.05 * socialLevel);
        }

        // ── How many missions to reach a target standing ─────────────────
        // Returns -1 if the target is not reachable within the iteration cap — this
        // happens whenever the target is 10.0 (asymptotically unreachable, since gain
        // = (10 − current) × increase never actually closes the gap). -1 means "no
        // countdown," never a raw iteration count that leaked out of the loop.
        public static int MissionsToTarget(double current, double target, int socialLevel = 0)
        {
            if (current >= target) return 0;
            double s = current;
            int count = 0;
            while (s < target && count < 10000)
            {
                s += StandingGainPerMission(s, socialLevel);
                count++;
            }
            return count >= 10000 ? -1 : count;
        }

        /// <summary>A storyline mission is offered every 16 completed missions of the same level.</summary>
        public const int MissionsPerStoryline = 16;

        /// <summary>
        /// FACTION standing gain from one storyline mission. Regular missions give NO
        /// faction standing — only storylines/epic arcs/COSMOS/career/datacenters do.
        /// Same shape as other gains: (10 − current) × increase, boosted by Social.
        /// </summary>
        public static double StorylineGain(double currentStanding, int socialLevel = 0)
        {
            double baseGain = 0.04 * (10.0 - currentStanding);
            return baseGain * (1.0 + 0.05 * socialLevel);
        }

        /// <summary>
        /// How many STORYLINE missions to reach a faction-standing target. Returns -1 if
        /// the target is not reachable within the iteration cap (see MissionsToTarget).
        /// </summary>
        public static int StorylinesToTarget(double currentStanding, double target, int socialLevel = 0)
        {
            if (currentStanding >= target) return 0;
            double s = currentStanding;
            int count = 0;
            while (s < target && count < 10000)
            {
                s += StorylineGain(s, socialLevel);
                count++;
            }
            return count >= 10000 ? -1 : count;
        }

        // ── Build threshold progression list ─────────────────────────────
        public static List<StandingThresholdInfo> GetThresholds(
            double currentStanding, int socialLevel = 0)
        {
            var result = new List<StandingThresholdInfo>();
            double? prevValue = null;

            bool foundNext = false;
            foreach (var (val, label, desc) in StandingThresholds.All)
            {
                bool unlocked = currentStanding >= val;
                bool isNext   = !unlocked && !foundNext;
                if (isNext) foundNext = true;

                double progress = 0;
                if (!unlocked && prevValue.HasValue)
                {
                    double from  = Math.Max(currentStanding, prevValue.Value);
                    double range = val - prevValue.Value;
                    progress = range > 0
                        ? Math.Clamp((currentStanding - prevValue.Value) / range, 0, 1)
                        : 0;
                }
                else if (unlocked) progress = 1.0;

                int needed = isNext ? MissionsToTarget(currentStanding, val, socialLevel) : 0;
                // -1 ("not reachable" — e.g. the asymptotic 10.0 ceiling) and the 10.0
                // display-only ceiling itself must never surface a countdown in the UI.
                string missLabel = (needed <= 0 || val >= 10.0) ? "" :
                    needed == 1 ? "1 mission" :
                    needed > 999 ? "999+ missions" : $"{needed} missions";

                result.Add(new StandingThresholdInfo
                {
                    Value          = val,
                    Label          = label,
                    Description    = desc,
                    IsUnlocked     = unlocked,
                    IsNext         = isNext,
                    ProgressToThis = progress,
                    MissionsNeeded = needed,
                    MissionsLabel  = missLabel,
                });
                prevValue = val;
            }
            return result;
        }

        // ── LP / ISK estimates ────────────────────────────────────────────
        public static List<LpEstimate> GetLpEstimates()
        {
            var result = new List<LpEstimate>();
            foreach (var (level, (minLP, maxLP, iskPerLP)) in StandingThresholds.LpByLevel)
            {
                result.Add(new LpEstimate
                {
                    Level  = level,
                    MinLP  = minLP,
                    MaxLP  = maxLP,
                    MinISK = (long)(minLP * iskPerLP),
                    MaxISK = (long)(maxLP * iskPerLP),
                    Label  = $"L{level}  {minLP:N0}–{maxLP:N0} LP  ({(long)(minLP*iskPerLP):N0}–{(long)(maxLP*iskPerLP):N0} ISK)",
                });
            }
            return result;
        }

        // ── Format standing gain text ─────────────────────────────────────
        public static string FormatGain(double gain)
            => gain >= 0 ? $"+{gain:F4}" : $"{gain:F4}";

        // ── ISK/h estimate based on level ─────────────────────────────────
        public static string IskPerHourEstimate(int level) => level switch
        {
            1 => "~5–15M ISK/h (approx.)",
            2 => "~15–30M ISK/h (approx.)",
            3 => "~30–60M ISK/h (approx.)",
            4 => "~60–100M ISK/h (approx.)",
            5 => "~100–200M ISK/h (approx.)",
            _ => "Unknown"
        };
    }
}
