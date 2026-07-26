// filename: Models/MissionCatalogue.cs
// Tutorial-style EVE Online mission catalogue.
//
// Agent level unlock thresholds (standard EVE):
//   L1 = 0.0   L2 = 1.0   L3 = 3.0   L4 = 5.0   L5 = 7.0
//   (Access checks use EFFECTIVE standing — base standing modified by
//    Connections for positive / Diplomacy for negative, +4% per level.)
//
// HOW FACTION STANDING IS ACTUALLY GAINED (important — drives the guide):
//   - Regular missions (security/distribution/mining) raise ONLY agent and
//     CORPORATION standing. They do NOT directly raise faction standing.
//   - FACTION standing comes from: Storyline missions (auto-offered every 16
//     completed missions of the same level), COSMOS chains, career-agent
//     missions, datacenter (tag) turn-ins, and EPIC ARCS.
//   - The Social skill (+5%/level) boosts ALL standing GAINS (agent/corp/
//     faction). Connections/Diplomacy do NOT boost gains — they only raise
//     effective standing for access thresholds.
//   - Epic arcs grant a large faction boost (~+10% of the gap to 10, i.e.
//     ~+0.5 from 5.0) with NO derived standing loss, repeatable every 90 days.
//   - Distribution/courier missions kill nothing, so they incur NO combat-
//     derived loss with pirate/rival factions — the cleanest way to chain
//     toward storyline triggers. A storyline fires from a highsec agent if
//     your 16th regular mission's agent was in highsec.
//
// Each step includes:
//   - Standing required to access the agent level
//   - Estimated progression per run (corp-standing pace toward storylines)
//   - Enemy faction standing losses (where applicable)
//   - Tutorial guidance text explaining the progression step
//   - Standing gain type: direct faction, storyline-derived faction, etc.

using System.Collections.Generic;

namespace OverlayMVP.Models
{
    public enum MissionLevel    { L1 = 1, L2, L3, L4, L5 }
    public enum MissionType     { Security, Distribution, Mining, Research, COSMOS }
    public enum StandingGainType
    {
        DirectFaction,       // Mission/arc directly raises target faction standing
        DerivedFaction,      // Builds corp standing; faction standing arrives via storyline triggers
        COSMOS,              // One-time large boost, non-repeatable
        SocialSkillBoosted   // Gain scales with the Social skill (+5%/level)
    }

    public sealed class EnemyWarning
    {
        public string FactionName  { get; init; } = "";
        public float  StandingLoss { get; init; }          // negative value per run
        public string Colour       { get; init; } = "#FFEF5350";
        public string Note         { get; init; } = "";
    }

    public sealed class ShipSpec
    {
        public string[] Ships        { get; init; } = Array.Empty<string>(); // Recommended hulls
        public int      MinDps       { get; init; }      // Minimum DPS (0 = non-combat)
        public int      MinEhpK      { get; init; }      // EHP in thousands (60 = 60k EHP)
        public string[] ResistProfile{ get; init; } = Array.Empty<string>(); // ["EM","Thermal"]
        public string   EnemyNote    { get; init; } = ""; // Damage source / enemy name
        public int      MinCargoM3   { get; init; }      // Required cargo m³ (0 = N/A)
        public string   FitNote      { get; init; } = ""; // Fitting / strategy advice

        public bool   IsCombat   => MinDps > 0;
        public bool   NeedsCargo => MinCargoM3 > 0;
        public string DpsLabel   => MinDps > 0 ? $"{MinDps}+ DPS" : "Non-combat";
        public string EhpLabel   => $"{MinEhpK}k+ EHP";
        public string CargoLabel => MinCargoM3 > 0 ? $"{MinCargoM3:N0} m³ cargo" : "";
        public string ShipList   => string.Join("  /  ", Ships);
        public string ResistList => Ships.Length > 0 ? string.Join(" · ", ResistProfile) : "";
    }

    public sealed class TutorialStep
    {
        public string       StepLabel        { get; init; } = "";
        public string       MediaKey         { get; set; }  = "";  // {faction}_step{N} → Assets/clips/{MediaKey}.mp4 briefing
        public MissionLevel Level            { get; init; }
        public float        StandingRequired { get; init; }
        public float        StandingGoal     { get; init; }
        public string       WhyText          { get; init; } = "";
        public string       TipText          { get; init; } = "";
        public string       Agent            { get; init; } = "";
        public string       Station          { get; init; } = "";
        public string       Region           { get; init; } = "";
        public MissionType  Type             { get; init; }
        public float        EstGainPerRun    { get; init; }
        public StandingGainType GainType     { get; init; }
        public List<EnemyWarning> EnemyWarnings { get; init; } = new();
        public bool         IsCOSMOS         { get; init; } = false;
        public string       Corporation      { get; init; } = "";  // Which NPC corp to run missions for
        public string       CorpNote         { get; init; } = "";  // Why this corp / how faction standing is earned
        public bool         HasCorporation   => !string.IsNullOrEmpty(Corporation);
        public ShipSpec?    Spec             { get; init; }   // Ship / fitting recommendations
        public bool         HasSpec          => Spec is not null;

        public string LevelLabel => $"L{(int)Level}";
        public string TypeLabel  => Type.ToString().ToUpperInvariant();

        public bool  IsUnlocked(float s)  => s >= StandingRequired;
        public bool  IsCompleted(float s) => s >= StandingGoal;

        public float Progress(float s) =>
            StandingGoal <= StandingRequired ? 1f :
            System.Math.Clamp((s - StandingRequired) / (StandingGoal - StandingRequired), 0f, 1f);

        public string LevelColour => (int)Level switch
        {
            1 => "#FF66BB6A",
            2 => "#FF4FC3F7",
            3 => "#FFFFD54F",
            4 => "#FFFF9800",
            5 => "#FFEF5350",
            _ => "#FFE8EDF2"
        };

        public string StatusLabel(float s) =>
            !IsUnlocked(s)  ? $"Requires {StandingRequired:+0.0;-0.0;0.0}" :
            IsCompleted(s)  ? "Complete" :
            $"{s:+0.0;-0.0;0.0} / {StandingGoal:+0.0;-0.0;0.0}";

        public string StatusEmoji(float s) =>
            !IsUnlocked(s)  ? "🔒" :
            IsCompleted(s)  ? "✅" : "▶";

        public string GainLabel =>
            IsCOSMOS ? "ONE-TIME COSMOS" :
            $"~+{EstGainPerRun:0.00} corp / run";
    }

    public sealed class FactionCatalogue
    {
        public string              FactionName  { get; init; } = "";
        public string              Icon         { get; init; } = "";
        public string              Colour       { get; init; } = "#FF4FC3F7";
        public int                 EsiFactionId { get; init; }
        public string              IntroText    { get; init; } = "";
        public List<TutorialStep>  Steps        { get; init; } = new();

        public TutorialStep? GetActiveStep(float standing)
        {
            foreach (var step in Steps)
                if (step.IsUnlocked(standing) && !step.IsCompleted(standing))
                    return step;
            return Steps.Count > 0 ? Steps[^1] : null;
        }
    }

    // =========================================================================
    // Static catalogue data — 9 factions with full tutorial progression
    // =========================================================================
    public static class MissionCatalogueData
    {
        public static readonly List<FactionCatalogue> Factions = new()
        {
            // ── Caldari State ─────────────────────────────────────────────
            new FactionCatalogue
            {
                FactionName  = "CALDARI STATE",
                Icon         = "◈",
                Colour       = "#FF4FC3F7",
                EsiFactionId = 500001,
                IntroText    = "Gateway to Jita and The Forge. High standing unlocks the Caldari Navy LP store (Navy gear, implants) and Caldari R&D Datacore access. Fastest path: grind distribution to 5.0, then repeat the 'Penumbra' epic arc (agent Aursa Kunivuri, Expert Distribution, Josameto) every 90 days for +10% Caldari standing with no enemy-faction loss.",
                Steps = new()
                {
                    new TutorialStep
                    {
                        StepLabel        = "Step 1 — Start with Distribution",
                        Level            = MissionLevel.L1,
                        StandingRequired = 0.0f,
                        StandingGoal     = 1.0f,
                        WhyText          = "Distribution (hauling) missions build Caldari Navy CORP standing with zero combat risk and — crucially — no kills, so they cost you nothing with Guristas or other factions. Every 16 completed missions auto-triggers a Storyline mission, and storylines are what actually raise CALDARI STATE faction standing. Distribution is the cleanest way to chain toward those triggers. The Forge (around Jita/Perimeter) is dense with Caldari Navy agents.",
                        TipText          = "💡 Faction standing comes from the storyline you're offered every 16 missions — not from each hauling run. Train Social (+5%/level) to boost every gain; Connections only changes effective standing for access, not the gain itself. Use Agent Finder (Alt+F) → Caldari Navy → Level 1 → Distribution to pick the closest agent.",
                        Corporation      = "Caldari Navy",
                        CorpNote         = "Run Caldari Navy distribution to bank corp standing safely. Faction standing arrives in chunks from the storyline offered every 16 missions (a highsec storyline agent if your 16th agent was in highsec). This sets up the 5.0 standing needed to start the repeatable Penumbra epic arc later.",
                        Agent            = "Use Agent Finder (Alt+F) — Caldari Navy, Level 1, Distribution",
                        Station          = "The Forge — Caldari Navy stations around Jita/Perimeter",
                        Region           = "The Forge",
                        Type             = MissionType.Distribution,
                        EstGainPerRun    = 0.07f,
                        GainType         = StandingGainType.DerivedFaction,
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Badger", "Crane", "Heron" },
                            MinDps        = 0,
                            MinEhpK       = 2,
                            ResistProfile = new[]{ "Kinetic", "Thermal" },
                            EnemyNote     = "Guristas Pirates (if combat triggered)",
                            MinCargoM3    = 2000,
                            FitNote       = "Prioritise cargo space over tank — L1 distribution is non-combat. A T1 hauler (Badger) handles all cargo requirements. Fit Kinetic/Thermal hardeners in case of accidental combat spawn.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 2 — L2 Security Agents",
                        Level            = MissionLevel.L2,
                        StandingRequired = 1.0f,
                        StandingGoal     = 3.0f,
                        WhyText          = "At 1.0 standing L2 agents open up. L2 missions clear faster, so you reach storyline triggers (every 16 missions) quicker — and L2 storylines pay more faction standing than L1 ones, so climbing levels pays twice: faster triggers, bigger rewards. Distribution stays the safe choice — no kills, no enemy loss. If you switch to L2 Security for speed, accept that each run kills Guristas and chips their standing. Either way it's the storyline chunks that move Caldari State faction.",
                        TipText          = "💡 Stick with Caldari Navy. Prefer distribution if you want to keep all other standings clean; choose security only if you're happy taking the Guristas loss for faster clears. Use Agent Finder (Alt+F) → Caldari Navy → Level 2 to find the nearest agent.",
                        Corporation      = "Caldari Navy",
                        CorpNote         = "Caldari Navy is the recommended corp — its standing counts toward the 5.0 needed for the Penumbra epic arc, and its storyline agents grant Caldari State faction standing. State Peacekeepers is a valid alternative. Neither corp 'propagates' faction per run — faction comes from storylines and the epic arc.",
                        Agent            = "Use Agent Finder (Alt+F) — Caldari Navy, Level 2",
                        Station          = "The Forge — Caldari Navy stations near Jita",
                        Region           = "The Forge",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.20f,
                        GainType         = StandingGainType.DerivedFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Guristas Pirates", StandingLoss=-0.10f,
                                    Note="Security missions kill Guristas — standing loss per run. Minor unless you need Guristas null-sec." }
                        },
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Caracal", "Moa", "Cormorant", "Osprey" },
                            MinDps        = 150,
                            MinEhpK       = 15,
                            ResistProfile = new[]{ "Kinetic", "Thermal" },
                            EnemyNote     = "Guristas Pirates — Kinetic/Thermal damage",
                            MinCargoM3    = 0,
                            FitNote       = "Shield tank with T1 Kinetic/Thermal hardeners. Caracal with T2 Heavy Missiles is the classic L2 Caldari ship — great range, strong application. Aim for 150+ DPS and 15k+ EHP.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 3 — L3 Security Grind",
                        Level            = MissionLevel.L3,
                        StandingRequired = 3.0f,
                        StandingGoal     = 5.0f,
                        WhyText          = "L3 agents are the sweet spot for mid-tier pilots — decent ISK and battlecruiser-level content that clears L3 storylines fast, and each L3 storyline pays more faction standing than an L2 one, so climbing levels pays twice: faster triggers, bigger rewards. Distribution L3 keeps you clean; security L3 is faster but continues to cost Guristas standing. Reaching 5.0 here unlocks the Penumbra epic arc, which is the big faction payoff.",
                        TipText          = "💡 Storylines every 16 missions are still the only faction source at this stage. Use Agent Finder (Alt+F) → Caldari Navy → Level 3 to find the nearest L3 agent — favour distribution to protect your other standings.",
                        Corporation      = "Caldari Navy",
                        CorpNote         = "Caldari Navy L3 builds corp standing toward 5.0 and feeds L3 storylines. Lai Dai Protection Service is a viable alternative corp. Faction gain still arrives via storylines, not per-mission propagation.",
                        Agent            = "Use Agent Finder (Alt+F) — Caldari Navy, Level 3",
                        Station          = "The Forge — Caldari Navy stations",
                        Region           = "The Forge",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.35f,
                        GainType         = StandingGainType.DerivedFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Guristas Pirates", StandingLoss=-0.18f,
                                    Note="Continued Guristas standing loss." },
                            new() { FactionName="Gallente Federation", StandingLoss=-0.03f,
                                    Note="Minor Gallente loss from some political L3 chains. Diplomacy III offsets this." }
                        },
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Drake", "Ferox", "Nighthawk", "Cerberus" },
                            MinDps        = 300,
                            MinEhpK       = 60,
                            ResistProfile = new[]{ "Kinetic", "Thermal" },
                            EnemyNote     = "Guristas Pirates — Kinetic/Thermal damage",
                            MinCargoM3    = 0,
                            FitNote       = "Drake is the definitive L3 Caldari workhorse — passive shield regen, Heavy Missile Launcher IIs, T2 Kinetic/Thermal hardeners. Target 60k+ EHP and 300+ DPS. Nighthawk (Command Ship) for faster clears.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 4 — Penumbra Epic Arc + L4 Navy",
                        Level            = MissionLevel.L4,
                        StandingRequired = 5.0f,
                        StandingGoal     = 8.0f,
                        WhyText          = "At 5.0 Caldari standing the 'Penumbra' epic arc unlocks — start it from Aursa Kunivuri (Expert Distribution) in Josameto. Completing it grants +10% Caldari State faction standing (12.5% with Social V) with NO derived loss, repeatable every 90 days. This is the single fastest faction jump available. Between arc runs, grind L4 Caldari Navy for LP/ISK and L4 storylines — the highest-level storylines pay the biggest faction-standing chunk of any mission tier, on top of the epic arc itself.",
                        TipText          = "💡 Run Penumbra the moment it's available and again every 90 days. It does cost minor Gallente and ~3% Serpentis standing during the combat legs — trivial unless you grind those. L4 Caldari Navy LP store (Navy Raven, Navy Drake BPCs) funds the rest.",
                        Corporation      = "Expert Distribution (Penumbra epic arc) / Caldari Navy (L4 grind)",
                        CorpNote         = "Penumbra epic arc — Aursa Kunivuri, Expert Distribution, Josameto. Requires 5.0 with Caldari State faction OR Expert Distribution. +10% faction standing per completion, no derived loss, every 90 days. Caldari Navy remains the L4 LP/ISK corp and its storylines top up faction standing between arc runs.",
                        Agent            = "Aursa Kunivuri (Penumbra epic arc) — or Agent Finder for L4 Caldari Navy",
                        Station          = "Josameto (Penumbra start) / The Forge Caldari Navy stations for L4",
                        Region           = "The Forge",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.50f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Guristas Pirates", StandingLoss=-0.30f,
                                    Note="Heavy Guristas loss at L4. Avoid if you need Guristas/pirate LP." },
                            new() { FactionName="Gallente Federation", StandingLoss=-0.05f,
                                    Note="Minor Gallente loss. Diplomacy IV mitigates." }
                        },
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Golem", "Raven Navy Issue", "Raven", "Tengu" },
                            MinDps        = 700,
                            MinEhpK       = 150,
                            ResistProfile = new[]{ "Kinetic", "Thermal" },
                            EnemyNote     = "Guristas Pirates — Kinetic/Thermal damage",
                            MinCargoM3    = 0,
                            FitNote       = "Golem with T2 Cruise Missiles + Bastion Module is the meta — 1,200+ DPS, near-impenetrable tank in Bastion. Raven is the budget alternative (700+ DPS). Tengu for mobile missile blitzing. Always T2 Kinetic hardeners.",
                        },
                    },
                }
            },

            // ── Gallente Federation ───────────────────────────────────────
            new FactionCatalogue
            {
                FactionName  = "GALLENTE FEDERATION",
                Icon         = "⬡",
                Colour       = "#FF66BB6A",
                EsiFactionId = 500004,
                IntroText    = "Gallente standing opens Dodixie — the 2nd largest trade hub. Essential for Gallente ship pilots (Ishtar, Kronos, Megathron). Federation Navy LP store offers Navy Comet and Megathron Navy BPCs. Fastest path: grind distribution to 5.0, then repeat the 'Syndication' epic arc (agent Roineron Aviviere, Impetus, Dodixie) every 90 days for +10% Gallente standing with no derived loss.",
                Steps = new()
                {
                    new TutorialStep
                    {
                        StepLabel        = "Step 1 — Federation Navy, Algogille",
                        Level            = MissionLevel.L1,
                        StandingRequired = 0.0f,
                        StandingGoal     = 1.0f,
                        WhyText          = "Algogille is a major Federation Navy hub in Essence, a few jumps from Dodixie. Run Federation Navy DISTRIBUTION here to build corp standing with no kills — that protects your Caldari and pirate standings. Every 16 missions triggers a Storyline, and storylines are what raise Gallente Federation faction standing. If you prefer combat, L1 Security against Serpentis is fine but starts chipping Serpentis standing.",
                        TipText          = "💡 Faction standing comes from storylines (every 16 missions), not each run. Train Social for bigger gains. Use Agent Finder (Alt+F) → Federation Navy → Level 1 → Distribution for the cleanest grind toward the 5.0 needed for the Syndication epic arc.",
                        Corporation      = "Federation Navy",
                        CorpNote         = "Federation Navy is the recommended corp — its standing counts toward the 5.0 needed for the Syndication epic arc, and its storyline agents grant Gallente faction standing. There is no per-mission 'propagation'; faction comes from storylines and the epic arc.",
                        Agent            = "Use Agent Finder (Alt+F) — Federation Navy, Level 1, Distribution",
                        Station          = "Essence — Federation Navy stations around Algogille",
                        Region           = "Essence",
                        Type             = MissionType.Distribution,
                        EstGainPerRun    = 0.08f,
                        GainType         = StandingGainType.DerivedFaction,
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Nereus", "Iteron Mark V", "Catalyst" },
                            MinDps        = 0,
                            MinEhpK       = 2,
                            ResistProfile = new[]{ "Kinetic", "Thermal" },
                            EnemyNote     = "Serpentis (Kinetic/Thermal) only if combat triggered",
                            MinCargoM3    = 2000,
                            FitNote       = "Distribution is non-combat — prioritise cargo space. A Nereus (T1 Gallente industrial) handles all L1 courier volumes. Fit Kinetic/Thermal hardeners only as insurance against an accidental Serpentis spawn. If you'd rather run L1 Security, a Tristan with light drones works instead.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 2 — L2 Security, Algogille",
                        Level            = MissionLevel.L2,
                        StandingRequired = 1.0f,
                        StandingGoal     = 3.0f,
                        WhyText          = "At 1.0 standing, L2 agents open. L2 clears faster so you hit storyline triggers sooner, and L2 storylines pay more faction standing than L1 ones — leveling up pays twice: quicker triggers, bigger rewards. Distribution stays clean (no Serpentis loss); L2 Security is faster but costs Serpentis standing each run. Aim for 3.0 to unlock L3, on the way to the 5.0 that opens the Syndication epic arc.",
                        TipText          = "💡 Federation Navy is the core corp. FIO (Federal Intelligence Office) is a valid alternative Gallente corp if you want variety. Use Agent Finder (Alt+F) → Federation Navy → Level 2 — favour Distribution to keep other standings intact.",
                        Corporation      = "Federation Navy",
                        CorpNote         = "Federation Navy builds the corp standing that counts toward the Syndication epic arc; FIO (Federal Intelligence Office) is an alternative Gallente corp. Faction standing arrives via storylines, not per-mission propagation.",
                        Agent            = "Use Agent Finder (Alt+F) — Federation Navy, Level 2",
                        Station          = "Essence — Federation Navy stations around Algogille",
                        Region           = "Essence",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.18f,
                        GainType         = StandingGainType.DerivedFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Serpentis", StandingLoss=-0.10f,
                                    Note="Killing Serpentis NPCs reduces Serpentis standing. Low impact for most pilots." }
                        },
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Vexor", "Thorax", "Myrmidon", "Celestis" },
                            MinDps        = 175,
                            MinEhpK       = 18,
                            ResistProfile = new[]{ "Kinetic", "Thermal" },
                            EnemyNote     = "Serpentis — Kinetic/Thermal damage",
                            MinCargoM3    = 0,
                            FitNote       = "Vexor with Medium Drones is the L2 Gallente standard — excellent drone damage application. Armor tank with Kinetic/Thermal Energized Platings. Drones trained to V will carry you all the way to L4.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 3 — COSMOS Algintal (One-Time Boost)",
                        Level            = MissionLevel.L3,
                        StandingRequired = 3.0f,
                        StandingGoal     = 5.0f,
                        WhyText          = "Before grinding L3 regular missions, run the Algintal COSMOS chain — a one-time massive standing boost that cannot be repeated. Each COSMOS mission gives 3–8x normal standing. The full Algintal chain can push you from 3.0 close to 5.0 in a single session.",
                        TipText          = "💡 Prepare all required COSMOS mission items in advance (check eve-survival.org or EVE University wiki). Missing an item mid-chain wastes significant travel time.",
                        Corporation      = "Various COSMOS corps",
                        CorpNote         = "COSMOS agents belong to multiple Gallente corps — standing is granted directly to Gallente Federation faction. Non-repeatable one-time boost. No corp preference here — complete every available COSMOS agent.",
                        Agent            = "Multiple COSMOS agents in Algintal",
                        Station          = "Algintal system — see in-game agent finder",
                        Region           = "Sinq Laison",
                        Type             = MissionType.COSMOS,
                        EstGainPerRun    = 0.80f,
                        GainType         = StandingGainType.COSMOS,
                        IsCOSMOS         = true,
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Myrmidon", "Thorax", "Vexor Navy Issue", "Omen" },
                            MinDps        = 250,
                            MinEhpK       = 40,
                            ResistProfile = new[]{ "Kinetic", "Thermal" },
                            EnemyNote     = "Serpentis (Kinetic/Thermal); some Blood Raiders (EM/Thermal)",
                            MinCargoM3    = 1500,
                            FitNote       = "Battlecruiser recommended for tougher COSMOS encounters. Cargo hold for mission items — check eve-survival.org item list before departing. Some encounters involve Blood Raiders — carry EM hardeners as backup.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 4 — Syndication Epic Arc + L4 Navy",
                        Level            = MissionLevel.L4,
                        StandingRequired = 5.0f,
                        StandingGoal     = 8.0f,
                        WhyText          = "At 5.0 Gallente standing the 'Syndication' epic arc unlocks — start it from Roineron Aviviere (Impetus) in Dodixie. Take the 'Safe Return' ending for +10% Gallente Federation faction standing (no derived loss), repeatable every 90 days — the fastest faction jump available. Between arc runs, grind L4 Federation Navy for LP/ISK and L4 storylines — the highest-level storylines pay the biggest faction-standing chunk of any mission tier, on top of the epic arc itself.",
                        TipText          = "💡 Run Syndication every 90 days; choose the Gallente ending ('Safe Return'), not the Syndicate ending, for empire standing. Drone ships (Ishtar, Dominix) excel at L4 Serpentis content. LP store: Navy Comet and Megathron Navy Issue BPCs.",
                        Corporation      = "Impetus (Syndication epic arc) / Federation Navy (L4 grind)",
                        CorpNote         = "Syndication epic arc — Roineron Aviviere, Impetus, Dodixie. Requires 5.0 with Gallente faction OR Impetus. +10% Gallente standing per completion (Safe Return ending), no derived loss, every 90 days. Federation Navy remains the L4 LP/ISK corp; its storylines top up faction standing between arc runs.",
                        Agent            = "Roineron Aviviere (Syndication epic arc) — or Agent Finder for L4 Federation Navy",
                        Station          = "Dodixie — Impetus (Syndication) and Federation Navy L4 agents",
                        Region           = "Sinq Laison",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.50f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Serpentis", StandingLoss=-0.28f,
                                    Note="Heavy Serpentis standing loss per L4 run." },
                            new() { FactionName="Caldari State", StandingLoss=-0.05f,
                                    Note="Minor Caldari loss. Use Diplomacy IV to partially offset." }
                        },
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Ishtar", "Dominix", "Kronos", "Navy Megathron" },
                            MinDps        = 900,
                            MinEhpK       = 140,
                            ResistProfile = new[]{ "Kinetic", "Thermal" },
                            EnemyNote     = "Serpentis — Kinetic/Thermal; some Angel Cartel (Explosive/Kinetic)",
                            MinCargoM3    = 0,
                            FitNote       = "Ishtar with Garde II Sentry Drones is the blitz meta — 1,000+ effective DPS at range. Dominix for budget pilots (Ogre IIs + Wardens). Kronos Marauder for max efficiency. T2 Kinetic/Thermal tank mandatory.",
                        },
                    },
                }
            },

            // ── Amarr Empire ──────────────────────────────────────────────
            new FactionCatalogue
            {
                FactionName  = "AMARR EMPIRE",
                Icon         = "✦",
                Colour       = "#FFFFD54F",
                EsiFactionId = 500003,
                IntroText    = "Essential for Domain, Kador, Devoid operations. Imperial Navy LP store sells Navy Slicer and Navy Apocalypse BPCs. Fastest path: grind Ministry of Internal Order DISTRIBUTION to 5.0 (distribution avoids the heavy Minmatar combat loss), then repeat the 'Right to Rule' epic arc (agent Karde Romu, Kor-Azor Prime) every 90 days for +10% Amarr standing with no derived loss. WARNING: Amarr COMBAT missions cause significant Minmatar standing loss — distribution does not.",
                Steps = new()
                {
                    new TutorialStep
                    {
                        StepLabel        = "Step 1 — Ministry of Internal Order, L1",
                        Level            = MissionLevel.L1,
                        StandingRequired = 0.0f,
                        StandingGoal     = 1.0f,
                        WhyText          = "Run L1 Ministry of Internal Order (MIO) DISTRIBUTION in Domain. Distribution builds MIO corp standing with no kills, so it triggers none of the Minmatar loss that Amarr combat missions cause. Every 16 missions you're offered a Storyline — that's what raises Amarr Empire faction standing. MIO is the corp the Right to Rule epic arc keys off, so this same grind sets up the arc.",
                        TipText          = "💡 If you might grind Minmatar later, do that FIRST — Amarr combat losses to Minmatar are asymmetric and slow to recover. Distribution is safe; combat is not. Use Agent Finder (Alt+F) → Ministry of Internal Order → Level 1 → Distribution.",
                        Corporation      = "Ministry of Internal Order",
                        CorpNote         = "MIO distribution builds Amarr corp standing with no Minmatar loss. Faction standing arrives via the storyline every 16 missions, and reaching 5.0 MIO/Amarr unlocks the repeatable Right to Rule epic arc. There is no per-mission faction 'propagation'.",
                        Agent            = "Use Agent Finder (Alt+F) — Ministry of Internal Order, Level 1, Distribution",
                        Station          = "Domain — Ministry of Internal Order stations",
                        Region           = "Domain",
                        Type             = MissionType.Distribution,
                        EstGainPerRun    = 0.07f,
                        GainType         = StandingGainType.DerivedFaction,
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Bestower", "Tormentor", "Magnate", "Punisher" },
                            MinDps        = 0,
                            MinEhpK       = 2,
                            ResistProfile = new[]{ "EM", "Thermal" },
                            EnemyNote     = "Sansha's Nation (EM/Thermal) if combat triggered",
                            MinCargoM3    = 2500,
                            FitNote       = "Distribution only — no combat. A Bestower hauler maximises cargo and completes missions fastest. Fit EM hardeners in case of accidental aggro from Sansha NPCs.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 2 — L2 Security / MIO, Domain",
                        Level            = MissionLevel.L2,
                        StandingRequired = 1.0f,
                        StandingGoal     = 3.0f,
                        WhyText          = "L2 MIO clears faster, reaching storyline triggers sooner, and L2 storylines pay more faction standing than L1 ones — climbing levels pays twice: quicker triggers, bigger rewards. Note the tradeoff: L2 SECURITY kills Minmatar/Sansha NPCs (Minmatar standing loss), while L2 DISTRIBUTION stays clean. Sansha are the main combat enemy in Amarr space — full EM resist tank if you do go combat. Keep building toward 5.0 for the Right to Rule epic arc.",
                        TipText          = "💡 Prefer distribution if protecting Minmatar standing matters. For combat, Sansha deal heavy EM — an EM-hardened cruiser is mandatory. Note: Diplomacy raises your EFFECTIVE standing with factions you're negative with (helps access), but it does NOT reduce the base loss per run.",
                        Corporation      = "Ministry of Internal Order",
                        CorpNote         = "MIO is the recommended Amarr corp — its standing keys the Right to Rule epic arc, and its storylines grant Amarr faction standing. Imperial Navy is an alternative corp. Faction standing comes from storylines and the epic arc, not per-mission propagation.",
                        Agent            = "Use Agent Finder (Alt+F) — Ministry of Internal Order, Level 2",
                        Station          = "Domain — Ministry of Internal Order stations",
                        Region           = "Domain",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.18f,
                        GainType         = StandingGainType.DerivedFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Minmatar Republic", StandingLoss=-0.12f,
                                    Note="Amarr combat missions kill Minmatar NPCs — significant Minmatar standing loss per run." },
                            new() { FactionName="Sansha's Nation", StandingLoss=-0.08f, Colour="#FFCE93D8",
                                    Note="Sansha standing loss. Minor impact for most pilots." },
                        },
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Maller", "Omen", "Arbitrator", "Augoror" },
                            MinDps        = 150,
                            MinEhpK       = 16,
                            ResistProfile = new[]{ "EM", "Thermal" },
                            EnemyNote     = "Sansha's Nation — 50% EM damage, 50% Thermal",
                            MinCargoM3    = 0,
                            FitNote       = "⚠️ EM tank is MANDATORY — Sansha deal 50% EM damage. Maller with EM Ward Amplifier + Thermal Dissipation Amplifier. Cap stability is important — Sansha NPCs use Energy Neutralisers. Aim for 80%+ EM resist.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 3 — COSMOS Araz (One-Time Boost)",
                        Level            = MissionLevel.L3,
                        StandingRequired = 3.0f,
                        StandingGoal     = 5.0f,
                        WhyText          = "Run the Araz COSMOS constellation missions before grinding L3 regularly. One-time massive standing boosts can push you from 3.0 close to 5.0 in a single session. Non-repeatable — do this once, then fall back to regular L3 agents for the remainder.",
                        TipText          = "💡 Prepare all COSMOS mission items in advance. Diplomacy raises your effective standing with factions you're negative with (eases access) but does not reduce the base Minmatar loss — the only real way to avoid that loss is to skip the combat legs.",
                        Corporation      = "Various COSMOS corps",
                        CorpNote         = "COSMOS agents span multiple Amarr corps — standing is granted directly to the Amarr Empire faction (a genuine direct-faction source, unlike regular missions). Non-repeatable. Complete every available COSMOS agent regardless of corp.",
                        Agent            = "COSMOS agents in Araz constellation",
                        Station          = "Araz constellation — see in-game agent finder",
                        Region           = "Domain",
                        Type             = MissionType.COSMOS,
                        EstGainPerRun    = 0.80f,
                        GainType         = StandingGainType.COSMOS,
                        IsCOSMOS         = true,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Minmatar Republic", StandingLoss=-0.20f,
                                    Note="COSMOS missions still cause Minmatar loss. Diplomacy IV strongly recommended." },
                        },
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Harbinger", "Prophecy", "Maller", "Omen Navy Issue" },
                            MinDps        = 300,
                            MinEhpK       = 55,
                            ResistProfile = new[]{ "EM", "Thermal" },
                            EnemyNote     = "Sansha's Nation — EM/Thermal",
                            MinCargoM3    = 2000,
                            FitNote       = "Harbinger with T2 Medium Pulse Lasers handles all Araz COSMOS encounters. Full EM/Thermal armor tank. Cargo for mission items — prepare the full item list from eve-survival.org before departure. Diplomacy IV reduces Minmatar losses during this step.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 4 — Right to Rule Epic Arc + L4 MIO",
                        Level            = MissionLevel.L4,
                        StandingRequired = 5.0f,
                        StandingGoal     = 8.0f,
                        WhyText          = "At 5.0 Amarr/MIO standing the 'Right to Rule' epic arc unlocks — start it from Karde Romu in Kor-Azor Prime (you can accept it remotely). Completing it grants +10% Amarr Empire faction standing (12.5% with Social V) with NO derived loss, plus bonus MIO/Kor-Azor/Amarr Navy standing, repeatable every 90 days. This is the fastest faction jump. Between arc runs, grind L4 MIO for LP/ISK and storylines — L4 storylines pay the biggest faction-standing chunk of any mission tier, on top of the epic arc itself.",
                        TipText          = "💡 Run Right to Rule every 90 days. A Paladin with full EM tank is the Amarr L4 meta against Sansha. The arc's combat legs cost some Minmatar standing — distribution-only pilots still benefit from the arc's direct +10% reward.",
                        Corporation      = "Ministry of Internal Order",
                        CorpNote         = "Right to Rule epic arc — Karde Romu, Kor-Azor Prime (Kor-Azor region). Requires 5.0 with the Ministry of Internal Order corp OR the Amarr Empire faction. +10% Amarr faction standing per completion (no derived loss), every 90 days. MIO remains the L4 LP/ISK corp; its storylines top up faction standing between arc runs.",
                        Agent            = "Karde Romu (Right to Rule epic arc, Kor-Azor Prime) — or Agent Finder for L4 MIO",
                        Station          = "Kor-Azor Prime (Right to Rule start) / Domain MIO stations for L4",
                        Region           = "Domain / Kor-Azor",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.50f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Minmatar Republic", StandingLoss=-0.35f,
                                    Note="Severe Minmatar loss on L4 COMBAT runs. You can't keep both empires high via combat — pick a side, or run distribution/the epic arc which avoid most of it. Diplomacy only raises effective standing for access; it doesn't undo the base loss." },
                        },
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Paladin", "Abaddon", "Navy Apocalypse", "Apocalypse" },
                            MinDps        = 850,
                            MinEhpK       = 175,
                            ResistProfile = new[]{ "EM", "Thermal" },
                            EnemyNote     = "Sansha's Nation — EM/Thermal damage",
                            MinCargoM3    = 0,
                            FitNote       = "Paladin with Tachyon Beam Laser IIs + Bastion Module is the Amarr L4 meta — 1,100+ DPS, massive armor EHP. Full EM/Thermal tank required. Abaddon is the budget option (still 850+ DPS). EM Armor Pump rigs for resist capping.",
                        },
                    },
                }
            },

            // ── Minmatar Republic ─────────────────────────────────────────
            new FactionCatalogue
            {
                FactionName  = "MINMATAR REPUBLIC",
                Icon         = "⚙",
                Colour       = "#FFEF5350",
                EsiFactionId = 500002,
                IntroText    = "Essential for Rens and Heimatar access. Republic Fleet LP store offers Stabber Fleet Issue and Firetail BPCs. Minmatar combat fights Angel Cartel — great LP:ISK, but every kill costs Angel standing. Fastest faction path: grind Brutor Tribe distribution to 5.0, then repeat the 'Wildfire' epic arc (agent Arsten Takalo, Brutor Tribe, Frarn) every 90 days for +10% Minmatar standing with no derived loss.",
                Steps = new()
                {
                    new TutorialStep
                    {
                        StepLabel        = "Step 1 — Hek Distribution Start",
                        Level            = MissionLevel.L1,
                        StandingRequired = 0.0f,
                        StandingGoal     = 1.0f,
                        WhyText          = "Start with Brutor Tribe DISTRIBUTION near Hek or Rens. Distribution kills nothing, so it avoids the Angel Cartel loss that Minmatar combat causes — the clean way to build corp standing. Every 16 missions triggers a Storyline, which is what raises Minmatar Republic faction standing. Brutor Tribe is also the corp the Wildfire epic arc keys off, so this grind doubles as arc setup.",
                        TipText          = "💡 Train Social (+5%/level) to boost every gain — Connections only changes effective standing for access, not the gain itself. Use Agent Finder (Alt+F) → Brutor Tribe → Level 1 → Distribution in Metropolis/Heimatar.",
                        Corporation      = "Brutor Tribe",
                        CorpNote         = "Brutor Tribe distribution avoids Angel combat losses and builds the corp standing that keys the Wildfire epic arc. Faction standing arrives via storylines (every 16 missions), not per-mission propagation. Best safe starting corp for Minmatar standing.",
                        Agent            = "Use Agent Finder (Alt+F) — Brutor Tribe, Level 1, Distribution",
                        Station          = "Rens VI - Moon 8 - Brutor Tribe Treasury (or nearest Brutor Tribe station)",
                        Region           = "Heimatar",
                        Type             = MissionType.Distribution,
                        EstGainPerRun    = 0.07f,
                        GainType         = StandingGainType.DerivedFaction,
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Probe", "Burst", "Slasher", "Rifter" },
                            MinDps        = 0,
                            MinEhpK       = 2,
                            ResistProfile = new[]{ "Explosive", "Kinetic" },
                            EnemyNote     = "Angel Cartel (Explosive/Kinetic) if combat triggered",
                            MinCargoM3    = 2000,
                            FitNote       = "Distribution only — no combat needed. Probe frigate has the best cargo for its class. Fit Explosive hardeners as a precaution. A small industrial (Wreathe) is faster if you have training.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 2 — L2 Security, Rens",
                        Level            = MissionLevel.L2,
                        StandingRequired = 1.0f,
                        StandingGoal     = 3.0f,
                        WhyText          = "At 1.0, L2 agents open. L2 clears faster, reaching storyline triggers sooner, and L2 storylines pay more faction standing than L1 ones — climbing levels pays twice: quicker triggers, bigger rewards. Distribution stays clean; L2 SECURITY against Angel Cartel is faster but costs Angel standing every run (Explosive/Kinetic tank required). Either way it's the storylines that move Minmatar faction standing. Keep climbing toward 5.0 for the Wildfire epic arc.",
                        TipText          = "💡 Prefer distribution to keep Angel (and other) standings clean; choose security only if you accept the Angel loss for speed. Explosive/Kinetic tank for Angel combat. Use Agent Finder (Alt+F) → Brutor Tribe → Level 2.",
                        Corporation      = "Brutor Tribe",
                        CorpNote         = "Brutor Tribe is the recommended corp — it keys the Wildfire epic arc and its storylines grant Minmatar faction standing. Republic Fleet is a strong alternative once your standing allows it. Neither 'propagates' faction per run — faction comes from storylines and the epic arc.",
                        Agent            = "Use Agent Finder (Alt+F) — Brutor Tribe, Level 2, Security",
                        Station          = "Rens VI - Moon 8 - Brutor Tribe Treasury (or nearest Brutor Tribe station)",
                        Region           = "Heimatar",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.20f,
                        GainType         = StandingGainType.DerivedFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Angel Cartel", StandingLoss=-0.10f,
                                    Note="Angel Cartel standing loss per run. Minor unless you plan null-sec Angel space." }
                        },
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Rupture", "Stabber", "Bellicose", "Thrasher" },
                            MinDps        = 175,
                            MinEhpK       = 16,
                            ResistProfile = new[]{ "Explosive", "Kinetic" },
                            EnemyNote     = "Angel Cartel — Explosive/Kinetic damage",
                            MinCargoM3    = 0,
                            FitNote       = "Rupture is the classic L2 Minmatar ship — strong autocannon DPS, good tank. Explosive/Kinetic shield hardeners required. Angel frigates are fast and tackle — kill them first. Thrasher (destroyer) viable for blitzing.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 3 — L3 Republic Fleet, Rens",
                        Level            = MissionLevel.L3,
                        StandingRequired = 3.0f,
                        StandingGoal     = 5.0f,
                        WhyText          = "L3 Republic Fleet around Rens/Heimatar is a fast 3.0→5.0 corp-standing path, and frequent L3 storylines feed Minmatar faction standing along the way — paying more per storyline than L2 did, so leveling up pays twice: faster triggers, bigger rewards. A well-fitted battlecruiser handles Angel content cleanly — but remember each combat run still costs Angel standing. The goal is 5.0 to open the repeatable Wildfire epic arc.",
                        TipText          = "💡 Regular missions like 'Recon' build CORP standing and count toward your every-16 storyline — they don't grant faction standing by themselves. Use Agent Finder (Alt+F) → Republic Fleet → Level 3.",
                        Corporation      = "Republic Fleet",
                        CorpNote         = "Republic Fleet is the top Minmatar combat/LP corp; Brutor Tribe is the alternative (and keys the Wildfire arc). Once you hit 5.0 Minmatar faction the arc unlocks regardless of corp. Faction standing comes from storylines and the epic arc, not per-mission propagation.",
                        Agent            = "Use Agent Finder (Alt+F) — Republic Fleet, Level 3",
                        Station          = "Heimatar — Republic Fleet stations around Rens",
                        Region           = "Heimatar",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.34f,
                        GainType         = StandingGainType.DerivedFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Angel Cartel", StandingLoss=-0.18f,
                                    Note="Sustained Angel standing loss at L3." },
                            new() { FactionName="Amarr Empire", StandingLoss=-0.03f,
                                    Note="Minor Amarr loss from some political Minmatar chains." }
                        },
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Hurricane", "Cyclone", "Sleipnir", "Vagabond" },
                            MinDps        = 450,
                            MinEhpK       = 65,
                            ResistProfile = new[]{ "Explosive", "Kinetic" },
                            EnemyNote     = "Angel Cartel — Explosive/Kinetic damage",
                            MinCargoM3    = 0,
                            FitNote       = "Hurricane with T2 800mm AutoCannons is the L3 Minmatar king — fast, high DPS. Explosive/Kinetic shield tank. Kill tackle frigates first. Sleipnir (Command Ship) for faster clears with boosts. Target 450+ DPS and 65k+ EHP.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 4 — Wildfire Epic Arc + L4 Fleet",
                        Level            = MissionLevel.L4,
                        StandingRequired = 5.0f,
                        StandingGoal     = 8.0f,
                        WhyText          = "At 5.0 Minmatar standing the 'Wildfire' epic arc unlocks — start it from Arsten Takalo (Brutor Tribe) in Frarn. The 'Revelation' ending grants +10% Minmatar Republic faction standing with NO derived loss, repeatable every 90 days — the fastest faction jump. Between arc runs, grind L4 Republic Fleet for LP/ISK and storylines ('Angel Extravaganza' is the classic blitz) — L4 storylines pay the biggest faction-standing chunk of any mission tier, on top of the epic arc itself.",
                        TipText          = "💡 Run Wildfire every 90 days; take the 'Revelation' ending for the full +10%. A Machariel or Vargur tops L4 Angel content. The arc costs some Angel (and minor Ammatar) standing on its combat legs — distribution/arc avoid most of it.",
                        Corporation      = "Brutor Tribe (Wildfire epic arc) / Republic Fleet (L4 grind)",
                        CorpNote         = "Wildfire epic arc — Arsten Takalo, Brutor Tribe, Frarn (Heimatar). Requires 5.0 with Brutor Tribe OR Minmatar faction. +10% Minmatar standing per completion (Revelation ending), no derived loss, every 90 days. Republic Fleet stays the L4 LP/ISK corp; its storylines top up faction between arc runs.",
                        Agent            = "Arsten Takalo (Wildfire epic arc, Frarn) — or Agent Finder for L4 Republic Fleet",
                        Station          = "Frarn (Wildfire start) / Heimatar Republic Fleet stations for L4",
                        Region           = "Heimatar",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.50f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Angel Cartel", StandingLoss=-0.30f,
                                    Note="Heavy Angel standing loss at L4. Not recoverable without months of pirate COSMOS." },
                            new() { FactionName="Amarr Empire", StandingLoss=-0.05f,
                                    Note="Minor Amarr loss. Diplomacy IV recommended." }
                        },
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Machariel", "Vargur", "Tempest Fleet Issue", "Sleipnir" },
                            MinDps        = 1000,
                            MinEhpK       = 150,
                            ResistProfile = new[]{ "Explosive", "Kinetic" },
                            EnemyNote     = "Angel Cartel — Explosive/Kinetic damage",
                            MinCargoM3    = 0,
                            FitNote       = "Machariel is the 'Angel Extravaganza' blitz meta — 1,200+ DPS, speed to clear pockets fast. Vargur (Marauder) for Bastion Mode burst damage. Tempest Fleet Issue is the budget option. Explosive/Kinetic tank is mandatory.",
                        },
                    },
                }
            },

            // ── Sisters of EVE ────────────────────────────────────────────
            new FactionCatalogue
            {
                FactionName  = "SISTERS OF EVE",
                Icon         = "✚",
                Colour       = "#FFE8EDF2",
                EsiFactionId = 500016,   // Servant Sisters of EVE (was 500017 = Society of Conscious Thought)
                IntroText    = "SoE standing is universally valuable: Sisters Probe Launchers and the Sisters Expanded Probe Launcher are used by nearly every explorer. SoE combat fights pirates (Blood Raiders, Serpentis), so it's a low-collateral neutral grind. Note: the SoE epic arc 'The Blood-Stained Stars' rewards a CHOSEN EMPIRE faction (+0.7), NOT SoE standing — run it for empire standing, and grind regular SoE missions (storylines) to raise SoE itself. (SoE has no COSMOS constellation.)",
                Steps = new()
                {
                    new TutorialStep
                    {
                        StepLabel        = "Step 1 — Arnon Entry Missions",
                        Level            = MissionLevel.L1,
                        StandingRequired = 0.0f,
                        StandingGoal     = 1.0f,
                        WhyText          = "Sister Alitura at Arnon IX is the starting agent for the famous 'Blood-Stained Stars' epic arc — the best newbie content in EVE. SoE combat fights pirates (Blood Raiders/Serpentis), so it has no EMPIRE-faction penalties, ideal for keeping all four empire standings positive. Regular SoE missions raise SoE corp standing; SoE storylines raise SoE faction standing.",
                        TipText          = "💡 The arc reward is +0.7 to ONE empire faction of your choice (×1.25 with Social V), not SoE standing — pick the empire you most need. No empire penalties at L1. Repeatable every 90 days.",
                        Corporation      = "Sisters of EVE",
                        CorpNote         = "Sister Alitura (Arnon IX - Moon 3 - Sisters of EVE Bureau, Essence) starts 'The Blood-Stained Stars' epic arc. Its final reward is +0.7 to a CHOSEN EMPIRE faction with no derived loss — it does NOT raise SoE standing. To raise SoE itself, grind regular SoE missions — their storylines give the SoE faction standing. (SoE has no COSMOS.)",
                        Agent            = "Sister Alitura (Blood-Stained Stars epic arc starter)",
                        Station          = "Arnon IX - Moon 3 - Sisters of EVE Bureau",
                        Region           = "Essence",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.09f,
                        GainType         = StandingGainType.DirectFaction,
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Tristan", "Arbitrator", "Vexor", "Imicus" },
                            MinDps        = 75,
                            MinEhpK       = 3,
                            ResistProfile = new[]{ "EM", "Thermal" },
                            EnemyNote     = "Blood Raiders (EM/Thermal); Serpentis occasionally",
                            MinCargoM3    = 0,
                            FitNote       = "Any drone frigate handles L1 SoE content. Tristan or Arbitrator preferred — flexible drone bonuses handle varied enemy types. EM/Thermal tank for Blood Raiders who appear frequently.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 2 — L2 SoE Security",
                        Level            = MissionLevel.L2,
                        StandingRequired = 1.0f,
                        StandingGoal     = 3.0f,
                        WhyText          = "Correction: there is NO Sisters of EVE COSMOS (the empire COSMOS constellations are Amarr=Araz, Caldari=Okkelen, Gallente=Algintal, Minmatar=Ani). To go from 1.0 to 3.0, run regular L2 SoE missions near Arnon. SoE combat fights pirates only (Blood Raiders/Serpentis), so it costs you nothing with the empires. The L2 storylines every 16 missions are what raise SoE faction standing — and pay more per storyline than L1 ones did, so leveling up pays twice: faster triggers, bigger rewards.",
                        TipText          = "💡 No COSMOS shortcut for SoE — it's regular missions + storylines. Drone cruisers (Vexor, Arbitrator) handle L2 SoE content well. Use Agent Finder (Alt+F) → Sisters of EVE → Level 2.",
                        Corporation      = "Sisters of EVE",
                        CorpNote         = "Sisters of EVE is the only corp granting SoE faction standing. Faction standing comes from the storylines L2 missions trigger. The Blood-Stained Stars epic arc does NOT raise SoE standing (its reward is a chosen empire faction), so regular SoE missions are the real SoE-standing path.",
                        Agent            = "Use Agent Finder (Alt+F) — Sisters of EVE, Level 2",
                        Station          = "Essence — Sisters of EVE stations around Arnon",
                        Region           = "Essence",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.18f,
                        GainType         = StandingGainType.DerivedFaction,
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Vexor", "Myrmidon", "Omen", "Arbitrator" },
                            MinDps        = 175,
                            MinEhpK       = 18,
                            ResistProfile = new[]{ "EM", "Thermal" },
                            EnemyNote     = "Blood Raiders (EM/Thermal); Serpentis occasionally",
                            MinCargoM3    = 0,
                            FitNote       = "A drone cruiser (Vexor, Myrmidon) clears L2 SoE content easily. EM/Thermal armor tank for Blood Raiders. Drones trained up carry you through to L4 SoE later.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 3 — L3 SoE Security",
                        Level            = MissionLevel.L3,
                        StandingRequired = 3.0f,
                        StandingGoal     = 5.0f,
                        WhyText          = "Post-COSMOS, continue with L3 SoE Security near Arnon. A drone-focused ship (Ishtar, Vexor) excels here. SoE combat is pirate-only, so it keeps your empire standings clean. As with every faction, it's the L3 storylines (every 16 missions) that raise SoE faction standing, not each run — and L3 storylines pay more than L2 ones did, so leveling up pays twice: faster triggers, bigger rewards.",
                        TipText          = "💡 Drone ships are meta for SoE — Ishtar or Vexor Navy Issue for L3 content. Blood Raiders appear; tank EM/Thermal. Use Agent Finder (Alt+F) → Sisters of EVE → Level 3.",
                        Corporation      = "Sisters of EVE",
                        CorpNote         = "Sisters of EVE is the only corp that gives SoE faction standing — stick to SoE agents (Agent Finder will list the nearest). Build corp standing via L3 missions; SoE faction standing arrives from the storylines they trigger.",
                        Agent            = "Use Agent Finder (Alt+F) — Sisters of EVE, Level 3",
                        Station          = "Essence — Sisters of EVE stations around Arnon",
                        Region           = "Essence",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.30f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Blood Raider Covenant", StandingLoss=-0.12f, Colour="#FFCE93D8",
                                    Note="Some L3 SoE missions involve Blood Raiders. Minor unless you need Blood Raider low-sec space." }
                        },
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Ishtar", "Myrmidon", "Vexor Navy Issue", "Prophecy" },
                            MinDps        = 400,
                            MinEhpK       = 60,
                            ResistProfile = new[]{ "EM", "Thermal" },
                            EnemyNote     = "Blood Raiders (EM/Thermal) primary; Sansha occasional",
                            MinCargoM3    = 0,
                            FitNote       = "Ishtar with T2 Sentry Drones is the drone boat meta for L3 SoE. EM/Thermal armor or shield hardeners. Drone Durability rigged for survivability vs. Blood Raider tracking. Target 400+ DPS and 60k+ EHP.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 4 — L4 Sisters of EVE (End-Game)",
                        Level            = MissionLevel.L4,
                        StandingRequired = 5.0f,
                        StandingGoal     = 8.0f,
                        WhyText          = "L4 SoE missions are the gold standard for SoE LP. The Sisters Expanded Probe Launcher is consistently one of the best LP:ISK items in EVE. A drone battleship (Dominix, Megathron) is the meta against Blood Raiders/Sansha. Lanngisi (Metropolis) hosts a well-known highsec L4 SoE agent. Note: Sister Alitura at Arnon is the L1 epic-arc starter, NOT an L4 agent. L4 storylines pay the biggest SoE faction-standing chunk of any mission tier — since the epic arc's reward goes to a chosen empire faction, not SoE, these storylines are the single largest lever for SoE standing itself.",
                        TipText          = "💡 The Blood-Stained Stars epic arc (Sister Alitura, Arnon) repeats every 90 days but rewards a CHOSEN EMPIRE faction (+0.7), not SoE — it won't raise SoE standing. Build SoE faction via regular SoE missions and the storylines they trigger.",
                        Corporation      = "Sisters of EVE",
                        CorpNote         = "Verified live: Lanngisi III - Moon 2 - Sisters of EVE Bureau (Metropolis, 0.5) is a primary highsec L4 SoE hub; Agent Finder (Alt+F) → Sisters of EVE → Level 4 lists the nearest. Faction standing comes from the L4 storylines these agents trigger. The SoE epic arc is an empire-standing tool, not an SoE-standing tool.",
                        Agent            = "L4 SoE agent — Lanngisi (use Agent Finder for the exact agent)",
                        Station          = "Lanngisi III - Moon 2 - Sisters of EVE Bureau",
                        Region           = "Metropolis",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.55f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Blood Raider Covenant", StandingLoss=-0.25f, Colour="#FFCE93D8",
                                    Note="L4 SoE content regularly involves Blood Raiders." },
                            new() { FactionName="Sansha's Nation", StandingLoss=-0.15f, Colour="#FFCE93D8",
                                    Note="Sansha appear in some L4 SoE missions." }
                        },
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Dominix", "Bhaalgorn", "Kronos", "Megathron" },
                            MinDps        = 900,
                            MinEhpK       = 140,
                            ResistProfile = new[]{ "EM", "Thermal" },
                            EnemyNote     = "Blood Raiders (EM/Thermal) dominant; Sansha secondary",
                            MinCargoM3    = 0,
                            FitNote       = "Dominix with Garde II / Ogre II Sentry Drones is the SoE L4 meta — flexible drone load-out adapts to any spawn. Full EM/Thermal tank. Bhaalgorn for energy neutraliser support if cap pressure is an issue. Target 900+ DPS.",
                        },
                    },
                }
            },

            // ── CONCORD Assembly ──────────────────────────────────────────
            new FactionCatalogue
            {
                FactionName  = "CONCORD ASSEMBLY",
                Icon         = "⚖",
                Colour       = "#FF9575CD",
                EsiFactionId = 500006,   // CONCORD Assembly (was 500010 = Guristas Pirates)
                IntroText    = "⚠️ CONCORD Assembly has NO mission agents and NO epic arc — you cannot run CONCORD missions at all. Since the Havoc expansion (2023), CONCORD standing rises ONLY through DERIVED standing as you raise the four empire factions. So there is no direct CONCORD grind: build empire standing (via the steps above) and CONCORD follows automatically. (The Pacifier/Enforcer/Marshal hulls historically came from Project Discovery and events, not a farmable agent LP grind.)",
                Steps = new()
                {
                    new TutorialStep
                    {
                        StepLabel        = "Step 1 — No Agents: Raise via Empires",
                        Level            = MissionLevel.L1,
                        StandingRequired = 0.0f,
                        StandingGoal     = 1.0f,
                        WhyText          = "There is no CONCORD ship to fly here and no agent to visit — CONCORD has no missions. The only lever is the four empires: raising Caldari/Gallente/Amarr/Minmatar standing pulls CONCORD up through derived standing. Distribution-led empire grinding (above) is the cleanest way to do it without tanking pirate standings.",
                        TipText          = "💡 You can't target CONCORD directly. Just run the empire faction you care about; CONCORD rises as a side effect. Standing skills (Social/Connections/Diplomacy) apply to the empire gains that drive it.",
                        Corporation      = "(none — CONCORD has no agents)",
                        CorpNote         = "Confirmed live: CONCORD Assembly gives no missions and has no agents or epic arc. Since the Havoc expansion, its standing is gained ONLY via derived standing from the four empire factions. Treat this faction as a by-product of empire grinding, not a destination.",
                        Agent            = "N/A — CONCORD has no mission agents",
                        Station          = "N/A — raised via empire derived standing",
                        Region           = "—",
                        Type             = MissionType.Distribution,
                        EstGainPerRun    = 0.02f,
                        GainType         = StandingGainType.DerivedFaction,
                        Spec = null,
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 2 — Derived Standing Builds Slowly",
                        Level            = MissionLevel.L3,
                        StandingRequired = 3.0f,
                        StandingGoal     = 5.0f,
                        WhyText          = "As your empire standings climb, CONCORD drifts up with them — but derived gains are small, so reaching mid CONCORD standing takes a long empire grind. There is nothing CONCORD-specific to do; the fastest route is whichever empire epic arc you're already repeating, since each arc dumps a big chunk of empire standing every 90 days.",
                        TipText          = "💡 Running empire epic arcs (Right to Rule / Penumbra / Syndication / Wildfire) is the most efficient way to nudge CONCORD up, because their large empire-standing rewards feed the derived gain.",
                        Corporation      = "(none — CONCORD has no agents)",
                        CorpNote         = "Still no CONCORD agents at any level. Keep raising empire standing; CONCORD follows. If your empire standing drops, CONCORD can be repaired the same way — via empire derived standing.",
                        Agent            = "N/A — CONCORD has no mission agents",
                        Station          = "N/A — raised via empire derived standing",
                        Region           = "—",
                        Type             = MissionType.Distribution,
                        EstGainPerRun    = 0.02f,
                        GainType         = StandingGainType.DerivedFaction,
                        Spec = null,
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 3 — What CONCORD Standing Is For",
                        Level            = MissionLevel.L4,
                        StandingRequired = 5.0f,
                        StandingGoal     = 8.0f,
                        WhyText          = "High CONCORD standing is mostly a flavour/lore goal — there is no CONCORD LP grind to chase, since you can't earn CONCORD LP from agents. The Pacifier (Covert Ops), Enforcer (Recon) and Marshal (Black Ops) hulls were distributed via Project Discovery tiers and event rewards, not a farmable agent store. Don't plan a 'CONCORD grind'; it simply trails your empire standing.",
                        TipText          = "💡 If you want those CONCORD hulls, watch for Project Discovery / event reward tracks — not an agent grind. CONCORD standing itself has little practical gating for most pilots.",
                        Corporation      = "(none — CONCORD has no agents)",
                        CorpNote         = "No CONCORD agents exist at any level. High CONCORD standing comes only from sustained high empire standing via derived gains. Keep this faction in mind as a passive consequence, not a grind target.",
                        Agent            = "N/A — CONCORD has no mission agents",
                        Station          = "N/A — raised via empire derived standing",
                        Region           = "—",
                        Type             = MissionType.Distribution,
                        EstGainPerRun    = 0.02f,
                        GainType         = StandingGainType.DerivedFaction,
                        Spec = null,
                    },
                }
            },

            // ── ORE ───────────────────────────────────────────────────────
            new FactionCatalogue
            {
                FactionName  = "OUTER RING EXCAVATIONS",
                Icon         = "⛏",
                Colour       = "#FF8D6E63",
                EsiFactionId = 500014,   // Outer Ring Excavations (was 500024)
                IntroText    = "ORE standing unlocks the LP store (Mining Laser/Ice Harvester Upgrades, mining-barge BPCs). The HIGHSEC 'Fractured Legacy' epic arc (added in the Nov-2025 Catalyst expansion; the game's shortest arc at 3 missions, all near Kisogo in The Forge) is the safe, no-null-sec intro — it rewards the ORE 'Pioneer' mining destroyer plus a Mobile Phase Anchor BPC. Regular ORE MINING agents, however, are in null-sec (4C-B7X / NM-OEA, Outer Ring), so sustained ORE-standing grinding still requires null access.",
                Steps = new()
                {
                    new TutorialStep
                    {
                        StepLabel        = "Step 1 — Fractured Legacy Epic Arc (Highsec)",
                        Level            = MissionLevel.L1,
                        StandingRequired = 0.0f,
                        StandingGoal     = 1.0f,
                        WhyText          = "The safest ORE entry is the 'Fractured Legacy' epic arc — a short highsec L1 arc (3 missions, all within ~5-6 jumps of Kisogo in The Forge). It needs no null-sec and rewards the ORE 'Pioneer' mining destroyer plus a 1-run Mobile Phase Anchor BPC. Do this first for the ship and a taste of ORE content; head to the null-sec ORE mining agents when you want to grind ORE standing in volume.",
                        TipText          = "💡 Fractured Legacy is doable in a cheap T1 frigate or Venture — light highsec L1 content. Start it from Elias Peltonnen at Kisogo VII - AIR Laboratories (or find him via The Agency). Its main draw is the Pioneer hull; the exact ORE-standing gain is small and not the reason to run it.",
                        Corporation      = "Outer Ring Excavations",
                        CorpNote         = "Outer Ring Excavations is the only corp for ORE standing. Unlike the file's old claim, ORE DOES have a highsec path: the Fractured Legacy epic arc (Elias Peltonnen, Kisogo VII - AIR Laboratories, The Forge). For real ORE-standing volume, the regular mining agents in null-sec (Outer Ring) are still required.",
                        Agent            = "Elias Peltonnen — Fractured Legacy epic arc (ORE Technologies)",
                        Station          = "Kisogo VII - AIR Laboratories (The Forge, highsec)",
                        Region           = "The Forge",
                        Type             = MissionType.Mining,
                        EstGainPerRun    = 0.30f,
                        GainType         = StandingGainType.DirectFaction,
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Venture", "Catalyst", "Tristan" },
                            MinDps        = 0,
                            MinEhpK       = 3,
                            ResistProfile = Array.Empty<string>(),
                            EnemyNote     = "Highsec L1 — light, occasional rats only",
                            MinCargoM3    = 5000,
                            FitNote       = "Fractured Legacy is light highsec L1 content — a Venture or any cheap T1 frigate completes it. No null-sec risk and no special fit needed. Save the mining barges for the null-sec ORE agents in later steps.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 2 — L3 ORE Mining, Outer Ring (Null-sec)",
                        Level            = MissionLevel.L3,
                        StandingRequired = 3.0f,
                        StandingGoal     = 5.0f,
                        WhyText          = "L3 ORE mining missions require a Retriever minimum and request large ore volumes (15,000–40,000 m³). All still in null-sec at 4C-B7X. Mining missions give both ORE standing AND the mined ore as reward, making them very ISK-efficient — but the null-sec environment requires caution.",
                        TipText          = "💡 Use Agent Finder (Alt+F) → Outer Ring Excavations → Level 3 → Mining to confirm all available L3 agents. Keep an eye on local chat — evacuate immediately if hostiles appear.",
                        Corporation      = "Outer Ring Excavations",
                        CorpNote         = "All L3 ORE agents are in 4C-B7X (Outer Ring). Mining missions give ORE standing AND the mined ore as reward. Use a corp or alliance with null-sec access for safer mining sessions.",
                        Agent            = "Use Agent Finder (Alt+F) — Outer Ring Excavations L3 Mining",
                        Station          = "4C-B7X VI - Moon 1 - Outer Ring Excavations Refinery",
                        Region           = "Outer Ring",
                        Type             = MissionType.Mining,
                        EstGainPerRun    = 0.28f,
                        GainType         = StandingGainType.DirectFaction,
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Retriever", "Mackinaw", "Skiff" },
                            MinDps        = 0,
                            MinEhpK       = 20,
                            ResistProfile = Array.Empty<string>(),
                            EnemyNote     = "Non-combat — null-sec: watch local for hostiles at all times",
                            MinCargoM3    = 40000,
                            FitNote       = "Skiff has the highest tank of any mining barge — recommended for null-sec. Mackinaw for largest ore hold. Always have a hauler alt staged in system. Align to a citadel and be ready to warp the moment hostiles appear in local.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 3 — L4 ORE (End-Game, Null-sec)",
                        Level            = MissionLevel.L4,
                        StandingRequired = 5.0f,
                        StandingGoal     = 8.0f,
                        WhyText          = "L4 ORE missions in Outer Ring offer the best industrialist LP:ISK in EVE. Ice Harvester Upgrade II and Mining Laser Upgrade II BPCs are top LP store items. Orca hull BPCs also appear at higher standing tiers. A null-sec-safe mining fleet is required.",
                        TipText          = "💡 Ice Harvester Upgrade II BPC and Mining Laser Upgrade II are consistently the best LP:ISK in the ORE store. L4 agents are in both 4C-B7X and NM-OEA — use Agent Finder to chain all available agents.",
                        Corporation      = "Outer Ring Excavations",
                        CorpNote         = "L4 ORE mining agents are in 4C-B7X and NM-OEA (Outer Ring null-sec) — use Agent Finder (Alt+F) → Outer Ring Excavations → Level 4 → Mining for current agents. At 5.0+ the LP store opens fully — Ice Harvester Upgrade II and Mining Laser Upgrade II BPCs offer the best LP:ISK for industrialists.",
                        Agent            = "Use Agent Finder (Alt+F) — Outer Ring Excavations, Level 4, Mining (4C-B7X / NM-OEA)",
                        Station          = "4C-B7X VI - Moon 1 - Outer Ring Excavations Refinery",
                        Region           = "Outer Ring",
                        Type             = MissionType.Mining,
                        EstGainPerRun    = 0.55f,
                        GainType         = StandingGainType.DirectFaction,
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Hulk", "Mackinaw", "Orca (boost fleet)" },
                            MinDps        = 0,
                            MinEhpK       = 35,
                            ResistProfile = Array.Empty<string>(),
                            EnemyNote     = "Non-combat — null-sec: requires fleet support or excellent situational awareness",
                            MinCargoM3    = 60000,
                            FitNote       = "L4 ORE missions request very large ore volumes. Orca boosting a Hulk is the meta. A dedicated freighter alt moves ore out. Highly recommended: join a null-sec mining corp for fleet protection during L4 sessions.",
                        },
                    },
                }
            },

            // ── EDENCOM ───────────────────────────────────────────────────
            new FactionCatalogue
            {
                FactionName  = "EDENCOM",
                Icon         = "⚡",
                Colour       = "#FF29B6F6",
                EsiFactionId = 500027,   // EDENCOM (was 500026 — swapped with Triglavian)
                IntroText    = "EDENCOM standing allows travel through Triglavian-invaded Fortress systems without attack. Killing Triglavian NPCs raises EDENCOM and lowers Triglavian by the same amount — so the two are normally opposed. NOTE (verified live): standing skills (Social/Connections/Diplomacy) have NO effect on EDENCOM/Triglavian standing, and within Pochven shooting Rogue Drones/Sleepers/Drifters raises BOTH sides — so it's actually possible to recover or hold both with effort. It's a preference, not a strictly permanent lock.",
                Steps = new()
                {
                    new TutorialStep
                    {
                        StepLabel        = "Step 1 — Kill Triglavian Rats (Pochven)",
                        Level            = MissionLevel.L1,
                        StandingRequired = 0.0f,
                        StandingGoal     = 1.0f,
                        WhyText          = "The Triglavian Invasion events are over, so EDENCOM standing now comes from killing TRIGLAVIAN rats inside Pochven (entered via a cheap market filament). Each Triglavian kill raises EDENCOM and lowers Triglavian by the same amount. The benefit is safe passage through Fortress systems and EDENCOM LP store access.",
                        TipText          = "💡 To gain EDENCOM, kill the Triglavian-faction rats. Killing Drifters/Sleepers/Rogue Drones raises BOTH EDENCOM and Triglavian — handy if you want to keep both positive (0.01+) for Pochven travel. Standing skills do NOT affect these gains.",
                        Corporation      = "EDENCOM",
                        CorpNote         = "EDENCOM has no mission agents and no epic arc — standing comes from killing Triglavian rats in Pochven (and any remaining Fortress-system content). No corp choice; all anti-Triglavian combat feeds EDENCOM faction standing directly.",
                        Agent            = "N/A — Pochven / open-world combat (no agents)",
                        Station          = "Pochven (enter via market filament) or Fortress systems",
                        Region           = "Pochven / Fortress systems",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.05f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Triglavian Collective", StandingLoss=-0.05f, Colour="#FFFF6F00",
                                    Note="Killing Triglavian rats lowers Triglavian standing 1:1. Not permanent — Drifter/Sleeper/Drone kills raise both, so the split is recoverable." }
                        },
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Loki", "Tengu", "Legion", "T3 Cruiser" },
                            MinDps        = 400,
                            MinEhpK       = 60,
                            ResistProfile = new[]{ "Thermal", "Explosive" },
                            EnemyNote     = "Triglavian Collective — Thermal/Explosive (entropic disintegrators ramp damage)",
                            MinCargoM3    = 0,
                            FitNote       = "T3 Cruisers excel at invasion sites — high mobility limits disintegrator ramp-up damage. Kill Triglavian ships quickly before their weapons ramp to full damage. Thermal/Explosive hardeners. Speed tank is as important as passive EHP here.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 2 — Fortress System Content",
                        Level            = MissionLevel.L3,
                        StandingRequired = 3.0f,
                        StandingGoal     = 8.0f,
                        WhyText          = "Fortress systems (where EDENCOM won the invasion) still hold EDENCOM-aligned content, and high-value Triglavian/Drifter kills in Pochven keep raising EDENCOM. The EDENCOM LP store offers niche modules. Higher standing means safer Fortress travel.",
                        TipText          = "💡 Fortress systems and Pochven both have Triglavian spawns — bring a mobile, well-tanked ship. Keep at least 0.01 EDENCOM (and 0.01 Triglavian if you want Pochven travel) to avoid being shot by gate NPCs.",
                        Corporation      = "EDENCOM",
                        CorpNote         = "EDENCOM has no traditional mission agents and no epic arc — standing is earned entirely from Triglavian-invasion and Fortress-system combat (Minor/Major Conduits, defending stargates). There is no distribution or storyline path here.",
                        Agent            = "N/A — Fortress / invasion-zone combat (no formal agents)",
                        Station          = "Fortress & invaded systems — see the in-game Triglavian Invasion map",
                        Region           = "Multiple (Fortress/invasion systems)",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.25f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Triglavian Collective", StandingLoss=-0.18f, Colour="#FFFF6F00",
                                    Note="Continued heavy Triglavian standing loss." }
                        },
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Loki", "Tengu", "Navy Battlecruiser", "Sleipnir" },
                            MinDps        = 550,
                            MinEhpK       = 80,
                            ResistProfile = new[]{ "Thermal", "Explosive" },
                            EnemyNote     = "Triglavian Collective — disintegrators scale damage over 40 seconds",
                            MinCargoM3    = 0,
                            FitNote       = "Fortress systems spawn escalating Triglavian waves. Logistics support recommended for group content. Solo: T3 Cruiser with active tank. Kill Triglavian Drekavac/Vedmak ships first — they deal the most ramp damage.",
                        },
                    },
                }
            },

            // ── Triglavian Collective ─────────────────────────────────────
            new FactionCatalogue
            {
                FactionName  = "TRIGLAVIAN COLLECTIVE",
                Icon         = "△",
                Colour       = "#FFFF6F00",
                EsiFactionId = 500026,   // Triglavian Collective (was 500027 — swapped with EDENCOM)
                IntroText    = "Triglavian standing unlocks Pochven (the unique systems with exclusive resources), Damavik/Kikimora hull BPCs, and Entropic Disintegrator weapons. Normally opposed to EDENCOM — killing Triglavian NPCs trades EDENCOM for Triglavian standing. NOTE (verified live): standing skills do NOT affect Triglavian/EDENCOM standing, and in Pochven killing Rogue Drones/Sleepers/Drifters raises BOTH factions — so being positive to both is possible with effort, and the choice isn't strictly permanent.",
                Steps = new()
                {
                    new TutorialStep
                    {
                        StepLabel        = "Step 1 — Enter Pochven via Filament",
                        Level            = MissionLevel.L1,
                        StandingRequired = 0.0f,
                        StandingGoal     = 1.0f,
                        WhyText          = "Correction (verified live): Abyssal Deadspace does NOT give Triglavian standing — 'what happens in the Abyss stays in the Abyss'. Triglavian standing is earned by killing rats inside POCHVEN. Buy the cheapest inbound filament off the market and use it to enter Pochven — no prior standing is needed to enter this way. Kill rats once inside; Drifters give the biggest gains.",
                        TipText          = "💡 Pochven rats appear on directional scanner, making them easy to find. Killing Drifters/Sleepers/Rogue Drones raises BOTH Triglavian AND EDENCOM (no EDENCOM cost); killing EDENCOM-aligned rats raises Triglavian but costs EDENCOM. Aim for at least 0.01 with both factions to move around Pochven safely.",
                        Corporation      = "Triglavian Collective",
                        CorpNote         = "No mission agents exist — Triglavian standing comes from killing rats in Pochven (entered via market filaments). Abyssal Deadspace and the old Invasion events do NOT grant it. Drifters are the highest-value targets for standing.",
                        Agent            = "N/A — Pochven open-world rats",
                        Station          = "Enter Pochven via cheapest inbound filament (bought on market)",
                        Region           = "Pochven",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.05f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="EDENCOM", StandingLoss=-0.05f, Colour="#FF29B6F6",
                                    Note="Killing EDENCOM-aligned rats costs EDENCOM standing. But Drifter/Sleeper/Rogue-Drone kills raise BOTH — favour those to avoid the loss." }
                        },
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Gila", "Vexor", "Caracal", "Cerberus" },
                            MinDps        = 250,
                            MinEhpK       = 40,
                            ResistProfile = new[]{ "Thermal", "Explosive" },
                            EnemyNote     = "Pochven rats — keep moving to limit Triglavian disintegrator ramp-up",
                            MinCargoM3    = 0,
                            FitNote       = "A Gila or drone/missile cruiser handles entry-level Pochven rats. Match tank to the target set, stay aligned and mobile to reduce disintegrator ramp damage. Watch local/d-scan for hostile players — Pochven is dangerous space.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 2 — Grind Pochven Rats & Anomalies",
                        Level            = MissionLevel.L2,
                        StandingRequired = 1.0f,
                        StandingGoal     = 3.0f,
                        WhyText          = "Keep clearing rats across Pochven — on gates, stations, planets, POCOs and combat anomalies. Bigger targets give bigger standing, and Drifters are the top earners. The old Triglavian Invasion events are over, so Pochven itself is now the grind. Coordinating with other Kybernaut-side pilots speeds up the tougher anomalies.",
                        TipText          = "💡 Prioritise Drifters and large Sleeper/Rogue-Drone rats — they give the most standing and raise EDENCOM too (no EDENCOM loss). The Kybernauts Clade community coordinates Pochven group content.",
                        Corporation      = "Triglavian Collective",
                        CorpNote         = "All Triglavian standing now comes from Pochven PvE (no agents, no Invasion events). Use a cheap inbound filament to enter, clear high-value rats, and extract before hostile players catch you. Drifters remain the best standing-per-kill.",
                        Agent            = "N/A — Pochven open-world content",
                        Station          = "Pochven systems (enter via market filament)",
                        Region           = "Pochven",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.08f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="EDENCOM", StandingLoss=-0.05f, Colour="#FF29B6F6",
                                    Note="Only if you kill EDENCOM-aligned rats. Drifter/Sleeper/Drone kills raise both factions." }
                        },
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Gila", "Ishtar", "Cerberus", "Sacrilege" },
                            MinDps        = 500,
                            MinEhpK       = 80,
                            ResistProfile = new[]{ "Thermal", "Explosive" },
                            EnemyNote     = "Triglavian NPCs + EDENCOM defenders — mixed damage; stay moving to reduce ramp",
                            MinCargoM3    = 0,
                            FitNote       = "Gila is the Conduit meta — drone bonuses + shield tank + mobility make it ideal. Keep moving to limit disintegrator ramp-up. T3/T4 (Fierce/Raging) filaments give 4× standing but require better fits — upgrade gradually.",
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 3 — High Standing & Pochven End-Game",
                        Level            = MissionLevel.L4,
                        StandingRequired = 5.0f,
                        StandingGoal     = 8.0f,
                        WhyText          = "As Triglavian standing climbs, the Collective grants deeper access and safer movement through Pochven. The toughest Pochven combat anomalies and Drifter content are the best end-game: strong ISK, Bioadaptive Caches, mutaplasmid drops, and the fastest standing gains. This is also where most Triglavian LP is earned.",
                        TipText          = "💡 The Triglavian LP store carries Damavik/Kikimora hull BPCs (high value). Keep at least 0.01 with both Triglavian and EDENCOM so Pochven gate/structure NPCs don't shoot you while you travel.",
                        Corporation      = "Triglavian Collective",
                        CorpNote         = "High-end Pochven combat anomalies give the fastest Triglavian standing and the best ISK (Bioadaptive Caches, mutaplasmids). Triglavian LP from Pochven content buys Damavik/Kikimora BPCs in the LP store. Still no agents — it's all open-world Pochven PvE.",
                        Agent            = "N/A — Pochven open-world sites",
                        Station          = "Pochven systems (enter via market filament)",
                        Region           = "Pochven",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.40f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="EDENCOM", StandingLoss=-0.20f, Colour="#FF29B6F6",
                                    Note="Killing EDENCOM-aligned Pochven rats lowers EDENCOM; if it drops negative, Fortress-system EDENCOM NPCs will shoot you. Drifter/Sleeper/Drone kills avoid this (they raise both)." }
                        },
                        Spec = new ShipSpec
                        {
                            Ships         = new[]{ "Kikimora", "Damavik", "Gila", "Loki" },
                            MinDps        = 700,
                            MinEhpK       = 100,
                            ResistProfile = new[]{ "Thermal", "Explosive" },
                            EnemyNote     = "Triglavian Collective + Drifters in Pochven — Thermal primary",
                            MinCargoM3    = 0,
                            FitNote       = "Triglavian ships (Kikimora, Damavik) gain environment bonuses in Pochven — preferred if you can fly them. Gila remains top-tier for outsider pilots. Speed is critical — never sit still under disintegrator fire. Drifter wormholes in Pochven lead to unique content.",
                        },
                    },
                }
            },
        };

        // Assign lore-video briefing keys ({slug}_step{N}) to every step.
        // EDENCOM / TRIGLAVIAN have one narration covering the whole faction, so
        // their extra guide steps all point at that single clip.
        static MissionCatalogueData()
        {
            var slug = new Dictionary<string, string>
            {
                ["CALDARI STATE"] = "caldari", ["GALLENTE FEDERATION"] = "gallente",
                ["AMARR EMPIRE"] = "amarr",    ["MINMATAR REPUBLIC"] = "minmatar",
                ["SISTERS OF EVE"] = "soe",    ["CONCORD ASSEMBLY"] = "concord",
                ["OUTER RING EXCAVATIONS"] = "ore", ["EDENCOM"] = "edencom",
                ["TRIGLAVIAN COLLECTIVE"] = "triglavian",
            };
            var maxClip = new Dictionary<string, int> { ["edencom"] = 1, ["triglavian"] = 1 };
            foreach (var f in Factions)
            {
                if (!slug.TryGetValue(f.FactionName, out var s)) continue;
                int cap = maxClip.TryGetValue(s, out var m) ? m : int.MaxValue;
                for (int i = 0; i < f.Steps.Count; i++)
                    f.Steps[i].MediaKey = $"{s}_step{System.Math.Min(i + 1, cap)}";
            }
        }

        // Backward compatibility alias
        public static IReadOnlyList<FactionCatalogue> All => Factions;

        // Safe accessor — use this to avoid depending on property name
        public static IReadOnlyList<FactionCatalogue> GetAll() => Factions;
    }
}
