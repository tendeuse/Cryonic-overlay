// filename: Models/MissionCatalogue.cs
// Tutorial-style EVE Online mission catalogue.
//
// Agent level unlock thresholds (standard EVE):
//   L1 = 0.0   L2 = 1.0   L3 = 3.0   L4 = 5.0   L5 = 7.0
//
// Each mission includes:
//   - Standing required to access the agent level
//   - Estimated standing gain per mission run
//   - Enemy faction standing losses (where applicable)
//   - Tutorial guidance text explaining the progression step
//   - Standing gain type: direct corp/faction, derived faction, etc.

using System.Collections.Generic;

namespace OverlayMVP.Models
{
    public enum MissionLevel    { L1 = 1, L2, L3, L4, L5 }
    public enum MissionType     { Security, Distribution, Mining, Research, COSMOS }
    public enum StandingGainType
    {
        DirectFaction,       // Mission directly raises target faction standing
        DerivedFaction,      // Corporation standing raises faction standing over time
        COSMOS,              // One-time large boost, non-repeatable
        SocialSkillBoosted   // Requires Connections/Diplomacy skills for full gain
    }

    public sealed class EnemyWarning
    {
        public string FactionName  { get; init; } = "";
        public float  StandingLoss { get; init; }          // negative value per run
        public string Colour       { get; init; } = "#FFEF5350";
        public string Note         { get; init; } = "";
    }

    public sealed class TutorialStep
    {
        public string       StepLabel        { get; init; } = "";
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
            $"~+{EstGainPerRun:0.00} / run";
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
                IntroText    = "Gateway to Jita and The Forge. High standing unlocks Caldari Navy LP store (Navy gear, implants) and Caldari R&D Datacore access. Essential for Caldari ship pilots and traders.",
                Steps = new()
                {
                    new TutorialStep
                    {
                        StepLabel        = "Step 1 — Start with Distribution",
                        Level            = MissionLevel.L1,
                        StandingRequired = 0.0f,
                        StandingGoal     = 1.0f,
                        WhyText          = "Distribution missions (hauling) build initial standing without combat risk. Run L1 distribution agents in The Forge to reach 1.0 standing quickly. Train Social V and Connections III before starting for a 10% standing gain boost on every mission.",
                        TipText          = "💡 Each Distribution mission gives ~0.05–0.10 faction standing. Connections III adds ~10% on top of that for free.",
                        Agent            = "Yuki Thomas",
                        Station          = "Jita IV - Moon 4 - Caldari Navy Assembly Plant",
                        Region           = "The Forge",
                        Type             = MissionType.Distribution,
                        EstGainPerRun    = 0.07f,
                        GainType         = StandingGainType.DerivedFaction,
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 2 — L2 Security Agents",
                        Level            = MissionLevel.L2,
                        StandingRequired = 1.0f,
                        StandingGoal     = 3.0f,
                        WhyText          = "At 1.0 standing you unlock L2 Security agents. Combat missions give much better standing gains than distribution. Focus on Caldari Navy agents in The Forge — standing earned applies to both Caldari Navy corp and Caldari State faction.",
                        TipText          = "💡 L2 missions need only a cruiser. Tank EM/Thermal against Guristas. Each run gives ~0.15–0.25 faction standing.",
                        Agent            = "Multiple L2 Security agents",
                        Station          = "Josameto VIII - Caldari Navy Assembly Plant",
                        Region           = "The Forge",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.20f,
                        GainType         = StandingGainType.DerivedFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Guristas Pirates", StandingLoss=-0.10f,
                                    Note="Security missions kill Guristas — standing loss per run. Minor unless you need Guristas null-sec." }
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 3 — L3 Security Grind",
                        Level            = MissionLevel.L3,
                        StandingRequired = 3.0f,
                        StandingGoal     = 5.0f,
                        WhyText          = "L3 agents are the sweet spot for mid-tier pilots — good ISK (~5–8M/hr), solid standing gains, battlecruiser-level. The 'Enemies Abound' chain and R&D agents unlock at 3.0 for passive Datacore income alongside combat missions.",
                        TipText          = "💡 Train Mechanic V to unlock Caldari R&D agents at 3.0 standing — free Datacores every day while you mission.",
                        Agent            = "Anka Ataira",
                        Station          = "Perimeter - Tranquility Trading Tower",
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
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 4 — L4 Caldari Navy (End-Game)",
                        Level            = MissionLevel.L4,
                        StandingRequired = 5.0f,
                        StandingGoal     = 8.0f,
                        WhyText          = "L4 Caldari Navy at Josameto is the most popular L4 hub in EVE. At 5.0 standing you access the full LP store. 'Blockade' and 'Worlds Collide' offer the best LP:ISK ratio. A Raven, Golem, or Tengu will clear most missions efficiently.",
                        TipText          = "💡 Decline 'Cargo Delivery' and 'Damsel in Distress' if blitzing for efficiency. The Caldari Navy LP store drops in value — sell LP store items promptly.",
                        Agent            = "Kylara Shivala",
                        Station          = "Josameto VIII - Caldari Navy Assembly Plant",
                        Region           = "The Forge",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.60f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Guristas Pirates", StandingLoss=-0.30f,
                                    Note="Heavy Guristas loss at L4. Avoid if you need Guristas/pirate LP." },
                            new() { FactionName="Gallente Federation", StandingLoss=-0.05f,
                                    Note="Minor Gallente loss. Diplomacy IV mitigates." }
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
                IntroText    = "Gallente standing opens Dodixie — the 2nd largest trade hub. Essential for Gallente ship pilots (Ishtar, Kronos, Megathron). Federal Navy LP store offers Navy Comet and Megathron Navy BPCs worth 300–600M ISK per batch.",
                Steps = new()
                {
                    new TutorialStep
                    {
                        StepLabel        = "Step 1 — Federal Navy Academy, Couster",
                        Level            = MissionLevel.L1,
                        StandingRequired = 0.0f,
                        StandingGoal     = 1.0f,
                        WhyText          = "Start at the Federal Navy Academy in Couster. L1 Security missions are low-risk and build initial Gallente standing fast. The Essence region around Couster has dense L1/L2 agent clusters — perfect for rapid early grinding.",
                        TipText          = "💡 Tank Kinetic/Thermal against Serpentis rats in Gallente space. A T1 frigate handles L1 easily.",
                        Agent            = "Bielle Gallix",
                        Station          = "Couster II - Moon 1 - Federal Navy Academy",
                        Region           = "Essence",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.08f,
                        GainType         = StandingGainType.DerivedFaction,
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 2 — L2 Security / FIO Villore",
                        Level            = MissionLevel.L2,
                        StandingRequired = 1.0f,
                        StandingGoal     = 3.0f,
                        WhyText          = "Move to L2 Security or FIO (Federal Intelligence Office) agents around Villore. FIO agents give notably higher faction standing than standard distribution — always prioritise security missions over hauling. Aim for 3.0 to access Dodixie L3 missions.",
                        TipText          = "💡 FIO chain in Villore gives higher faction standing than most L2 security agents. Check agent finder for FIO before picking a random L2.",
                        Agent            = "FIO L2 agents",
                        Station          = "Villore VII - Moon 8 - Quafe Company Factory",
                        Region           = "Essence",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.18f,
                        GainType         = StandingGainType.DerivedFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Serpentis", StandingLoss=-0.10f,
                                    Note="Killing Serpentis NPCs reduces Serpentis standing. Low impact for most pilots." }
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
                        Agent            = "Multiple COSMOS agents in Algintal",
                        Station          = "Algintal system — see in-game agent finder",
                        Region           = "Sinq Laison",
                        Type             = MissionType.COSMOS,
                        EstGainPerRun    = 0.80f,
                        GainType         = StandingGainType.COSMOS,
                        IsCOSMOS         = true,
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 4 — L4 Federation Navy, Dodixie",
                        Level            = MissionLevel.L4,
                        StandingRequired = 5.0f,
                        StandingGoal     = 8.0f,
                        WhyText          = "L4 Federation Navy at Dodixie is the top Gallente mission hub. 'Smash and Grab', 'Recon (1-3)', and 'Angel Extravaganza' are top LP:ISK missions. The LP store yields Navy Comet and Megathron Navy BPCs worth 300–600M ISK.",
                        TipText          = "💡 Drone-heavy ships (Ishtar, Dominix, Myrmidon) excel here — most Gallente L4 content is drone-friendly. Serpentis and Angel NPCs are the main enemies.",
                        Agent            = "Alles Seccant",
                        Station          = "Dodixie IX - Moon 20 - Federation Navy Assembly Plant",
                        Region           = "Sinq Laison",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.58f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Serpentis", StandingLoss=-0.28f,
                                    Note="Heavy Serpentis standing loss per L4 run." },
                            new() { FactionName="Caldari State", StandingLoss=-0.05f,
                                    Note="Minor Caldari loss. Use Diplomacy IV to partially offset." }
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
                IntroText    = "Essential for Domain, Kador, Devoid operations. Imperial Navy LP store sells Navy Slicer and Navy Apocalypse BPCs (400M–1B ISK each). High standing reduces NPC taxation in Amarr stations. WARNING: Amarr combat missions cause significant Minmatar standing loss.",
                Steps = new()
                {
                    new TutorialStep
                    {
                        StepLabel        = "Step 1 — Royal Amarr Institute, Distribution",
                        Level            = MissionLevel.L1,
                        StandingRequired = 0.0f,
                        StandingGoal     = 1.0f,
                        WhyText          = "Start with Distribution missions to build standing without triggering Minmatar faction loss. Critical note: if you plan to grind Minmatar standing later, finish that grind FIRST before starting Amarr combat missions — the standing losses are asymmetric and hard to recover.",
                        TipText          = "💡 WARNING: Do not start Amarr combat missions until you decide you're committed to the Amarr path. Distribution is safe — combat is not.",
                        Agent            = "Dihra Fassit",
                        Station          = "Penirgman IX - Moon 14 - Royal Amarr Institute School",
                        Region           = "Domain",
                        Type             = MissionType.Distribution,
                        EstGainPerRun    = 0.07f,
                        GainType         = StandingGainType.DerivedFaction,
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 2 — L2 Security / MIO, Domain",
                        Level            = MissionLevel.L2,
                        StandingRequired = 1.0f,
                        StandingGoal     = 3.0f,
                        WhyText          = "Move to L2 MIO (Ministry of Internal Order) Security agents in Domain. Sansha Nation is the primary enemy in Amarr space — full EM resist tank is mandatory. MIO agents provide excellent faction standing gains and unlock higher MIO levels quickly.",
                        TipText          = "💡 Full EM tank required: Sansha deal 50% EM damage. An EM-hardened cruiser clears L2 content in minutes. Train Diplomacy III now to reduce Minmatar losses.",
                        Agent            = "MIO L2 agents",
                        Station          = "Sarum Prime VI - Moon 1 - Sarum Family Academy",
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
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 3 — COSMOS Araz (One-Time Boost)",
                        Level            = MissionLevel.L3,
                        StandingRequired = 3.0f,
                        StandingGoal     = 5.0f,
                        WhyText          = "Run the Araz COSMOS constellation missions before grinding L3 regularly. One-time massive standing boosts can push you from 3.0 close to 5.0 in a single session. Non-repeatable — do this once, then fall back to regular L3 agents for the remainder.",
                        TipText          = "💡 Prepare all COSMOS mission items in advance. Train Diplomacy IV before starting to reduce Minmatar standing losses by ~25% per mission.",
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
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 4 — L4 Imperial Navy, Amarr (End-Game)",
                        Level            = MissionLevel.L4,
                        StandingRequired = 5.0f,
                        StandingGoal     = 8.0f,
                        WhyText          = "L4 Imperial Navy at Amarr VIII is the primary end-game Amarr hub. 'Damsel in Distress', 'Blockade', and 'The Assault' are top LP:ISK missions. The LP store offers Navy Slicer and Navy Apocalypse BPCs worth 400M–1B ISK. Marauders (Paladin) dominate here.",
                        TipText          = "💡 A Paladin with full EM tank is the meta for Amarr L4. Storyline agents at 8.0 standing offer rare implants. Combine with Caldari standings for maximum empire access.",
                        Agent            = "Aralin Jick",
                        Station          = "Amarr VIII (Oris) - Emperor Family Academy",
                        Region           = "Domain",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.62f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Minmatar Republic", StandingLoss=-0.35f,
                                    Note="Severe Minmatar loss at L4. Minmatar pilots cannot maintain both standings without heavy storyline mission use." },
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
                IntroText    = "Essential for Rens and Heimatar access. Republic Fleet LP store offers Stabber Fleet Issue and Firetail BPCs. Minmatar missions fight Angel Cartel — one of the best LP:ISK ratios in EVE. 'Angel Extravaganza' is a legendary L4 mission.",
                Steps = new()
                {
                    new TutorialStep
                    {
                        StepLabel        = "Step 1 — Hek Distribution Start",
                        Level            = MissionLevel.L1,
                        StandingRequired = 0.0f,
                        StandingGoal     = 1.0f,
                        WhyText          = "Start with distribution agents near Hek or Rens. Building initial Minmatar standing via distribution avoids Angel Cartel combat standing loss early on. Train Social V and Connections III for faster gains — especially important for new pilots.",
                        TipText          = "💡 Use in-game agent finder filtered by 'Distribution' and 'Brutor Tribe' for quickest access in Metropolis/Heimatar.",
                        Agent            = "Balle Ongrard",
                        Station          = "Hek VIII - Moon 12 - Boundless Creation Factory",
                        Region           = "Metropolis",
                        Type             = MissionType.Distribution,
                        EstGainPerRun    = 0.07f,
                        GainType         = StandingGainType.DerivedFaction,
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 2 — L2 Security, Rens",
                        Level            = MissionLevel.L2,
                        StandingRequired = 1.0f,
                        StandingGoal     = 3.0f,
                        WhyText          = "Move to L2 Security agents at Rens. Republic Fleet agents fight Angel Cartel — Explosive/Kinetic tank required. The faster standing gain from combat makes L2 the transition point where growth accelerates significantly. Run security every time one is available.",
                        TipText          = "💡 Explosive/Kinetic tank for Angel Cartel. A well-fitted destroyer handles L2 content. Security missions over distribution whenever available.",
                        Agent            = "Acassa Midular",
                        Station          = "Rens VI - Moon 8 - Brutor Tribe Treasury",
                        Region           = "Heimatar",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.20f,
                        GainType         = StandingGainType.DerivedFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Angel Cartel", StandingLoss=-0.10f,
                                    Note="Angel Cartel standing loss per run. Minor unless you plan null-sec Angel space." }
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 3 — L3 Republic Fleet, Rens",
                        Level            = MissionLevel.L3,
                        StandingRequired = 3.0f,
                        StandingGoal     = 5.0f,
                        WhyText          = "L3 Republic Fleet at Rens is one of the fastest 3.0 to 5.0 paths in EVE. The 'Recon (1-3)' chain is the single best L3 standing booster — accept it every time. A well-fitted battlecruiser handles everything cleanly.",
                        TipText          = "💡 'Recon' 3-part chain gives ~+0.8 faction standing total. Accept it every time it appears. Completing the chain in one session is faster than individual missions.",
                        Agent            = "Karin Midular",
                        Station          = "Frarn VI - Moon 8 - Republic Fleet Assembly Plant",
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
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 4 — L4 Republic Fleet (End-Game)",
                        Level            = MissionLevel.L4,
                        StandingRequired = 5.0f,
                        StandingGoal     = 8.0f,
                        WhyText          = "L4 Republic Fleet at Rens is among the highest ISK/hr content in EVE. 'Angel Extravaganza' is the crown jewel — blitz pockets 1/3/4, fight only pocket 2 for maximum efficiency. Republic Fleet LP store: Stabber Fleet Issue and Firetail BPCs worth 200–500M ISK.",
                        TipText          = "💡 Fit a Sleipnir or Machariel for top L4 performance. 'Angel Extravaganza' 4-pocket blitz: skip pockets 1/3/4, clear only pocket 2 for fast clear.",
                        Agent            = "Acassa Midular",
                        Station          = "Rens VI - Moon 8 - Brutor Tribe Treasury",
                        Region           = "Heimatar",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.60f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Angel Cartel", StandingLoss=-0.30f,
                                    Note="Heavy Angel standing loss at L4. Not recoverable without months of pirate COSMOS." },
                            new() { FactionName="Amarr Empire", StandingLoss=-0.05f,
                                    Note="Minor Amarr loss. Diplomacy IV recommended." }
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
                EsiFactionId = 500017,
                IntroText    = "SoE standing is universally valuable: Sisters Probe Launchers and Probes are used by nearly every pilot in EVE. SoE standing gains also raise all four empire standings simultaneously — making this the best neutral grinding choice for pilots wanting cross-empire access with zero faction penalties.",
                Steps = new()
                {
                    new TutorialStep
                    {
                        StepLabel        = "Step 1 — Arnon Entry Missions",
                        Level            = MissionLevel.L1,
                        StandingRequired = 0.0f,
                        StandingGoal     = 1.0f,
                        WhyText          = "SoE L1 missions at Arnon have zero enemy faction penalties — ideal for pilots who need to keep all empire standings positive. SoE standing gains also give small increases to all four empire standings simultaneously via 'derived standing', making this the safest possible starting faction.",
                        TipText          = "💡 Zero faction standing penalties at L1. Perfect starting faction for new players who don't want to accidentally lose access to trade hubs.",
                        Agent            = "Sister Alitura",
                        Station          = "Arnon IX - Moon 3 - Sisters of EVE Bureau",
                        Region           = "Genesis",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.09f,
                        GainType         = StandingGainType.DirectFaction,
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 2 — COSMOS Arnon (One-Time Boost)",
                        Level            = MissionLevel.L2,
                        StandingRequired = 1.0f,
                        StandingGoal     = 3.0f,
                        WhyText          = "Run the Arnon COSMOS chain BEFORE grinding L3 regular missions. Each COSMOS mission gives 3–8x normal standing and the full chain can push you from 1.0 to 3.0+ standing in one session. They cannot be repeated — this is a one-time opportunity.",
                        TipText          = "💡 Prepare all required COSMOS items in advance. Missing an item mid-chain costs significant travel time. Check EVE University wiki for the full item list.",
                        Agent            = "Multiple COSMOS agents in Arnon",
                        Station          = "Arnon system — see in-game agent list",
                        Region           = "Genesis",
                        Type             = MissionType.COSMOS,
                        EstGainPerRun    = 0.80f,
                        GainType         = StandingGainType.COSMOS,
                        IsCOSMOS         = true,
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 3 — L3 Security, Genesis",
                        Level            = MissionLevel.L3,
                        StandingRequired = 3.0f,
                        StandingGoal     = 5.0f,
                        WhyText          = "Post-COSMOS, continue with L3 SoE Security at Arnon or Gehi. A drone-focused ship (Ishtar, Vexor) excels here. 'Pirate Invasion' and 'Dread Pirate Scarlet' appear at L3 and give strong standing. Minimal faction penalties compared to empire factions.",
                        TipText          = "💡 Drone ships are meta for SoE — Ishtar or Vexor Navy Issue for L3 content. Blood Raiders appear occasionally; tank EM/Thermal for those encounters.",
                        Agent            = "Sister Elkin",
                        Station          = "Gehi VI - Moon 1 - Sisters of EVE Academy",
                        Region           = "Genesis",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.30f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Blood Raider Covenant", StandingLoss=-0.12f, Colour="#FFCE93D8",
                                    Note="Some L3 SoE missions involve Blood Raiders. Minor unless you need Blood Raider low-sec space." }
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 4 — L4 Sister Alitura (End-Game)",
                        Level            = MissionLevel.L4,
                        StandingRequired = 5.0f,
                        StandingGoal     = 8.0f,
                        WhyText          = "Sister Alitura's L4 missions are the gold standard for SoE grinding. Sisters Expanded Probe Launcher sells for 60–90M ISK each — at 300–600M ISK/hr in LP value alone. A drone battleship (Dominix, Megathron) is the meta. One of the best LP:ISK ratios in all of EVE.",
                        TipText          = "💡 Sisters Expanded Probe Launcher is consistently one of the best LP:ISK items in EVE. At 7.0+ standing you gain access to the highest LP tier.",
                        Agent            = "Sister Alitura",
                        Station          = "Arnon IX - Moon 3 - Sisters of EVE Bureau",
                        Region           = "Genesis",
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
                    },
                }
            },

            // ── CONCORD Assembly ──────────────────────────────────────────
            new FactionCatalogue
            {
                FactionName  = "CONCORD ASSEMBLY",
                Icon         = "⚖",
                Colour       = "#FF9575CD",
                EsiFactionId = 500010,
                IntroText    = "CONCORD standing reduces clone jump timer and unlocks the LP store with Marshal and Enforcer ship BPCs worth 3–8B ISK each. Agents are rare (mainly Yulai/Genesis). Run alongside SoE missions in Genesis for maximum efficiency. No faction standing penalties.",
                Steps = new()
                {
                    new TutorialStep
                    {
                        StepLabel        = "Step 1 — CONCORD Bureau, Yulai",
                        Level            = MissionLevel.L1,
                        StandingRequired = 0.0f,
                        StandingGoal     = 1.0f,
                        WhyText          = "CONCORD missions have no NPC enemy standing penalties — completely safe to run alongside any empire grind. L1/L2 agents are accessible to all pilots in Yulai. Main goal is unlocking the LP store tier at higher standings for Marshal BPCs.",
                        TipText          = "💡 CONCORD is in Genesis alongside SoE agents. Running both simultaneously saves enormous travel time.",
                        Agent            = "Ciraye Amare",
                        Station          = "Yulai XVI - CONCORD Bureau",
                        Region           = "Genesis",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.08f,
                        GainType         = StandingGainType.DirectFaction,
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 2 — L3 CONCORD Security",
                        Level            = MissionLevel.L3,
                        StandingRequired = 3.0f,
                        StandingGoal     = 5.0f,
                        WhyText          = "L3 CONCORD agents are the sweet spot. Agent density is low so plan runs in advance using the in-game agent finder. Chain CONCORD L3 missions with SoE L3 missions in neighbouring systems for maximum time efficiency.",
                        TipText          = "💡 Check agent finder before travelling — CONCORD agents are sparse. Combine with SoE L3 in Genesis to avoid dead travel time.",
                        Agent            = "Dirk McBride",
                        Station          = "Jolia III - CONCORD Bureau",
                        Region           = "Genesis",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.28f,
                        GainType         = StandingGainType.DirectFaction,
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 3 — L4 CONCORD Assembly (End-Game)",
                        Level            = MissionLevel.L4,
                        StandingRequired = 5.0f,
                        StandingGoal     = 8.0f,
                        WhyText          = "L4 CONCORD missions are rare end-game content. The LP store at 5.0+ includes Marshal and Enforcer CONCORD ship BPCs worth 3–8B ISK each. Low mission volume = slow standing gain, but exceptional LP value per mission. A multi-day grind session is required to accumulate enough LP.",
                        TipText          = "💡 Marshal BPC is the primary L4 CONCORD goal — worth 5–8B ISK. Plan and track your LP balance before each grind session.",
                        Agent            = "Auner Plaude",
                        Station          = "Yulai IX - Moon 4 - CONCORD Bureau",
                        Region           = "Genesis",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.55f,
                        GainType         = StandingGainType.DirectFaction,
                    },
                }
            },

            // ── ORE ───────────────────────────────────────────────────────
            new FactionCatalogue
            {
                FactionName  = "OUTER RING EXCAVATIONS",
                Icon         = "⛏",
                Colour       = "#FF8D6E63",
                EsiFactionId = 500024,
                IntroText    = "ORE standing unlocks the LP store (Orca/Rorqual modules, Mining Laser Upgrades), jump clone installation in Outer Ring null-sec, and station service discounts. The natural grind for dedicated industrialists. Mining missions are the primary method — ore rewards are an additional bonus.",
                Steps = new()
                {
                    new TutorialStep
                    {
                        StepLabel        = "Step 1 — ORE Factory, Solitude",
                        Level            = MissionLevel.L1,
                        StandingRequired = 0.0f,
                        StandingGoal     = 1.0f,
                        WhyText          = "ORE agents are in Solitude (high-sec) and Outer Ring (null-sec). Start in Solitude for safety. L1 distribution and mining missions build standing quickly. Note: Solitude is an isolated high-sec pocket — bring supplies before travelling.",
                        TipText          = "💡 A mining barge handles all L1 ORE mining missions. Bring a hauler for ore delivery — barge cargo may not be large enough.",
                        Agent            = "Reni Taavila",
                        Station          = "Wuos VIII - ORE Factory",
                        Region           = "Solitude",
                        Type             = MissionType.Distribution,
                        EstGainPerRun    = 0.07f,
                        GainType         = StandingGainType.DirectFaction,
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 2 — L3 Mining Contracts",
                        Level            = MissionLevel.L3,
                        StandingRequired = 3.0f,
                        StandingGoal     = 5.0f,
                        WhyText          = "L3 ORE mining missions require a Retriever minimum. The ore volumes requested are large — a Mackinaw or Skiff with a dedicated hauler alt is ideal. Mining missions give both ORE standing AND the mined ore as reward, making them very ISK-efficient for industrialists.",
                        TipText          = "💡 L3 mining missions often need 15,000–40,000 m³ of ore. Bring a dedicated Industrial ship (Tayra, Badger) for ore transport.",
                        Agent            = "Olairi Pellola",
                        Station          = "Agrallarier XI - Moon 4 - ORE Factory",
                        Region           = "Solitude",
                        Type             = MissionType.Mining,
                        EstGainPerRun    = 0.28f,
                        GainType         = StandingGainType.DirectFaction,
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 3 — L4 ORE (End-Game)",
                        Level            = MissionLevel.L4,
                        StandingRequired = 5.0f,
                        StandingGoal     = 8.0f,
                        WhyText          = "L4 ORE missions in Solitude offer the best industrialist LP:ISK in EVE. Ice Harvester Upgrade II and Mining Laser Upgrade II BPCs are top LP store items. Orca hull BPCs also appear at higher standing tiers. A dedicated exhumer fleet excels here.",
                        TipText          = "💡 Ice Harvester Upgrade II BPC and Mining Laser Upgrade II are consistently the best LP:ISK in the ORE store. Track market prices before buying LP store items.",
                        Agent            = "Airas Aulento",
                        Station          = "Clellinon VI - Moon 11 - ORE Factory",
                        Region           = "Solitude",
                        Type             = MissionType.Mining,
                        EstGainPerRun    = 0.55f,
                        GainType         = StandingGainType.DirectFaction,
                    },
                }
            },

            // ── EDENCOM ───────────────────────────────────────────────────
            new FactionCatalogue
            {
                FactionName  = "EDENCOM",
                Icon         = "⚡",
                Colour       = "#FF29B6F6",
                EsiFactionId = 500026,
                IntroText    = "EDENCOM standing allows travel through Triglavian-invaded Fortress systems without attack. CRITICAL: EDENCOM standing is directly opposed to Triglavian standing — every gain here is a loss there. Choosing EDENCOM permanently blocks Pochven access. Choose your side carefully.",
                Steps = new()
                {
                    new TutorialStep
                    {
                        StepLabel        = "Step 1 — Kill Triglavians in Invasion Zones",
                        Level            = MissionLevel.L1,
                        StandingRequired = 0.0f,
                        StandingGoal     = 1.0f,
                        WhyText          = "Kill Triglavian NPCs in Minor Conduit / Emerging Conduit sites in invaded systems. Each site gives ~+0.01 to +0.05 EDENCOM standing. The primary benefit is safe passage through former invasion systems and access to the EDENCOM LP store (niche ship BPCs).",
                        TipText          = "💡 ⚠️ PERMANENT CHOICE: Every Triglavian NPC killed reduces Triglavian standing. This blocks Pochven and all Triglavian content permanently. Decide before committing.",
                        Agent            = "N/A — Open world invasion site content",
                        Station          = "Find invasion systems via Triglavian Invasion map",
                        Region           = "Multiple (invasion zones)",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.03f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Triglavian Collective", StandingLoss=-0.05f, Colour="#FFFF6F00",
                                    Note="⚠️ PERMANENT: Every EDENCOM gain = Triglavian loss. Blocks Pochven access forever." }
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 2 — Fortress System Content",
                        Level            = MissionLevel.L3,
                        StandingRequired = 3.0f,
                        StandingGoal     = 8.0f,
                        WhyText          = "Fortress systems (where EDENCOM won the invasion) have higher-value EDENCOM content and give better standing gains. EDENCOM LP store at 3.0+ offers useful niche modules. Run alongside CONCORD missions in Genesis for maximum efficiency.",
                        TipText          = "💡 Fortress systems still have Triglavian spawns — bring adequate tank. At 5.0+ standing you are a recognized EDENCOM Champion and gain top LP store access.",
                        Agent            = "Akatta Teppler",
                        Station          = "Yulai IX - CONCORD Bureau (EDENCOM Division)",
                        Region           = "Genesis",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.25f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="Triglavian Collective", StandingLoss=-0.18f, Colour="#FFFF6F00",
                                    Note="Continued heavy Triglavian standing loss." }
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
                EsiFactionId = 500027,
                IntroText    = "Triglavian standing unlocks Pochven (18 unique systems with exclusive resources), Damavik/Kikimora hull BPCs, and Entropic Disintegrator weapons. CRITICAL: Directly opposed to EDENCOM — choosing Triglavian blocks fortress system travel and all EDENCOM content permanently.",
                Steps = new()
                {
                    new TutorialStep
                    {
                        StepLabel        = "Step 1 — Abyssal Deadspace T1/T2",
                        Level            = MissionLevel.L1,
                        StandingRequired = 0.0f,
                        StandingGoal     = 1.0f,
                        WhyText          = "Start with T1 (Calm) Abyssal Deadspace filaments — soloable in a T1 cruiser, no existing Triglavian standing required. Each run gives ~+0.01 to +0.03 Triglavian standing. Safe entry point before committing to higher-risk content.",
                        TipText          = "💡 ⚠️ PERMANENT CHOICE: Running Abyssal content reduces EDENCOM standing. Once started, reversing this path takes months. Commit to Triglavian or don't start.",
                        Agent            = "N/A — Filament content",
                        Station          = "Purchase T1 Calm filaments on market in any major hub",
                        Region           = "Abyssal Deadspace",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.02f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="EDENCOM", StandingLoss=-0.03f, Colour="#FF29B6F6",
                                    Note="⚠️ PERMANENT: Every Triglavian gain = EDENCOM loss. You will be attacked in fortress systems." }
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 2 — Emerging Conduit Sites (1.0+)",
                        Level            = MissionLevel.L2,
                        StandingRequired = 1.0f,
                        StandingGoal     = 3.0f,
                        WhyText          = "At 1.0 Triglavian standing, Emerging Conduit invasion sites become available and give significantly more standing per run than Abyssal content. Fly alongside other Triglavian-sided pilots for faster clears. Upgrade to T3/T4 (Fierce/Raging) filaments for much faster standing gains.",
                        TipText          = "💡 T3 filaments give ~4x the standing of T1 but require a well-fitted cruiser. Coordinate with other Trig-side pilots for Conduit content — faster clear = more standing.",
                        Agent            = "N/A — Open world site content",
                        Station          = "Emerging Conduit sites in active invasion systems",
                        Region           = "Multiple (invasion systems)",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.05f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="EDENCOM", StandingLoss=-0.08f, Colour="#FF29B6F6",
                                    Note="Accelerating EDENCOM standing loss." }
                        },
                    },
                    new TutorialStep
                    {
                        StepLabel        = "Step 3 — Pochven Access: Semiosis Proving (5.0+)",
                        Level            = MissionLevel.L4,
                        StandingRequired = 5.0f,
                        StandingGoal     = 8.0f,
                        WhyText          = "At 5.0 Triglavian standing the Collective fully accepts you — free travel through Pochven, docking in Triglavian structures. Semiosis Proving sites in Pochven are the best end-game content: excellent ISK, Bioadaptive Caches, Entropic Disintegrators, and fastest Triglavian standing gains available.",
                        TipText          = "💡 Kikimora and Damavik BPCs from LP store: 800M–2B ISK each. At 7.0+ standing you are 'Triglavian Provisional Citizen' — highest trust tier with access to all Pochven content.",
                        Agent            = "N/A — Pochven open world sites",
                        Station          = "Pochven conduit systems (enter via filament at 1.0+)",
                        Region           = "Pochven",
                        Type             = MissionType.Security,
                        EstGainPerRun    = 0.40f,
                        GainType         = StandingGainType.DirectFaction,
                        EnemyWarnings    = new()
                        {
                            new() { FactionName="EDENCOM", StandingLoss=-0.30f, Colour="#FF29B6F6",
                                    Note="Severe EDENCOM loss in Pochven. All fortress system EDENCOM ships will attack you on sight." },
                            new() { FactionName="Amarr Empire", StandingLoss=-0.05f,
                                    Note="Some Pochven content involves Amarr-aligned targets." }
                        },
                    },
                }
            },
        };
    }
}
