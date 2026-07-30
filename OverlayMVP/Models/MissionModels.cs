// filename: Models/MissionModels.cs
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OverlayMVP.Models
{
    // -----------------------------------------------------------------------
    // Missions
    // -----------------------------------------------------------------------

    public sealed class Mission
    {
        public int    Id          { get; set; }
        public string Title       { get; set; } = "";
        public string Description { get; set; } = "";
        public string Reward      { get; set; } = "";
        public string Status      { get; set; } = "open";
        public string CreatedBy   { get; set; } = "";
        public string AssignedTo  { get; set; } = "";
        public string CreatedAt   { get; set; } = "";
        public string UpdatedAt   { get; set; } = "";

        // Order-specific fields (CEO/coalition broadcasts — Task 3 "Orders" panel).
        // Not populated for regular agent-mission records.
        public string  TargetScope { get; set; } = "";
        public long?   TargetId    { get; set; }
        public string  ExpiresAt   { get; set; } = "";

        // ── Participation (Missions v2 · Slice 1) ─────────────────────────
        public double  RewardAmount    { get; set; }
        public string  RewardType      { get; set; } = "";
        public int?    MaxParticipants { get; set; }
        public int     SlotsTaken      { get; set; }
        /// <summary>null = not joined; otherwise joined | completed | verified.</summary>
        public string? MyState         { get; set; }

        public bool HasReward   => RewardAmount > 0;
        public string RewardLabel => HasReward
            ? $"{RewardAmount:N0} {RewardType}".Trim()
            : "";
        public string SlotsLabel => MaxParticipants.HasValue
            ? $"{SlotsTaken}/{MaxParticipants} slots"
            : $"{SlotsTaken} joined";
        public bool IsFull => MaxParticipants.HasValue
                              && SlotsTaken >= MaxParticipants.Value
                              && MyState is null;

        // Button visibility — exactly one primary action is available at a time.
        public bool CanJoin     => MyState is null && !IsFull;
        public bool CanLeave    => MyState == "joined";
        // Only free-form orders are self-reported. Bounties are settled by killmails
        // and standing goals by a server-side ESI read — the server refuses a manual
        // complete on both. Stated as a whitelist so a fourth type is excluded by
        // default rather than being forgotten here.
        public bool CanComplete => MyState == "joined" && MissionType == "freeform";
        public string StateLabel => MyState switch
        {
            "joined"    => "✔ Joined",
            "completed" => "✅ Completed — awaiting verification",
            "verified"  => "✅ Verified",
            _           => IsFull ? "FULL" : "",
        };

        public string StatusEmoji => Status switch
        {
            "open"        => "🔵",
            "in_progress" => "🟠",
            "completed"   => "✅",
            "cancelled"   => "❌",
            _             => "❓"
        };

        public string StatusLabel => Status.Replace("_", " ").ToUpperInvariant();

        // ── Kill bounty (Missions v2 · Slice 2) ───────────────────────────
        public string MissionType { get; set; } = "freeform";
        public int?   TargetUnits { get; set; }
        public int    MyKills     { get; set; }
        public int    TotalKills  { get; set; }

        public bool IsBounty => MissionType == "kill_bounty";
        /// <summary>Global bounties are credited automatically — manual submit is refused by the server.</summary>
        public bool CanSubmitKill => IsBounty && MyState is not null && TargetScope != "global";
        public string BountyProgressLabel => IsBounty
            ? $"{MyKills} by you · {TotalKills}{(TargetUnits.HasValue ? "/" + TargetUnits : "")} total"
            : "";
        public string BountyRewardLabel => IsBounty && RewardAmount > 0
            ? $"{RewardAmount:N0} {RewardType} per kill"
            : "";
        public string BountyAutoNote => IsBounty && TargetScope == "global"
            ? "Kills are credited automatically (~15 min)"
            : "";

        // ── Bounty qualification criteria ─────────────────────────────────
        // A pilot cannot tell whether a kill will pay without seeing these, so they
        // are surfaced in the Orders panel rather than left server-side.
        public double  KillMinIsk             { get; set; }
        public bool    KillFinalBlowOnly      { get; set; }
        public string? KillSystemIdsJson      { get; set; }
        public string? KillVictimCharIdsJson  { get; set; }
        public string? KillVictimCorpIdsJson  { get; set; }
        public string? KillVictimAllyIdsJson  { get; set; }

        /// <summary>Names of the bounty's targets, resolved from ESI by the view-model.</summary>
        public string TargetNames { get; set; } = "";

        private static int CountIds(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return 0;
            try
            {
                var arr = System.Text.Json.JsonSerializer.Deserialize<long[]>(json);
                return arr?.Length ?? 0;
            }
            catch { return 0; }
        }

        public bool HasTargets =>
            CountIds(KillVictimCharIdsJson) + CountIds(KillVictimCorpIdsJson) + CountIds(KillVictimAllyIdsJson) > 0;

        public string BountyTargetLabel => !IsBounty
            ? ""
            : HasTargets
                ? (string.IsNullOrWhiteSpace(TargetNames) ? "Targets: (resolving…)" : "Targets: " + TargetNames)
                : "Targets: anyone";

        /// <summary>The thresholds a kill must clear, in plain language.</summary>
        public string BountyRulesLabel
        {
            get
            {
                if (!IsBounty) return "";
                var parts = new System.Collections.Generic.List<string>();
                parts.Add(KillMinIsk > 0
                    ? $"min value {KillMinIsk:N0} ISK"
                    : "no minimum value");
                if (KillFinalBlowOnly) parts.Add("final blow only");
                int sys = CountIds(KillSystemIdsJson);
                if (sys > 0) parts.Add(sys == 1 ? "1 system only" : $"{sys} systems only");
                return string.Join(" · ", parts);
            }
        }

        // ── Standing goal (Missions v2 · Slice 3) ─────────────────────────
        public string? StandingTargetType   { get; set; }
        public long?   StandingTargetId     { get; set; }
        public string  StandingTargetName   { get; set; } = "";
        public double? StandingThreshold    { get; set; }
        public bool    StandingUseEffective { get; set; } = true;
        public bool    StandingMustEarn     { get; set; }
        /// <summary>Standing when this pilot joined. null = no snapshot (0.0 is a real value).</summary>
        public double? MyStandingAtJoin     { get; set; }
        /// <summary>Set by the view-model from local ESI data. Display only — the server decides.</summary>
        public double? CurrentStanding      { get; set; }

        public bool IsStandingGoal => MissionType == "standing_goal";
        public bool CanClaimStanding => IsStandingGoal && MyState == "joined";

        public string StandingGoalLabel => IsStandingGoal && StandingThreshold.HasValue
            ? $"Reach {StandingThreshold.Value:+0.00;-0.00} with {StandingTargetName}"
            : "";

        /// <summary>
        /// Local progress hint. The overlay's own reading is NOT authoritative —
        /// the server re-reads standings on claim and decides.
        /// </summary>
        public string StandingProgressLabel
        {
            get
            {
                if (!IsStandingGoal || !StandingThreshold.HasValue) return "";
                var basis = StandingUseEffective ? "effective" : "base";
                var now   = CurrentStanding.HasValue ? $"{CurrentStanding.Value:+0.00;-0.00}" : "—";
                var start = MyStandingAtJoin.HasValue
                    ? $" · joined at {MyStandingAtJoin.Value:+0.00;-0.00}"
                    : "";
                return $"{now} / {StandingThreshold.Value:+0.00;-0.00} ({basis}){start}";
            }
        }

        public string StandingTokenNote => IsStandingGoal
            ? "Joining and claiming send a one-time EVE token so the server can check your standing. It isn't stored."
            : "";
    }

    public sealed class MissionListResponse
    {
        public List<Mission> Missions { get; set; } = new();
    }

    // -----------------------------------------------------------------------
    // EVE Character
    // -----------------------------------------------------------------------

    public sealed class CharacterInfo
    {
        public long   CharacterId    { get; set; }
        public string CharacterName  { get; set; } = "Unknown Pilot";
        public string Corporation    { get; set; } = "";
        public string Alliance       { get; set; } = "";
        public string ShipName       { get; set; } = "";
        public string ShipType       { get; set; } = "";
        public string SolarSystem    { get; set; } = "";
        public string Region         { get; set; } = "";
        public float  SecurityStatus { get; set; }

        public string SecurityColour => SecurityStatus switch
        {
            >= 0.5f  => "#00FF80",
            >= 0.0f  => "#FFDD00",
            >= -5.0f => "#FF8800",
            _        => "#FF3333"
        };
    }

    // -----------------------------------------------------------------------
    // Intel
    // FIX: Type comes from Python as string ("gate_camp", "pirate", etc.)
    //      Using string avoids JSON enum deserialization failure.
    // -----------------------------------------------------------------------

    // Used only when SENDING intel from overlay → API
    public enum IntelType { Neutral, Pirate, GateCamp, Roaming, Clear }

    public sealed class IntelReport
    {
        public string System     { get; set; } = "";

        // Python returns "gate_camp" / "pirate" / "roaming" / "clear" / "neutral"
        [JsonPropertyName("type")]
        public string TypeRaw    { get; set; } = "neutral";

        public int    Count      { get; set; } = 1;
        public string Notes      { get; set; } = "";

        [JsonPropertyName("reported_by")]
        public string ReportedBy { get; set; } = "";

        // Python stores Unix timestamp as float
        [JsonPropertyName("reported_at")]
        public double ReportedAtUnix { get; set; }

        [JsonIgnore]
        public DateTime ReportedAt =>
            ReportedAtUnix > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds((long)(ReportedAtUnix * 1000)).UtcDateTime
                : DateTime.UtcNow;

        [JsonIgnore]
        public string TypeEmoji => TypeRaw switch
        {
            "gate_camp" => "⛔",
            "pirate"    => "💀",
            "roaming"   => "⚠️",
            "clear"     => "✅",
            _           => "👁️"
        };

        [JsonIgnore]
        public string TypeLabel => TypeRaw switch
        {
            "gate_camp" => "GATE CAMP",
            "pirate"    => "PIRATES",
            "roaming"   => "ROAMING",
            "clear"     => "CLEAR",
            _           => "NEUTRAL"
        };

        [JsonIgnore]
        public string AgeLabel
        {
            get
            {
                var age = DateTime.UtcNow - ReportedAt;
                if (age.TotalSeconds < 60)  return $"{(int)age.TotalSeconds}s ago";
                if (age.TotalMinutes < 60)  return $"{(int)age.TotalMinutes}m ago";
                return $"{(int)age.TotalHours}h ago";
            }
        }
    }

    // -----------------------------------------------------------------------
    // Snapshot response
    // -----------------------------------------------------------------------

    public sealed class OverlayDataResponse
    {
        public CharacterInfo?    Character { get; set; }
        public List<Mission>     Missions  { get; set; } = new();
        public List<IntelReport> Intel     { get; set; } = new();
    }

    // -----------------------------------------------------------------------
    // EVE Window (local — Win32 detection, not from API)
    // -----------------------------------------------------------------------

    public sealed class EveWindow
    {
        public IntPtr Handle        { get; set; }
        public string CharacterName { get; set; } = "";
        public string Title         { get; set; } = "";
        public bool   IsActive      { get; set; }

        public string DisplayLabel    => string.IsNullOrEmpty(CharacterName) ? "EVE Client" : CharacterName;
        public string ActiveIndicator => IsActive ? "▶ " : "    ";
    }
    // -----------------------------------------------------------------------
    // Faction Standings (ESI)
    // -----------------------------------------------------------------------
    public sealed class FactionStanding
    {
        [JsonPropertyName("faction_id")]
        public int    FactionId   { get; set; }

        [JsonPropertyName("faction_name")]
        public string FactionName { get; set; } = "";

        [JsonPropertyName("standing")]
        public float  Standing    { get; set; }

        [JsonPropertyName("modified")]
        public bool   Modified    { get; set; }
    }

    public sealed class StandingsResponse
    {
        public List<FactionStanding> Standings { get; set; } = new();
    }

}
