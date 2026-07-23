// filename: Models/FactionSkillPlan.cs
// Skill plans for every major mission-running faction in EVE Online.
// Skill IDs verified against Pyfa eve.db (invtypes table).
//
// AlphaCap: -1 = Omega only (Alpha cannot train this skill at all).
//            0–5 = highest level an Alpha clone can reach.
// Default AlphaCap is 4 — most combat skills are capped there for Alpha.
// Skills with AlphaCap 5 can be maxed even on Alpha (basic/universal skills).

using System;
using System.Collections.Generic;

namespace OverlayMVP.Models
{
    public sealed class SkillRequirement
    {
        public int    SkillId          { get; init; }
        public string SkillName        { get; init; } = "";
        public string Category         { get; init; } = "";
        public int    RecommendedLevel { get; init; } = 4;
        public string WhyText          { get; init; } = "";

        /// -1 = Omega only.  0–5 = highest level an Alpha clone can train.
        public int    AlphaCap         { get; init; } = 4;

        // Set from ViewModel at runtime — NOT init so ApplySkillPlan can update them
        public int    TrainedLevel     { get; set; } = -1;
        public bool   IsOmega          { get; set; } = true;

        public bool   IsOmegaOnly      => AlphaCap < 0;

        /// Level this player should aim for, accounting for Alpha caps.
        public int    EffectiveTarget  => IsOmega
            ? RecommendedLevel
            : IsOmegaOnly ? 0 : Math.Min(RecommendedLevel, AlphaCap);

        public bool   IsMaxed     => EffectiveTarget > 0 && TrainedLevel >= EffectiveTarget;
        public bool   IsUnknown   => TrainedLevel < 0;

        public string StatusEmoji => IsOmegaOnly && !IsOmega
            ? "🔒"
            : IsUnknown ? "?" : IsMaxed ? "✅" : TrainedLevel == 0 ? "🔴" : "🟡";

        public string LevelText   => IsOmegaOnly && !IsOmega
            ? "Ω only"
            : IsUnknown ? "?" : $"{TrainedLevel}/{EffectiveTarget}";
    }

    public sealed class FactionSkillPlan
    {
        public string                    FactionKey   { get; init; } = "";
        public string                    FactionName  { get; init; } = "";
        public string                    Ships        { get; init; } = "";
        public string                    Notes        { get; init; } = "";
        public List<SkillRequirement>    Skills       { get; init; } = new();
    }

    public static class FactionSkillPlans
    {
        // ── Verified skill IDs (eve.db invtypes) ─────────────────────────────
        // Turrets:
        //   3301=Small Hybrid Turret  3302=Small Projectile Turret  3303=Small Energy Turret
        //   3304=Medium Hybrid Turret 3305=Medium Projectile Turret 3306=Medium Energy Turret
        //   3307=Large Hybrid Turret  3308=Large Projectile Turret  3309=Large Energy Turret
        // Gunnery support:
        //   3310=Rapid Firing  3311=Sharpshooter  3312=Motion Prediction
        //   3315=Surgical Strike  3316=Controlled Bursts  3317=Trajectory Analysis
        //   3318=Weapon Upgrades  3421=Energy Pulse Weapons
        // Missiles:
        //   3319=Missile Launcher Operation  3320=Rockets  3321=Light Missiles
        //   3324=Heavy Missiles  3325=Torpedoes  3326=Cruise Missiles
        //   12441=Missile Bombardment  20312=Guided Missile Precision
        //   20314=Target Navigation Prediction  20315=Warhead Upgrades
        //   21071=Rapid Launch  25719=Heavy Assault Missiles
        // Shield:
        //   3416=Shield Operation  3419=Shield Management  3420=Tactical Shield Manipulation
        //   3425=Shield Upgrades  3422=Shield Emission Systems
        // Capacitor:
        //   3417=Capacitor Systems Operation  3418=Capacitor Management
        //   3424=Energy Grid Upgrades  3432=Electronics Upgrades
        // Core / Engineering:
        //   3413=Power Grid Management  3426=CPU Management  3427=Electronic Warfare
        //   3428=Long Range Targeting  3431=Signature Analysis  3432=Electronics Upgrades
        // Navigation:
        //   3449=Navigation  3450=Afterburner  3453=Evasive Maneuvering
        //   3454=High Speed Maneuvering  3455=Warp Drive Operation
        // Drones:
        //   3436=Drones  3437=Drone Avionics  3441=Heavy Drone Operation
        //   3442=Drone Interfacing  12305=Drone Navigation  23606=Drone Sharpshooting
        //   23618=Drone Durability  24241=Light Drone Operation  33699=Medium Drone Operation
        // Armor:
        //   3392=Mechanics  3393=Repair Systems  3394=Hull Upgrades
        // Exploration:
        //   3412=Astrometrics  12093=Covert Ops  25811=Astrometric Acquisition
        // Ships:
        //   3327=Spaceship Command  3328=Gallente Frigate  3329=Minmatar Frigate
        //   3330=Caldari Frigate  3334=Caldari Cruiser  3336=Gallente Battleship
        //   3337=Minmatar Battleship  3338=Caldari Battleship  3339=Amarr Battleship
        //   16591=Heavy Assault Cruisers  17940=Mining Barge  22043=Tactical Weapon Reconfiguration
        //   22551=Exhumers  22578=Mining Upgrades  23950=Command Ships  28667=Marauders
        //   33092=Caldari Destroyer  33093=Gallente Destroyer  33091=Amarr Destroyer  33094=Minmatar Destroyer
        //   33095=Amarr Battlecruiser  33096=Caldari Battlecruiser  33097=Gallente Battlecruiser  33098=Minmatar Battlecruiser
        //   3332=Gallente Cruiser  3333=Minmatar Cruiser  3334=Caldari Cruiser  3335=Amarr Cruiser
        //   3336=Gallente Battleship  3337=Minmatar Battleship  3338=Caldari Battleship  3339=Amarr Battleship
        //   47867=Precursor Frigate  47868=Precursor Cruiser  47869=Precursor Battleship
        //   47870=Small Precursor Weapon  47871=Medium Precursor Weapon  47872=Large Precursor Weapon
        // Fitting:
        //   3318=Weapon Upgrades  11207=Advanced Weapon Upgrades
        // Leadership:
        //   3348=Leadership  3402=Science  3354=Command Burst Specialist  11574=Wing Command
        //   3350=Shield Command
        // Mining:
        //   3386=Mining  3410=Astrogeology  22578=Mining Upgrades

        public static readonly List<FactionSkillPlan> All = new()
        {
            // ── Caldari State ────────────────────────────────────────────────
            new FactionSkillPlan
            {
                FactionKey  = "CALDARI",
                FactionName = "Caldari State",
                Ships       = "Caracal, Drake, Ferox, Cerberus, Nighthawk, Raven, Golem",
                Notes       = "Missiles + shields. Great passive tank. L4s pay well.",
                Skills      = new()
                {
                    // Ships
                    new() { SkillId=3327,  SkillName="Spaceship Command",           Category="Ships",      RecommendedLevel=5, AlphaCap=4, WhyText="Universal ship handling — needed at V for BC/BS" },
                    new() { SkillId=3334,  SkillName="Caldari Cruiser",             Category="Ships",      RecommendedLevel=5, AlphaCap=4, WhyText="Caracal L1, Cerberus L5 (HAC prereq)" },
                    new() { SkillId=33096, SkillName="Caldari Battlecruiser",       Category="Ships",      RecommendedLevel=5, AlphaCap=4, WhyText="Drake L1, Nighthawk L5 (Command Ship prereq)" },
                    new() { SkillId=3338,  SkillName="Caldari Battleship",          Category="Ships",      RecommendedLevel=5, AlphaCap=4, WhyText="Raven L1, Golem L5 (Marauder prereq)" },
                    new() { SkillId=16591, SkillName="Heavy Assault Cruisers",      Category="Ships",      RecommendedLevel=1, AlphaCap=-1, WhyText="Fly the Cerberus — Omega only" },
                    new() { SkillId=23950, SkillName="Command Ships",               Category="Ships",      RecommendedLevel=1, AlphaCap=-1, WhyText="Fly the Nighthawk — Omega only" },
                    new() { SkillId=28667, SkillName="Marauders",                   Category="Ships",      RecommendedLevel=4, AlphaCap=-1, WhyText="Golem bonus scales with level — Omega only" },
                    // Missiles
                    new() { SkillId=3319,  SkillName="Missile Launcher Operation",  Category="Missiles",   RecommendedLevel=5, AlphaCap=4, WhyText="Base missile skill — mandatory" },
                    new() { SkillId=3324,  SkillName="Heavy Missiles",              Category="Missiles",   RecommendedLevel=5, AlphaCap=4, WhyText="Main damage for Drake/Cerberus — V for T2 ammo" },
                    new() { SkillId=3326,  SkillName="Cruise Missiles",             Category="Missiles",   RecommendedLevel=5, AlphaCap=4, WhyText="L4 main weapon for Raven/Golem — V for T2 ammo" },
                    new() { SkillId=12441, SkillName="Missile Bombardment",         Category="Missiles",   RecommendedLevel=4, AlphaCap=4, WhyText="+10% range per level; needed for Missile Guidance Computer" },
                    new() { SkillId=21071, SkillName="Rapid Launch",                Category="Missiles",   RecommendedLevel=4, AlphaCap=4, WhyText="+2% ROF per level" },
                    new() { SkillId=20312, SkillName="Guided Missile Precision",    Category="Missiles",   RecommendedLevel=4, AlphaCap=4, WhyText="Reduces signature radius penalty" },
                    new() { SkillId=20314, SkillName="Target Navigation Prediction",Category="Missiles",   RecommendedLevel=4, AlphaCap=4, WhyText="Reduces velocity penalty on missiles" },
                    new() { SkillId=20315, SkillName="Warhead Upgrades",            Category="Missiles",   RecommendedLevel=4, AlphaCap=4, WhyText="+2% explosion velocity per level" },
                    // Gunnery (Ferox / railgun builds)
                    new() { SkillId=3304,  SkillName="Medium Hybrid Turret",        Category="Gunnery",    RecommendedLevel=5, AlphaCap=4, WhyText="Ferox 250mm Railgun II — V for T2 ammo" },
                    new() { SkillId=3311,  SkillName="Sharpshooter",                Category="Gunnery",    RecommendedLevel=4, AlphaCap=4, WhyText="+5% optimal range per level" },
                    // Shield
                    new() { SkillId=3419,  SkillName="Shield Management",           Category="Shield",     RecommendedLevel=5, AlphaCap=5, WhyText="+5% shield capacity per level — V for buffer fits" },
                    new() { SkillId=3416,  SkillName="Shield Operation",            Category="Shield",     RecommendedLevel=5, AlphaCap=5, WhyText="Passive regen bonus — V for shield-heavy setups" },
                    new() { SkillId=3425,  SkillName="Shield Upgrades",             Category="Shield",     RecommendedLevel=4, AlphaCap=4, WhyText="Reduces shield upgrader CPU; fit LSE II" },
                    new() { SkillId=3420,  SkillName="Tactical Shield Manipulation",Category="Shield",     RecommendedLevel=4, AlphaCap=4, WhyText="Reduces chance of damage bleeding through" },
                    // Core / Engineering
                    new() { SkillId=3318,  SkillName="Weapon Upgrades",             Category="Core",       RecommendedLevel=5, AlphaCap=5, WhyText="Reduces CPU use of weapons — V for T2 modules" },
                    new() { SkillId=11207, SkillName="Advanced Weapon Upgrades",    Category="Core",       RecommendedLevel=5, AlphaCap=3, WhyText="Reduces PG use of T2 weapons — mandatory for T2 fits" },
                    new() { SkillId=3426,  SkillName="CPU Management",              Category="Core",       RecommendedLevel=5, AlphaCap=5, WhyText="Fit everything you need" },
                    new() { SkillId=3413,  SkillName="Power Grid Management",       Category="Core",       RecommendedLevel=5, AlphaCap=5, WhyText="Fit large weapons + T2 modules" },
                    new() { SkillId=3428,  SkillName="Long Range Targeting",        Category="Core",       RecommendedLevel=4, AlphaCap=4, WhyText="Lock targets further away" },
                    new() { SkillId=19921, SkillName="Target Painting",             Category="Core",       RecommendedLevel=4, AlphaCap=3, WhyText="Makes cruisers hit harder vs battleships" },
                    new() { SkillId=3450,  SkillName="Afterburner",                 Category="Core",       RecommendedLevel=4, AlphaCap=4, WhyText="Speed and cap efficiency in missions" },
                    // Leadership (Nighthawk Command Ship prerequisites)
                    new() { SkillId=3402,  SkillName="Science",                     Category="Leadership", RecommendedLevel=5, AlphaCap=4, WhyText="Prereq for Command Ships" },
                    new() { SkillId=3348,  SkillName="Leadership",                  Category="Leadership", RecommendedLevel=5, AlphaCap=3, WhyText="Prereq for Command Ships" },
                }
            },

            // ── Gallente Federation ──────────────────────────────────────────
            new FactionSkillPlan
            {
                FactionKey  = "GALLENTE",
                FactionName = "Gallente Federation",
                Ships       = "Vexor, Myrmidon, Dominix, Ishtar",
                Notes       = "Drones + armor. Versatile. Drones handle all sizes.",
                Skills      = new()
                {
                    new() { SkillId=3327,  SkillName="Spaceship Command",           Category="Ships",      RecommendedLevel=5, AlphaCap=4, WhyText="Universal ship handling — V for BC/BS" },
                    new() { SkillId=3332,  SkillName="Gallente Cruiser",            Category="Ships",      RecommendedLevel=5, AlphaCap=4, WhyText="Vexor L1, Ishtar L5 (HAC prereq)" },
                    new() { SkillId=33097, SkillName="Gallente Battlecruiser",      Category="Ships",      RecommendedLevel=5, AlphaCap=4, WhyText="Myrmidon L1, Eos/Astarte L5" },
                    new() { SkillId=3336,  SkillName="Gallente Battleship",         Category="Ships",      RecommendedLevel=5, AlphaCap=4, WhyText="Dominix L1 — top L4 drone ship" },
                    new() { SkillId=3436,  SkillName="Drones",                      Category="Drones",     RecommendedLevel=5, AlphaCap=5, WhyText="Controls drone count — max at 5" },
                    new() { SkillId=3441,  SkillName="Heavy Drone Operation",       Category="Drones",     RecommendedLevel=4, AlphaCap=4, WhyText="Main DPS drones for Dominix/Ishtar" },
                    new() { SkillId=23618, SkillName="Drone Durability",            Category="Drones",     RecommendedLevel=4, AlphaCap=4, WhyText="+5% drone HP per level" },
                    new() { SkillId=3442,  SkillName="Drone Interfacing",           Category="Drones",     RecommendedLevel=4, AlphaCap=4, WhyText="+20% drone DPS per level — most impactful" },
                    new() { SkillId=12305, SkillName="Drone Navigation",            Category="Drones",     RecommendedLevel=4, AlphaCap=4, WhyText="+5% drone speed per level" },
                    new() { SkillId=23606, SkillName="Drone Sharpshooting",         Category="Drones",     RecommendedLevel=4, AlphaCap=4, WhyText="+5% drone optimal range per level" },
                    new() { SkillId=24241, SkillName="Light Drone Operation",       Category="Drones",     RecommendedLevel=5, AlphaCap=4, WhyText="Required for T2 light drones" },
                    new() { SkillId=3394,  SkillName="Hull Upgrades",               Category="Armor",      RecommendedLevel=4, AlphaCap=4, WhyText="Required for armor plates" },
                    new() { SkillId=3392,  SkillName="Mechanics",                   Category="Armor",      RecommendedLevel=5, AlphaCap=5, WhyText="Base repair skill" },
                    new() { SkillId=3393,  SkillName="Repair Systems",              Category="Armor",      RecommendedLevel=4, AlphaCap=4, WhyText="Reduces armor rep cycle time" },
                    new() { SkillId=3318,  SkillName="Weapon Upgrades",             Category="Core",       RecommendedLevel=5, AlphaCap=5, WhyText="Reduces weapon CPU use — V for T2 modules" },
                    new() { SkillId=11207, SkillName="Advanced Weapon Upgrades",    Category="Core",       RecommendedLevel=5, AlphaCap=3, WhyText="Reduces T2 weapon PG use — mandatory for T2 fits" },
                    new() { SkillId=3426,  SkillName="CPU Management",              Category="Core",       RecommendedLevel=5, AlphaCap=5, WhyText="Drones need fitting room" },
                    new() { SkillId=3413,  SkillName="Power Grid Management",       Category="Core",       RecommendedLevel=5, AlphaCap=5, WhyText="Armor plates are power hungry" },
                }
            },

            // ── Amarr Empire ─────────────────────────────────────────────────
            new FactionSkillPlan
            {
                FactionKey  = "AMARR",
                FactionName = "Amarr Empire",
                Ships       = "Omen, Prophecy, Apocalypse, Paladin",
                Notes       = "Lasers + armor. Best L4 income with Paladin/Nightmare.",
                Skills      = new()
                {
                    new() { SkillId=3327,  SkillName="Spaceship Command",           Category="Ships",      RecommendedLevel=5, AlphaCap=4, WhyText="Universal ship handling — V for BC/BS" },
                    new() { SkillId=3335,  SkillName="Amarr Cruiser",               Category="Ships",      RecommendedLevel=5, AlphaCap=4, WhyText="Omen L1, Sacrilege/Zealot L5" },
                    new() { SkillId=33095, SkillName="Amarr Battlecruiser",         Category="Ships",      RecommendedLevel=5, AlphaCap=4, WhyText="Prophecy L1, Absolution/Astarte L5" },
                    new() { SkillId=3339,  SkillName="Amarr Battleship",            Category="Ships",      RecommendedLevel=5, AlphaCap=4, WhyText="Apocalypse L1, Paladin L5 (Marauder prereq)" },
                    new() { SkillId=3303,  SkillName="Small Energy Turret",         Category="Lasers",     RecommendedLevel=4, AlphaCap=4, WhyText="Frigate lasers baseline" },
                    new() { SkillId=3306,  SkillName="Medium Energy Turret",        Category="Lasers",     RecommendedLevel=5, AlphaCap=4, WhyText="Cruiser lasers — Omen/Sacrilege" },
                    new() { SkillId=3309,  SkillName="Large Energy Turret",         Category="Lasers",     RecommendedLevel=5, AlphaCap=4, WhyText="BS lasers — Apoc/Paladin" },
                    new() { SkillId=3316,  SkillName="Controlled Bursts",           Category="Lasers",     RecommendedLevel=4, AlphaCap=4, WhyText="Reduces cap use of energy weapons" },
                    new() { SkillId=3421,  SkillName="Energy Pulse Weapons",        Category="Lasers",     RecommendedLevel=4, AlphaCap=4, WhyText="+3% RoF per level" },
                    new() { SkillId=3311,  SkillName="Sharpshooter",                Category="Lasers",     RecommendedLevel=4, AlphaCap=4, WhyText="+5% optimal range per level" },
                    new() { SkillId=3418,  SkillName="Capacitor Management",        Category="Capacitor",  RecommendedLevel=5, AlphaCap=4, WhyText="Lasers eat cap — essential" },
                    new() { SkillId=3417,  SkillName="Capacitor Systems Operation", Category="Capacitor",  RecommendedLevel=4, AlphaCap=4, WhyText="Faster cap recharge" },
                    new() { SkillId=3424,  SkillName="Energy Grid Upgrades",        Category="Capacitor",  RecommendedLevel=4, AlphaCap=4, WhyText="Fit cap boosters/batteries" },
                    new() { SkillId=3394,  SkillName="Hull Upgrades",               Category="Armor",      RecommendedLevel=4, AlphaCap=4, WhyText="Plates and hardeners" },
                    new() { SkillId=3392,  SkillName="Mechanics",                   Category="Armor",      RecommendedLevel=4, AlphaCap=5, WhyText="Base armor HP" },
                    new() { SkillId=3318,  SkillName="Weapon Upgrades",             Category="Core",       RecommendedLevel=5, AlphaCap=5, WhyText="Reduces weapon CPU use — V for T2 modules" },
                    new() { SkillId=11207, SkillName="Advanced Weapon Upgrades",    Category="Core",       RecommendedLevel=5, AlphaCap=3, WhyText="Reduces T2 weapon PG use — mandatory for T2 fits" },
                    new() { SkillId=3426,  SkillName="CPU Management",              Category="Core",       RecommendedLevel=5, AlphaCap=5, WhyText="Cap modules need CPU" },
                    new() { SkillId=3413,  SkillName="Power Grid Management",       Category="Core",       RecommendedLevel=5, AlphaCap=5, WhyText="Large lasers are power hungry" },
                }
            },

            // ── Minmatar Republic ────────────────────────────────────────────
            new FactionSkillPlan
            {
                FactionKey  = "MINMATAR",
                FactionName = "Minmatar Republic",
                Ships       = "Rupture, Hurricane, Typhoon, Machariel",
                Notes       = "Projectiles + shields/armor. Speed-tank viable.",
                Skills      = new()
                {
                    new() { SkillId=3327,  SkillName="Spaceship Command",           Category="Ships",      RecommendedLevel=5, AlphaCap=4, WhyText="Universal ship handling — V for BC/BS" },
                    new() { SkillId=3333,  SkillName="Minmatar Cruiser",            Category="Ships",      RecommendedLevel=5, AlphaCap=4, WhyText="Rupture L1, Vagabond/Muninn L5" },
                    new() { SkillId=33098, SkillName="Minmatar Battlecruiser",      Category="Ships",      RecommendedLevel=5, AlphaCap=4, WhyText="Hurricane L1, Sleipnir/Claymore L5" },
                    new() { SkillId=3337,  SkillName="Minmatar Battleship",         Category="Ships",      RecommendedLevel=5, AlphaCap=4, WhyText="Typhoon L1, Vargur L5 (Marauder prereq)" },
                    new() { SkillId=3302,  SkillName="Small Projectile Turret",     Category="Projectiles",RecommendedLevel=4, AlphaCap=4, WhyText="Frigate ACs/Arties" },
                    new() { SkillId=3305,  SkillName="Medium Projectile Turret",    Category="Projectiles",RecommendedLevel=5, AlphaCap=4, WhyText="Cruiser guns" },
                    new() { SkillId=3308,  SkillName="Large Projectile Turret",     Category="Projectiles",RecommendedLevel=5, AlphaCap=4, WhyText="BS guns — Typhoon/Mach" },
                    new() { SkillId=3317,  SkillName="Trajectory Analysis",         Category="Projectiles",RecommendedLevel=4, AlphaCap=4, WhyText="+5% optimal range per level" },
                    new() { SkillId=3312,  SkillName="Motion Prediction",           Category="Projectiles",RecommendedLevel=4, AlphaCap=4, WhyText="Reduces tracking penalty" },
                    new() { SkillId=3315,  SkillName="Surgical Strike",             Category="Projectiles",RecommendedLevel=4, AlphaCap=4, WhyText="+3% damage per level" },
                    new() { SkillId=3310,  SkillName="Rapid Firing",                Category="Projectiles",RecommendedLevel=4, AlphaCap=4, WhyText="+3% RoF per level" },
                    new() { SkillId=3449,  SkillName="Navigation",                  Category="Navigation", RecommendedLevel=5, AlphaCap=5, WhyText="+5% sub-warp speed" },
                    new() { SkillId=3450,  SkillName="Afterburner",                 Category="Navigation", RecommendedLevel=4, AlphaCap=4, WhyText="AB speed and cap use" },
                    new() { SkillId=3454,  SkillName="High Speed Maneuvering",      Category="Navigation", RecommendedLevel=4, AlphaCap=4, WhyText="MWD signature penalty reduction" },
                    new() { SkillId=3419,  SkillName="Shield Management",           Category="Shield",     RecommendedLevel=4, AlphaCap=5, WhyText="Buffer or active shield" },
                    new() { SkillId=3416,  SkillName="Shield Operation",            Category="Shield",     RecommendedLevel=4, AlphaCap=5, WhyText="Passive regen" },
                    new() { SkillId=3318,  SkillName="Weapon Upgrades",             Category="Core",       RecommendedLevel=5, AlphaCap=5, WhyText="Reduces weapon CPU use — V for T2 modules" },
                    new() { SkillId=11207, SkillName="Advanced Weapon Upgrades",    Category="Core",       RecommendedLevel=5, AlphaCap=3, WhyText="Reduces T2 weapon PG use — mandatory for T2 fits" },
                    new() { SkillId=3426,  SkillName="CPU Management",              Category="Core",       RecommendedLevel=5, AlphaCap=5, WhyText="Fit everything" },
                    new() { SkillId=3413,  SkillName="Power Grid Management",       Category="Core",       RecommendedLevel=5, AlphaCap=5, WhyText="Large ACs need PG" },
                }
            },

            // ── Sisters of EVE ───────────────────────────────────────────────
            new FactionSkillPlan
            {
                FactionKey  = "SOE",
                FactionName = "Sisters of EVE",
                Ships       = "Stratios, Astero, Nestor",
                Notes       = "Covert ops + drones. Exploration-focused missions.",
                Skills      = new()
                {
                    new() { SkillId=3436,  SkillName="Drones",                      Category="Drones",      RecommendedLevel=5, AlphaCap=5,  WhyText="Core Stratios damage" },
                    new() { SkillId=3442,  SkillName="Drone Interfacing",           Category="Drones",      RecommendedLevel=4, AlphaCap=4,  WhyText="+20% DPS per level" },
                    new() { SkillId=3441,  SkillName="Heavy Drone Operation",       Category="Drones",      RecommendedLevel=4, AlphaCap=4,  WhyText="Heavy drones for bigger rats" },
                    new() { SkillId=12093, SkillName="Covert Ops",                  Category="Covert",      RecommendedLevel=4, AlphaCap=-1, WhyText="Required for covert cloaking devices — Omega only" },
                    new() { SkillId=3394,  SkillName="Hull Upgrades",               Category="Armor",       RecommendedLevel=4, AlphaCap=4,  WhyText="Armor tank on Stratios" },
                    new() { SkillId=3412,  SkillName="Astrometrics",                Category="Exploration", RecommendedLevel=4, AlphaCap=4,  WhyText="Scan strength for exploration" },
                    new() { SkillId=25811, SkillName="Astrometric Acquisition",     Category="Exploration", RecommendedLevel=4, AlphaCap=4,  WhyText="Reduces scan time" },
                    new() { SkillId=3428,  SkillName="Long Range Targeting",        Category="Core",        RecommendedLevel=4, AlphaCap=4,  WhyText="Lock cloaked targets further" },
                    new() { SkillId=3426,  SkillName="CPU Management",              Category="Core",        RecommendedLevel=5, AlphaCap=5,  WhyText="Cloaks and drones need CPU" },
                }
            },

            // ── CONCORD Assembly ─────────────────────────────────────────────
            new FactionSkillPlan
            {
                FactionKey  = "CONCORD",
                FactionName = "CONCORD Assembly",
                Ships       = "Enforcer, Marshal (CONCORD LP ships)",
                Notes       = "Only CONCORD event missions. Focus on combat core skills.",
                Skills      = new()
                {
                    new() { SkillId=3426, SkillName="CPU Management",              Category="Core",   RecommendedLevel=5, AlphaCap=5, WhyText="Universal fitting skill" },
                    new() { SkillId=3413, SkillName="Power Grid Management",       Category="Core",   RecommendedLevel=5, AlphaCap=5, WhyText="Universal fitting skill" },
                    new() { SkillId=3419, SkillName="Shield Management",           Category="Shield", RecommendedLevel=5, AlphaCap=5, WhyText="CONCORD ships are shield-tanked" },
                    new() { SkillId=3416, SkillName="Shield Operation",            Category="Shield", RecommendedLevel=5, AlphaCap=5, WhyText="Passive regen important" },
                    new() { SkillId=3436, SkillName="Drones",                      Category="Drones", RecommendedLevel=5, AlphaCap=5, WhyText="Drone-focused CONCORD hulls" },
                    new() { SkillId=3442, SkillName="Drone Interfacing",           Category="Drones", RecommendedLevel=5, AlphaCap=4, WhyText="Max DPS from drones" },
                }
            },

            // ── ORE ──────────────────────────────────────────────────────────
            new FactionSkillPlan
            {
                FactionKey  = "ORE",
                FactionName = "ORE",
                Ships       = "Retriever, Covetor, Hulk, Orca",
                Notes       = "Mining operations. Standings improve LP store access.",
                Skills      = new()
                {
                    new() { SkillId=3386,  SkillName="Mining",                      Category="Mining",  RecommendedLevel=5, AlphaCap=5,  WhyText="Base mining yield" },
                    new() { SkillId=3410,  SkillName="Astrogeology",                Category="Mining",  RecommendedLevel=5, AlphaCap=4,  WhyText="+5% mining yield per level" },
                    new() { SkillId=22578, SkillName="Mining Upgrades",             Category="Mining",  RecommendedLevel=4, AlphaCap=4,  WhyText="Fit mining upgrade modules" },
                    new() { SkillId=17940, SkillName="Mining Barge",                Category="Ships",   RecommendedLevel=5, AlphaCap=3,  WhyText="Alpha: Retriever (III). Omega: Hulk (V)" },
                    new() { SkillId=22551, SkillName="Exhumers",                    Category="Ships",   RecommendedLevel=4, AlphaCap=-1, WhyText="T2 mining barges — Omega only" },
                    new() { SkillId=3419,  SkillName="Shield Management",           Category="Shield",  RecommendedLevel=4, AlphaCap=5,  WhyText="Barges are shield-tanked" },
                    new() { SkillId=3416,  SkillName="Shield Operation",            Category="Shield",  RecommendedLevel=4, AlphaCap=5,  WhyText="Passive regen while mining" },
                    new() { SkillId=3426,  SkillName="CPU Management",              Category="Core",    RecommendedLevel=4, AlphaCap=5,  WhyText="Fit all the strips" },
                }
            },

            // ── EDENCOM ──────────────────────────────────────────────────────
            new FactionSkillPlan
            {
                FactionKey  = "EDENCOM",
                FactionName = "EDENCOM",
                Ships       = "Skybreaker, Stormbringer, Thunderchild",
                Notes       = "Triglavian invasion defense. Vojnfield-type weapons arc between targets.",
                Skills      = new()
                {
                    new() { SkillId=56306, SkillName="Fullerene Lancer Specialization", Category="Weapons", RecommendedLevel=4, AlphaCap=-1, WhyText="EDENCOM weapon spec — Omega only" },
                    new() { SkillId=3419,  SkillName="Shield Management",          Category="Shield",  RecommendedLevel=5, AlphaCap=5,  WhyText="EDENCOM ships are shield-tanked" },
                    new() { SkillId=3416,  SkillName="Shield Operation",           Category="Shield",  RecommendedLevel=5, AlphaCap=5,  WhyText="Active shield tank" },
                    new() { SkillId=3426,  SkillName="CPU Management",             Category="Core",    RecommendedLevel=5, AlphaCap=5,  WhyText="EDENCOM modules are CPU-heavy" },
                    new() { SkillId=3413,  SkillName="Power Grid Management",      Category="Core",    RecommendedLevel=5, AlphaCap=5,  WhyText="Fit full rack" },
                    new() { SkillId=3428,  SkillName="Long Range Targeting",       Category="Core",    RecommendedLevel=4, AlphaCap=4,  WhyText="Long range weapons need lock range" },
                }
            },

            // ── Triglavian Collective ────────────────────────────────────────
            new FactionSkillPlan
            {
                FactionKey  = "TRIGLAVIAN",
                FactionName = "Triglavian Collective",
                Ships       = "Damavik, Vedmak, Leshak, Nergal",
                Notes       = "Precursor weapons (damage ramps up). Unique playstyle.",
                Skills      = new()
                {
                    new() { SkillId=47870, SkillName="Small Precursor Weapon",     Category="Weapons",  RecommendedLevel=5, AlphaCap=-1, WhyText="Damavik/Nergal weapons — Omega only" },
                    new() { SkillId=47871, SkillName="Medium Precursor Weapon",    Category="Weapons",  RecommendedLevel=5, AlphaCap=-1, WhyText="Vedmak weapons — Omega only" },
                    new() { SkillId=47872, SkillName="Large Precursor Weapon",     Category="Weapons",  RecommendedLevel=5, AlphaCap=-1, WhyText="Leshak weapons — Omega only" },
                    new() { SkillId=47867, SkillName="Precursor Frigate",          Category="Ships",    RecommendedLevel=5, AlphaCap=-1, WhyText="Damavik L1, Nergal L5 — Omega only" },
                    new() { SkillId=47868, SkillName="Precursor Cruiser",          Category="Ships",    RecommendedLevel=4, AlphaCap=-1, WhyText="Vedmak L1, Stormbringer/Ikitursa — Omega only" },
                    new() { SkillId=47869, SkillName="Precursor Battleship",       Category="Ships",    RecommendedLevel=4, AlphaCap=-1, WhyText="Leshak — Omega only" },
                    new() { SkillId=3394,  SkillName="Hull Upgrades",              Category="Armor",    RecommendedLevel=4, AlphaCap=4, WhyText="Armor plates" },
                    new() { SkillId=3392,  SkillName="Mechanics",                  Category="Armor",    RecommendedLevel=4, AlphaCap=5, WhyText="Base armor HP" },
                    new() { SkillId=3426,  SkillName="CPU Management",             Category="Core",     RecommendedLevel=5, AlphaCap=5, WhyText="Complex Trig fits need CPU" },
                    new() { SkillId=3413,  SkillName="Power Grid Management",      Category="Core",     RecommendedLevel=5, AlphaCap=5, WhyText="Leshak PG hungry" },
                }
            },

            // ══════════════════════════════════════════════════════════════════
            //  PIRATE FACTIONS
            // ══════════════════════════════════════════════════════════════════

            // ── Guristas Pirates ─────────────────────────────────────────────
            new FactionSkillPlan
            {
                FactionKey  = "GURISTAS",
                FactionName = "Guristas Pirates",
                Ships       = "Worm, Gila, Rattlesnake",
                Notes       = "Drones + missiles. Shield tank. One of EVE's best LP stores.",
                Skills      = new()
                {
                    new() { SkillId=3436,  SkillName="Drones",                      Category="Drones",   RecommendedLevel=5, AlphaCap=5, WhyText="Guristas ships have massive drone bonuses" },
                    new() { SkillId=3442,  SkillName="Drone Interfacing",           Category="Drones",   RecommendedLevel=5, AlphaCap=4, WhyText="+20% drone DPS — highest priority" },
                    new() { SkillId=3441,  SkillName="Heavy Drone Operation",       Category="Drones",   RecommendedLevel=4, AlphaCap=4, WhyText="Heavy drones for L3-L4 content" },
                    new() { SkillId=23606, SkillName="Drone Sharpshooting",         Category="Drones",   RecommendedLevel=4, AlphaCap=4, WhyText="+5% drone optimal range" },
                    new() { SkillId=3324,  SkillName="Heavy Missiles",              Category="Missiles", RecommendedLevel=4, AlphaCap=4, WhyText="Rattlesnake secondary damage output" },
                    new() { SkillId=3319,  SkillName="Missile Launcher Operation",  Category="Missiles", RecommendedLevel=4, AlphaCap=4, WhyText="Base missile skill" },
                    new() { SkillId=21071, SkillName="Rapid Launch",                Category="Missiles", RecommendedLevel=4, AlphaCap=4, WhyText="+2% ROF per level" },
                    new() { SkillId=3419,  SkillName="Shield Management",           Category="Shield",   RecommendedLevel=5, AlphaCap=5, WhyText="All Guristas hulls are shield-tanked" },
                    new() { SkillId=3416,  SkillName="Shield Operation",            Category="Shield",   RecommendedLevel=4, AlphaCap=5, WhyText="Passive regen" },
                    new() { SkillId=3425,  SkillName="Shield Upgrades",             Category="Shield",   RecommendedLevel=4, AlphaCap=4, WhyText="Fit shield extenders" },
                    new() { SkillId=3318,  SkillName="Weapon Upgrades",             Category="Core",     RecommendedLevel=5, AlphaCap=5, WhyText="Reduces weapon CPU use — V for T2 modules" },
                    new() { SkillId=11207, SkillName="Advanced Weapon Upgrades",    Category="Core",     RecommendedLevel=5, AlphaCap=3, WhyText="Reduces T2 weapon PG use — mandatory for T2 fits" },
                    new() { SkillId=3426,  SkillName="CPU Management",              Category="Core",     RecommendedLevel=5, AlphaCap=5, WhyText="Drone control units + launchers" },
                    new() { SkillId=3413,  SkillName="Power Grid Management",       Category="Core",     RecommendedLevel=5, AlphaCap=5, WhyText="Fit full missile rack" },
                }
            },

            // ── Angel Cartel ─────────────────────────────────────────────────
            new FactionSkillPlan
            {
                FactionKey  = "ANGELS",
                FactionName = "Angel Cartel",
                Ships       = "Dramiel, Cynabal, Machariel",
                Notes       = "Projectiles + speed. Machariel is the king of L4 missions.",
                Skills      = new()
                {
                    new() { SkillId=3302, SkillName="Small Projectile Turret",     Category="Projectiles", RecommendedLevel=4, AlphaCap=4, WhyText="Dramiel autocannons" },
                    new() { SkillId=3305, SkillName="Medium Projectile Turret",    Category="Projectiles", RecommendedLevel=5, AlphaCap=4, WhyText="Cynabal guns" },
                    new() { SkillId=3308, SkillName="Large Projectile Turret",     Category="Projectiles", RecommendedLevel=5, AlphaCap=4, WhyText="Machariel 800mm ACs — top priority" },
                    new() { SkillId=3310, SkillName="Rapid Firing",                Category="Projectiles", RecommendedLevel=4, AlphaCap=4, WhyText="+3% RoF per level" },
                    new() { SkillId=3315, SkillName="Surgical Strike",             Category="Projectiles", RecommendedLevel=4, AlphaCap=4, WhyText="+3% damage per level" },
                    new() { SkillId=3317, SkillName="Trajectory Analysis",         Category="Projectiles", RecommendedLevel=4, AlphaCap=4, WhyText="+5% optimal range" },
                    new() { SkillId=3312, SkillName="Motion Prediction",           Category="Projectiles", RecommendedLevel=4, AlphaCap=4, WhyText="Tracking speed" },
                    new() { SkillId=3449, SkillName="Navigation",                  Category="Navigation",  RecommendedLevel=5, AlphaCap=5, WhyText="Mach speed advantage" },
                    new() { SkillId=3450, SkillName="Afterburner",                 Category="Navigation",  RecommendedLevel=4, AlphaCap=4, WhyText="AB speed + cap efficiency" },
                    new() { SkillId=3454, SkillName="High Speed Maneuvering",      Category="Navigation",  RecommendedLevel=4, AlphaCap=4, WhyText="MWD sig bloom reduction" },
                    new() { SkillId=3419, SkillName="Shield Management",           Category="Shield",      RecommendedLevel=4, AlphaCap=5, WhyText="Standard Mach shield buffer" },
                    new() { SkillId=3416, SkillName="Shield Operation",            Category="Shield",      RecommendedLevel=4, AlphaCap=5, WhyText="Passive regen" },
                    new() { SkillId=3318,  SkillName="Weapon Upgrades",             Category="Core",        RecommendedLevel=5, AlphaCap=5, WhyText="Reduces weapon CPU use — V for T2 modules" },
                    new() { SkillId=11207, SkillName="Advanced Weapon Upgrades",   Category="Core",        RecommendedLevel=5, AlphaCap=3, WhyText="Reduces T2 weapon PG use — mandatory for T2 fits" },
                    new() { SkillId=3426,  SkillName="CPU Management",             Category="Core",        RecommendedLevel=5, AlphaCap=5, WhyText="Fit all the guns" },
                    new() { SkillId=3413,  SkillName="Power Grid Management",      Category="Core",        RecommendedLevel=5, AlphaCap=5, WhyText="800mm ACs are PG hungry" },
                }
            },

            // ── Blood Raiders ────────────────────────────────────────────────
            new FactionSkillPlan
            {
                FactionKey  = "BLOOD",
                FactionName = "Blood Raiders",
                Ships       = "Cruor, Ashimmu, Bhaalgorn",
                Notes       = "Lasers + energy neuts. Armor tank. Blood rats neut hard — bring cap boosters.",
                Skills      = new()
                {
                    new() { SkillId=3303, SkillName="Small Energy Turret",         Category="Lasers",    RecommendedLevel=4, AlphaCap=4, WhyText="Cruor pulse lasers" },
                    new() { SkillId=3306, SkillName="Medium Energy Turret",        Category="Lasers",    RecommendedLevel=4, AlphaCap=4, WhyText="Ashimmu pulse lasers" },
                    new() { SkillId=3309, SkillName="Large Energy Turret",         Category="Lasers",    RecommendedLevel=5, AlphaCap=4, WhyText="Bhaalgorn mega pulse — essential" },
                    new() { SkillId=3316, SkillName="Controlled Bursts",           Category="Lasers",    RecommendedLevel=5, AlphaCap=4, WhyText="Reduce laser cap use — critical vs neuting rats" },
                    new() { SkillId=3421, SkillName="Energy Pulse Weapons",        Category="Lasers",    RecommendedLevel=4, AlphaCap=4, WhyText="+3% RoF per level" },
                    new() { SkillId=3311, SkillName="Sharpshooter",                Category="Lasers",    RecommendedLevel=4, AlphaCap=4, WhyText="+5% optimal range" },
                    new() { SkillId=3418, SkillName="Capacitor Management",        Category="Capacitor", RecommendedLevel=5, AlphaCap=4, WhyText="Blood neuts drain cap — mandatory skill" },
                    new() { SkillId=3417, SkillName="Capacitor Systems Operation", Category="Capacitor", RecommendedLevel=5, AlphaCap=4, WhyText="Faster cap recharge" },
                    new() { SkillId=3424, SkillName="Energy Grid Upgrades",        Category="Capacitor", RecommendedLevel=4, AlphaCap=4, WhyText="Fit cap booster modules" },
                    new() { SkillId=3394, SkillName="Hull Upgrades",               Category="Armor",     RecommendedLevel=4, AlphaCap=4, WhyText="Armor plates + hardeners" },
                    new() { SkillId=3392, SkillName="Mechanics",                   Category="Armor",     RecommendedLevel=4, AlphaCap=5, WhyText="Base armor HP" },
                    new() { SkillId=3393, SkillName="Repair Systems",              Category="Armor",     RecommendedLevel=4, AlphaCap=4, WhyText="Armor rep cycle time" },
                    new() { SkillId=3318,  SkillName="Weapon Upgrades",             Category="Core",      RecommendedLevel=5, AlphaCap=5, WhyText="Reduces weapon CPU use — V for T2 modules" },
                    new() { SkillId=11207, SkillName="Advanced Weapon Upgrades",   Category="Core",      RecommendedLevel=5, AlphaCap=3, WhyText="Reduces T2 weapon PG use — mandatory for T2 fits" },
                    new() { SkillId=3426,  SkillName="CPU Management",             Category="Core",      RecommendedLevel=5, AlphaCap=5, WhyText="Cap modules are CPU hungry" },
                    new() { SkillId=3413,  SkillName="Power Grid Management",      Category="Core",      RecommendedLevel=5, AlphaCap=5, WhyText="Mega pulse + armor plates" },
                }
            },

            // ── Serpentis Corporation ────────────────────────────────────────
            new FactionSkillPlan
            {
                FactionKey  = "SERPENTIS",
                FactionName = "Serpentis Corporation",
                Ships       = "Daredevil, Vigilant, Vindicator",
                Notes       = "Blasters + webs. Armor tank. Vindicator is one of the best L4 ships.",
                Skills      = new()
                {
                    new() { SkillId=3301, SkillName="Small Hybrid Turret",         Category="Hybrids",   RecommendedLevel=4, AlphaCap=4, WhyText="Daredevil blasters" },
                    new() { SkillId=3304, SkillName="Medium Hybrid Turret",        Category="Hybrids",   RecommendedLevel=5, AlphaCap=4, WhyText="Vigilant blasters" },
                    new() { SkillId=3307, SkillName="Large Hybrid Turret",         Category="Hybrids",   RecommendedLevel=5, AlphaCap=4, WhyText="Vindicator neutron blasters — key skill" },
                    new() { SkillId=3310, SkillName="Rapid Firing",                Category="Hybrids",   RecommendedLevel=4, AlphaCap=4, WhyText="+3% RoF per level" },
                    new() { SkillId=3315, SkillName="Surgical Strike",             Category="Hybrids",   RecommendedLevel=4, AlphaCap=4, WhyText="+3% damage per level" },
                    new() { SkillId=3312, SkillName="Motion Prediction",           Category="Hybrids",   RecommendedLevel=4, AlphaCap=4, WhyText="Blasters have great tracking — maintain it" },
                    new() { SkillId=3311, SkillName="Sharpshooter",                Category="Hybrids",   RecommendedLevel=4, AlphaCap=4, WhyText="+5% optimal range" },
                    new() { SkillId=3449, SkillName="Navigation",                  Category="Navigation", RecommendedLevel=4, AlphaCap=5, WhyText="Close-range — burn to target fast" },
                    new() { SkillId=3450, SkillName="Afterburner",                 Category="Navigation", RecommendedLevel=4, AlphaCap=4, WhyText="Speed for getting into blaster range" },
                    new() { SkillId=3394, SkillName="Hull Upgrades",               Category="Armor",     RecommendedLevel=4, AlphaCap=4, WhyText="Armor plates + hardeners" },
                    new() { SkillId=3392, SkillName="Mechanics",                   Category="Armor",     RecommendedLevel=4, AlphaCap=5, WhyText="Base armor HP" },
                    new() { SkillId=3393, SkillName="Repair Systems",              Category="Armor",     RecommendedLevel=4, AlphaCap=4, WhyText="Armor rep cycle time" },
                    new() { SkillId=3318,  SkillName="Weapon Upgrades",             Category="Core",      RecommendedLevel=5, AlphaCap=5, WhyText="Reduces weapon CPU use — V for T2 modules" },
                    new() { SkillId=11207, SkillName="Advanced Weapon Upgrades",   Category="Core",      RecommendedLevel=5, AlphaCap=3, WhyText="Reduces T2 weapon PG use — mandatory for T2 fits" },
                    new() { SkillId=3426,  SkillName="CPU Management",             Category="Core",      RecommendedLevel=5, AlphaCap=5, WhyText="Blasters + webs need CPU" },
                    new() { SkillId=3413,  SkillName="Power Grid Management",      Category="Core",      RecommendedLevel=5, AlphaCap=5, WhyText="Neutron blasters are PG hungry" },
                }
            },

            // ── Sansha's Nation ──────────────────────────────────────────────
            new FactionSkillPlan
            {
                FactionKey  = "SANSHA",
                FactionName = "Sansha's Nation",
                Ships       = "Succubus, Phantasm, Nightmare",
                Notes       = "Lasers + shields. Nightmare is a superb L4 runner with great cap bonuses.",
                Skills      = new()
                {
                    new() { SkillId=3303, SkillName="Small Energy Turret",         Category="Lasers",    RecommendedLevel=4, AlphaCap=4, WhyText="Succubus pulse lasers" },
                    new() { SkillId=3306, SkillName="Medium Energy Turret",        Category="Lasers",    RecommendedLevel=4, AlphaCap=4, WhyText="Phantasm lasers" },
                    new() { SkillId=3309, SkillName="Large Energy Turret",         Category="Lasers",    RecommendedLevel=5, AlphaCap=4, WhyText="Nightmare mega pulse — key skill" },
                    new() { SkillId=3421, SkillName="Energy Pulse Weapons",        Category="Lasers",    RecommendedLevel=4, AlphaCap=4, WhyText="+3% RoF per level" },
                    new() { SkillId=3311, SkillName="Sharpshooter",                Category="Lasers",    RecommendedLevel=4, AlphaCap=4, WhyText="+5% optimal range" },
                    new() { SkillId=3316, SkillName="Controlled Bursts",           Category="Lasers",    RecommendedLevel=4, AlphaCap=4, WhyText="Reduce cap use — cap-stable Nightmare" },
                    new() { SkillId=3418, SkillName="Capacitor Management",        Category="Capacitor", RecommendedLevel=4, AlphaCap=4, WhyText="Lasers are cap-heavy" },
                    new() { SkillId=3417, SkillName="Capacitor Systems Operation", Category="Capacitor", RecommendedLevel=4, AlphaCap=4, WhyText="Faster cap recharge" },
                    new() { SkillId=3419, SkillName="Shield Management",           Category="Shield",    RecommendedLevel=5, AlphaCap=5, WhyText="Nightmare shield-tanks despite using lasers" },
                    new() { SkillId=3416, SkillName="Shield Operation",            Category="Shield",    RecommendedLevel=4, AlphaCap=5, WhyText="Passive regen" },
                    new() { SkillId=3425, SkillName="Shield Upgrades",             Category="Shield",    RecommendedLevel=4, AlphaCap=4, WhyText="Fit shield extenders" },
                    new() { SkillId=3420, SkillName="Tactical Shield Manipulation",Category="Shield",    RecommendedLevel=4, AlphaCap=4, WhyText="Reduces damage bleed-through" },
                    new() { SkillId=3318,  SkillName="Weapon Upgrades",             Category="Core",      RecommendedLevel=5, AlphaCap=5, WhyText="Reduces weapon CPU use — V for T2 modules" },
                    new() { SkillId=11207, SkillName="Advanced Weapon Upgrades",   Category="Core",      RecommendedLevel=5, AlphaCap=3, WhyText="Reduces T2 weapon PG use — mandatory for T2 fits" },
                    new() { SkillId=3426,  SkillName="CPU Management",             Category="Core",      RecommendedLevel=5, AlphaCap=5, WhyText="Cap mods + turrets need CPU" },
                    new() { SkillId=3413,  SkillName="Power Grid Management",      Category="Core",      RecommendedLevel=5, AlphaCap=5, WhyText="Mega pulse lasers are PG hungry" },
                }
            },

            // ── Mordu's Legion Command ───────────────────────────────────────
            new FactionSkillPlan
            {
                FactionKey  = "MORDUS",
                FactionName = "Mordu's Legion Command",
                Ships       = "Garmur, Orthrus, Barghest",
                Notes       = "Light missiles + point defense. Shield tank. Garmur/Orthrus are elite tackle ships.",
                Skills      = new()
                {
                    new() { SkillId=3321,  SkillName="Light Missiles",              Category="Missiles",  RecommendedLevel=5, AlphaCap=4, WhyText="Garmur primary weapon system" },
                    new() { SkillId=3324,  SkillName="Heavy Missiles",              Category="Missiles",  RecommendedLevel=4, AlphaCap=4, WhyText="Orthrus/Barghest HM option" },
                    new() { SkillId=3319,  SkillName="Missile Launcher Operation",  Category="Missiles",  RecommendedLevel=5, AlphaCap=4, WhyText="Base missile skill" },
                    new() { SkillId=21071, SkillName="Rapid Launch",                Category="Missiles",  RecommendedLevel=4, AlphaCap=4, WhyText="+2% ROF per level" },
                    new() { SkillId=20314, SkillName="Target Navigation Prediction",Category="Missiles",  RecommendedLevel=4, AlphaCap=4, WhyText="Reduces velocity penalty" },
                    new() { SkillId=20312, SkillName="Guided Missile Precision",    Category="Missiles",  RecommendedLevel=4, AlphaCap=4, WhyText="Hit smaller fast targets" },
                    new() { SkillId=3449,  SkillName="Navigation",                  Category="Navigation",RecommendedLevel=4, AlphaCap=5, WhyText="Garmur/Orthrus rely on speed" },
                    new() { SkillId=3450,  SkillName="Afterburner",                 Category="Navigation",RecommendedLevel=4, AlphaCap=4, WhyText="AB speed + cap efficiency" },
                    new() { SkillId=3419,  SkillName="Shield Management",           Category="Shield",    RecommendedLevel=5, AlphaCap=5, WhyText="Mordu's ships are shield-tanked" },
                    new() { SkillId=3416,  SkillName="Shield Operation",            Category="Shield",    RecommendedLevel=4, AlphaCap=5, WhyText="Passive regen" },
                    new() { SkillId=3425,  SkillName="Shield Upgrades",             Category="Shield",    RecommendedLevel=4, AlphaCap=4, WhyText="Fit shield extenders" },
                    new() { SkillId=3318,  SkillName="Weapon Upgrades",             Category="Core",      RecommendedLevel=5, AlphaCap=5, WhyText="Reduces weapon CPU use — V for T2 modules" },
                    new() { SkillId=11207, SkillName="Advanced Weapon Upgrades",   Category="Core",      RecommendedLevel=5, AlphaCap=3, WhyText="Reduces T2 weapon PG use — mandatory for T2 fits" },
                    new() { SkillId=3426,  SkillName="CPU Management",             Category="Core",      RecommendedLevel=5, AlphaCap=5, WhyText="Fit full rig/module stack" },
                    new() { SkillId=3413,  SkillName="Power Grid Management",      Category="Core",      RecommendedLevel=5, AlphaCap=5, WhyText="Launcher-heavy fits" },
                }
            },

            // ══════════════════════════════════════════════════════════════════
            //  OTHER (unique ships open to all players)
            // ══════════════════════════════════════════════════════════════════

            // ── Society of Conscious Thought ─────────────────────────────────
            new FactionSkillPlan
            {
                FactionKey  = "SOCT",
                FactionName = "Society of Conscious Thought",
                Ships       = "Gnosis, Sunesis, Praxis (open to all players)",
                Notes       = "Neutral faction. Ships accept any weapon. Praxis (missiles + drones) is the most popular choice.",
                Skills      = new()
                {
                    new() { SkillId=3436,  SkillName="Drones",                      Category="Drones",   RecommendedLevel=5, AlphaCap=5, WhyText="Works on Gnosis, Praxis, and Sunesis" },
                    new() { SkillId=3442,  SkillName="Drone Interfacing",           Category="Drones",   RecommendedLevel=4, AlphaCap=4, WhyText="+20% drone DPS" },
                    new() { SkillId=3441,  SkillName="Heavy Drone Operation",       Category="Drones",   RecommendedLevel=4, AlphaCap=4, WhyText="Heavy drones for bigger rats" },
                    new() { SkillId=3319,  SkillName="Missile Launcher Operation",  Category="Missiles", RecommendedLevel=4, AlphaCap=4, WhyText="Praxis missile base skill" },
                    new() { SkillId=3324,  SkillName="Heavy Missiles",              Category="Missiles", RecommendedLevel=4, AlphaCap=4, WhyText="Praxis missile-drone hybrid" },
                    new() { SkillId=21071, SkillName="Rapid Launch",                Category="Missiles", RecommendedLevel=4, AlphaCap=4, WhyText="+2% ROF" },
                    new() { SkillId=20314, SkillName="Target Navigation Prediction",Category="Missiles", RecommendedLevel=4, AlphaCap=4, WhyText="Hit faster targets" },
                    new() { SkillId=3419,  SkillName="Shield Management",           Category="Shield",   RecommendedLevel=4, AlphaCap=5, WhyText="Praxis shield-tanked by default" },
                    new() { SkillId=3416,  SkillName="Shield Operation",            Category="Shield",   RecommendedLevel=4, AlphaCap=5, WhyText="Passive regen" },
                    new() { SkillId=3394,  SkillName="Hull Upgrades",               Category="Armor",    RecommendedLevel=4, AlphaCap=4, WhyText="Gnosis armor option" },
                    new() { SkillId=3392,  SkillName="Mechanics",                   Category="Armor",    RecommendedLevel=4, AlphaCap=5, WhyText="Base armor HP" },
                    new() { SkillId=3318,  SkillName="Weapon Upgrades",             Category="Core",     RecommendedLevel=5, AlphaCap=5, WhyText="Reduces weapon CPU use — V for T2 modules" },
                    new() { SkillId=11207, SkillName="Advanced Weapon Upgrades",   Category="Core",     RecommendedLevel=5, AlphaCap=3, WhyText="Reduces T2 weapon PG use — mandatory for T2 fits" },
                    new() { SkillId=3426,  SkillName="CPU Management",             Category="Core",     RecommendedLevel=5, AlphaCap=5, WhyText="Praxis slots need CPU" },
                    new() { SkillId=3413,  SkillName="Power Grid Management",      Category="Core",     RecommendedLevel=5, AlphaCap=5, WhyText="Fit all launchers + drone control" },
                }
            },

        };

        public static FactionSkillPlan? GetByKey(string key)
            => All.Find(p => p.FactionKey == key?.ToUpperInvariant());
    }
}
