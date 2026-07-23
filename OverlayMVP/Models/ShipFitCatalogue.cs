// filename: Models/ShipFitCatalogue.cs
// Static catalogue of verified minimum mission fits per hull.
// Slot counts verified against EVE University Wiki / EVE Ref.
// Module names verified against EVE Ref (everef.net).
// Skills are the minimum required to USE every module in the listed fit.
using System.Collections.Generic;

namespace OverlayMVP.Models
{
    public sealed class ShipFitData
    {
        public string   Hull          { get; init; } = "";
        public string   ShipClass     { get; init; } = "";
        public string   Role          { get; init; } = "";
        public string[] RequiredSkills{ get; init; } = System.Array.Empty<string>();
        public string[] HighSlots     { get; init; } = System.Array.Empty<string>();
        public string[] MidSlots      { get; init; } = System.Array.Empty<string>();
        public string[] LowSlots      { get; init; } = System.Array.Empty<string>();
        public string[] RigSlots      { get; init; } = System.Array.Empty<string>();
        public string   Notes         { get; init; } = "";
        public string   IskEstimate   { get; init; } = "";
    }

    public static class ShipFitCatalogue
    {
        private static readonly Dictionary<string, ShipFitData> _fits = new(
            System.StringComparer.OrdinalIgnoreCase)
        {
            // ── CALDARI ──────────────────────────────────────────────────────────
            // Slot reference (EVE Uni Wiki):
            //   Badger:    3H 3M 4L 3R | no hardpoints | 0 Mbit drones
            //   Crane:     3H 3M 3L 3R | 0 turrets 0 launchers | 0 Mbit
            //   Heron:     4H 3M 2L 3R | 0 turrets 0 launchers | 0 Mbit
            //   Caracal:   5H 5M 4L 3R | 0 turrets 5 launchers | 10 Mbit 10m³
            //   Moa:       5H 5M 4L 3R | 5 turrets 0 launchers | 15 Mbit 15m³
            //   Drake:     7H 6M 4L 3R | 0 turrets 6 launchers | 25 Mbit 25m³
            //   Ferox:     7H 6M 4L 3R | 6 turrets 0 launchers | 25 Mbit 25m³
            //   Nighthawk: 7H 6M 4L 2R | 2 turrets 5 launchers | 25 Mbit 25m³
            //   Cerberus:  6H 5M 4L 2R | 0 turrets 6 launchers | 15 Mbit 15m³
            //   Raven:     7H 7M 5L 3R | 4 turrets 6 launchers | 50 Mbit 75m³
            //   Golem:     8H 7M 4L 2R | 0 turrets 4 launchers | 25 Mbit 75m³
            //   Tengu:     variable by subsystem (typically 5H 5M 4L 3R combat config)

            ["Badger"] = new()
            {
                Hull = "Badger", ShipClass = "Industrial (3H 3M 4L 3R)", Role = "Distribution / Hauler",
                RequiredSkills = new[]
                {
                    "Caldari Industrial I — to fly",
                    "Hull Upgrades I — for Expanded Cargoholds",
                },
                HighSlots = new[]
                {
                    "— (empty — no weapons needed for distribution)",
                    "— (empty)",
                    "— (empty)",
                },
                MidSlots = new[]
                {
                    "1MN Afterburner I",
                    "EM Shield Amplifier I (situational)",
                    "— (empty)",
                },
                LowSlots = new[]
                {
                    "Expanded Cargohold II",
                    "Expanded Cargohold II",
                    "Expanded Cargohold II",
                    "Expanded Cargohold II",
                },
                RigSlots = new[]
                {
                    "Large Cargohold Optimisation I",
                    "Large Cargohold Optimisation I",
                    "— (empty)",
                },
                Notes = "Pure cargo fit — ~6 000 m³ with rigs and four Expanded Cargoholds. Distribution L1 needs ~1 000–2 000 m³. No active tank required — just warp away from combat. Upgrade to Crane (T2 Industrial) for better align time and warp strength.",
                IskEstimate = "~5–15M ISK fully fitted",
            },

            ["Crane"] = new()
            {
                Hull = "Crane", ShipClass = "Transport Ship T2 (3H 3M 3L 3R)", Role = "Blockade Runner / Hauler",
                RequiredSkills = new[]
                {
                    "Caldari Industrial V — prerequisite for Transport Ships",
                    "Transport Ships I — to fly",
                    "Cloaking IV — to fit Covert Ops Cloaking Device II",
                },
                HighSlots = new[]
                {
                    "Covert Ops Cloaking Device II",
                    "— (empty)",
                    "— (empty)",
                },
                MidSlots = new[]
                {
                    "1MN Afterburner II",
                    "Medium Shield Extender II",
                    "— (empty)",
                },
                LowSlots = new[]
                {
                    "Expanded Cargohold II",
                    "Expanded Cargohold II",
                    "Expanded Cargohold II",
                },
                RigSlots = new[]
                {
                    "Medium Cargohold Optimisation I",
                    "Medium Cargohold Optimisation I",
                    "— (empty)",
                },
                Notes = "Covert Ops cloak lets you warp while cloaked — superior safety for distribution. ~3 500–4 500 m³ cargo handles all L1–L2 distribution missions. Warp cloaked whenever possible.",
                IskEstimate = "~60–90M ISK fitted",
            },

            ["Heron"] = new()
            {
                Hull = "Heron", ShipClass = "Frigate (4H 3M 2L 3R)", Role = "Exploration / Light Distribution",
                RequiredSkills = new[]
                {
                    "Caldari Frigate I — to fly",
                    "Astrometrics III — to use Core Probe Launcher",
                    "Survey I — to use Core Probes",
                },
                HighSlots = new[]
                {
                    "Core Probe Launcher I",
                    "— (empty)",
                    "— (empty)",
                    "— (empty)",
                },
                MidSlots = new[]
                {
                    "1MN Afterburner I",
                    "Cargo Scanner I",
                    "— (empty)",
                },
                LowSlots = new[]
                {
                    "Expanded Cargohold I",
                    "Expanded Cargohold I",
                },
                RigSlots = new[]
                {
                    "Small Cargohold Optimisation I",
                    "— (empty)",
                    "— (empty)",
                },
                Notes = "Cheap exploration frigate. Can do L1 distribution. Heron has no drone bandwidth — it cannot use drones. Not recommended for combat missions — use Caracal instead.",
                IskEstimate = "~2–5M ISK fitted",
            },

            ["Caracal"] = new()
            {
                Hull = "Caracal", ShipClass = "Cruiser (5H 5M 4L 3R)", Role = "L2 Mission Runner",
                RequiredSkills = new[]
                {
                    "Caldari Cruiser III — to fly",
                    "Heavy Missiles IV — to fit Heavy Missile Launcher II",
                    "Missile Launcher Operation IV — prerequisite for HML",
                    "Shield Operation III — for shield skills",
                    "Shield Upgrades III — to fit Large Shield Extender II",
                    "Missile Bombardment III — increases HML range",
                    "Warhead Upgrades III — increases HML damage",
                },
                HighSlots = new[]
                {
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                },
                MidSlots = new[]
                {
                    "Large Shield Extender II",
                    "Kinetic Shield Amplifier II",
                    "Thermal Shield Amplifier II",
                    "1MN Afterburner II",
                    "Cap Recharger II",
                },
                LowSlots = new[]
                {
                    "Ballistic Control System II",
                    "Ballistic Control System II",
                    "Power Diagnostic System II",
                    "— (empty)",
                },
                RigSlots = new[]
                {
                    "Medium Core Defense Field Extender I",
                    "Medium Core Defense Field Extender I",
                    "Medium Thermal Shield Reinforcer I",
                },
                Notes = "The definitive L2 Caldari ship. ~150–200 DPS with 5× HML II + 2× BCS II. 15–20k EHP with LSE II + passive hardeners. Kinetic/Thermal tank against Guristas. Caracal has only 10 Mbit drone bandwidth — cannot field drones effectively.",
                IskEstimate = "~25–40M ISK fitted",
            },

            ["Moa"] = new()
            {
                Hull = "Moa", ShipClass = "Cruiser (5H 5M 4L 3R)", Role = "L2 Mission Runner (Hybrid turrets)",
                RequiredSkills = new[]
                {
                    "Caldari Cruiser III — to fly",
                    "Medium Hybrid Turret IV — to fit 150mm Railgun II",
                    "Gunnery IV — prerequisite for turret skills",
                    "Motion Prediction III — tracking skill",
                    "Sharpshooter III — optimal range skill",
                    "Shield Operation III",
                    "Shield Upgrades III — for Large Shield Extender II",
                },
                HighSlots = new[]
                {
                    "150mm Railgun II [Antimatter Charge M]",
                    "150mm Railgun II [Antimatter Charge M]",
                    "150mm Railgun II [Antimatter Charge M]",
                    "150mm Railgun II [Antimatter Charge M]",
                    "150mm Railgun II [Antimatter Charge M]",
                },
                MidSlots = new[]
                {
                    "Large Shield Extender II",
                    "Kinetic Shield Amplifier II",
                    "Thermal Shield Amplifier II",
                    "1MN Afterburner II",
                    "Cap Recharger II",
                },
                LowSlots = new[]
                {
                    "Magnetic Field Stabilizer II",
                    "Magnetic Field Stabilizer II",
                    "Damage Control II",
                    "— (empty)",
                },
                RigSlots = new[]
                {
                    "Medium Core Defense Field Extender I",
                    "Medium Core Defense Field Extender I",
                    "Medium Hybrid Burst Aerator I",
                },
                Notes = "Railgun alternative to Caracal. Use Antimatter charges at close range, Spike for long range. Moa has 15 Mbit drone bandwidth — can field 1 medium or 3 light drones. Caracal generally easier at L2.",
                IskEstimate = "~20–35M ISK fitted",
            },

            ["Drake"] = new()
            {
                Hull = "Drake", ShipClass = "Battlecruiser (7H 6M 4L 3R)", Role = "L3 Mission Runner",
                RequiredSkills = new[]
                {
                    "Caldari Battlecruiser III — to fly",
                    "Heavy Missiles IV — to fit Heavy Missile Launcher II",
                    "Missile Launcher Operation IV — prerequisite",
                    "Shield Operation IV — for cap stability",
                    "Shield Management III — increases shield HP",
                    "Shield Upgrades IV — to fit Large Shield Extender II",
                    "Missile Bombardment I — to fit Missile Guidance Computer I",
                    "Warhead Upgrades III — damage",
                    "Drones III — improves drone range and damage",
                    "Light Drone Operation I — to deploy Warrior I (T1 light drones, 5 Mbit each)",
                    "Drone Avionics III — to fit Drone Link Augmentor I",
                },
                HighSlots = new[]
                {
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Drone Link Augmentor I",
                },
                MidSlots = new[]
                {
                    "Large Shield Extender II",
                    "Large Shield Extender II",
                    "Kinetic Shield Amplifier II",
                    "Thermal Shield Amplifier II",
                    "Missile Guidance Computer I",
                    "Cap Recharger II",
                },
                LowSlots = new[]
                {
                    "Ballistic Control System II",
                    "Ballistic Control System II",
                    "Ballistic Control System II",
                    "Power Diagnostic System II",
                },
                RigSlots = new[]
                {
                    "Medium Core Defense Field Extender I",
                    "Medium Core Defense Field Extender I",
                    "Medium Thermal Shield Reinforcer I",
                },
                Notes = "Pyfa-verified (All V skills): 369 weapon DPS (Scourge HM) + ~25 drone DPS = ~394 DPS total. 56.6k omni-EHP / 43.5k shield EHP (20% EM / 74.9% Kin / 74.5% Therm / 60% Expl). Cap stable at 98.5%. Passive regen 113 EHP/s. CPU 623/625 tf. Use Scourge HM vs Guristas (kinetic bonus). Drake has 25 Mbit — field 5× Warrior I.",
                IskEstimate = "~75–100M ISK fitted",
            },

            ["Ferox"] = new()
            {
                Hull = "Ferox", ShipClass = "Battlecruiser (7H 6M 4L 3R)", Role = "L3 Mission Runner (Railguns)",
                RequiredSkills = new[]
                {
                    "Caldari Battlecruiser III — to fly",
                    "Medium Hybrid Turret IV — to fit 250mm Railgun II",
                    "Gunnery IV",
                    "Sharpshooter IV — optimal range bonus",
                    "Motion Prediction III — tracking",
                    "Shield Operation III",
                    "Shield Upgrades IV — for Large Shield Extender II",
                    "Drones III — to use light drones (25 Mbit = 5 light drones)",
                    "Drone Avionics III — to fit Drone Link Augmentor I",
                },
                HighSlots = new[]
                {
                    "250mm Railgun II [Antimatter Charge M]",
                    "250mm Railgun II [Antimatter Charge M]",
                    "250mm Railgun II [Antimatter Charge M]",
                    "250mm Railgun II [Antimatter Charge M]",
                    "250mm Railgun II [Antimatter Charge M]",
                    "250mm Railgun II [Antimatter Charge M]",
                    "Drone Link Augmentor I",
                },
                MidSlots = new[]
                {
                    "Large Shield Extender II",
                    "Large Shield Extender II",
                    "Kinetic Shield Amplifier II",
                    "Thermal Shield Amplifier II",
                    "10MN Afterburner II",
                    "Cap Recharger II",
                },
                LowSlots = new[]
                {
                    "Magnetic Field Stabilizer II",
                    "Magnetic Field Stabilizer II",
                    "Damage Control II",
                    "Power Diagnostic System II",
                },
                RigSlots = new[]
                {
                    "Medium Core Defense Field Extender I",
                    "Medium Core Defense Field Extender I",
                    "Medium Hybrid Burst Aerator I",
                },
                Notes = "Pyfa-verified (All V skills): 441 weapon DPS (Antimatter M) + ~25 drone DPS = ~466 DPS. Volley: 1,419. 57k omni-EHP / 37k shield EHP (12.5% EM / 62.8% Kin / 72.1% Therm / 56.2% Expl). Passive regen 96.2 EHP/s. Cap stable 70.3%. CPU 531.8/643.8 tf. PG is tight at 97.2% — no headroom. Speed 420 m/s with AB. Use Antimatter at <20km or Spike for 70km+. Ferox has 25 Mbit — 5× Warrior I. Drake is more efficient for missile pilots.",
                IskEstimate = "~65–80M ISK fitted",
            },

            ["Nighthawk"] = new()
            {
                Hull = "Nighthawk", ShipClass = "Command Ship T2 (7H 6M 4L 2R)", Role = "L3–L4 Fast Clear",
                RequiredSkills = new[]
                {
                    "Caldari Battlecruiser V — prerequisite",
                    "Command Ships I — to fly",
                    "Heavy Missiles V — for T2 launchers",
                    "Missile Launcher Operation V — prerequisite",
                    "Shield Operation IV",
                    "Shield Upgrades IV — for Large Shield Extender II",
                    "Fleet Command I — to fit Shield Command Burst I",
                    "Shield Command I — to use Shield Harmonizing Charge",
                    "Missile Bombardment IV — required for Missile Guidance Computer II",
                    "Drones III — improves drone range and damage",
                    "Light Drone Operation I — to deploy Warrior I (T1 light drones, 5 Mbit each)",
                    "Drone Avionics III — to fit Drone Link Augmentor I",
                },
                HighSlots = new[]
                {
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Shield Command Burst I (load: Shield Harmonizing Charge)",
                    "Drone Link Augmentor I",
                },
                MidSlots = new[]
                {
                    "Large Shield Extender II",
                    "Large Shield Extender II",
                    "Kinetic Shield Amplifier II",
                    "Thermal Shield Amplifier II",
                    "Missile Guidance Computer II",
                    "Cap Recharger II",
                },
                LowSlots = new[]
                {
                    "Ballistic Control System II",
                    "Ballistic Control System II",
                    "Ballistic Control System II",
                    "Power Diagnostic System II",
                },
                RigSlots = new[]
                {
                    "Medium Core Defense Field Extender I",
                    "Medium Core Defense Field Extender I",
                },
                Notes = "Pyfa-verified (All V skills): 492 weapon DPS (Scourge HM) + ~25 drone DPS = ~517 DPS total. Volley: 1,942. 67.9k omni-EHP / 52.6k shield EHP (20% EM / 91.5% Kin / 87.2% Therm / 60% Expl). Passive regen 137 EHP/s. Cap stable 97.6%. CPU 639/700 tf. Nighthawk has only 2 rig slots and 5 launcher hardpoints — 5 HML II + Command Burst + DLA = 7 highs filled. Drones: 5× Warrior I (25 Mbit).",
                IskEstimate = "~360–400M ISK fitted",
            },

            ["Cerberus"] = new()
            {
                Hull = "Cerberus", ShipClass = "Heavy Assault Cruiser T2 (6H 5M 4L 2R)", Role = "L3–L4 HAC",
                RequiredSkills = new[]
                {
                    "Caldari Cruiser V — prerequisite",
                    "Heavy Assault Cruisers I — to fly",
                    "Heavy Missiles V — for T2 launchers",
                    "Missile Launcher Operation V — prerequisite",
                    "Shield Operation IV",
                    "Shield Upgrades IV — for Large Shield Extender II",
                    "Assault Damage Control I — to fit ADC module",
                },
                HighSlots = new[]
                {
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                },
                MidSlots = new[]
                {
                    "Large Shield Extender II",
                    "Kinetic Shield Amplifier II",
                    "Thermal Shield Amplifier II",
                    "10MN Afterburner II",
                    "Cap Recharger II",
                },
                LowSlots = new[]
                {
                    "Ballistic Control System II",
                    "Ballistic Control System II",
                    "Ballistic Control System II",
                    "Assault Damage Control I",
                },
                RigSlots = new[]
                {
                    "Medium Core Defense Field Extender I",
                    "Medium Thermal Shield Reinforcer I",
                },
                Notes = "Pyfa-verified (All V skills): 410 weapon DPS (Scourge HM) + ~25 drone DPS = ~435 DPS total. 22.9k omni-EHP / 16k shield EHP (5% EM / 92.5% Kin / 84.9% Therm / 52.5% Expl). Cap stable at 94.3%. Passive regen 42.7 EHP/s. CPU 536.8/668.8 tf. Speed 668 m/s with AB. Cerberus has 15 Mbit — field 3× Warrior I. ADC gives 30-second emergency survivability burst.",
                IskEstimate = "~170–200M ISK fitted",
            },

            ["Raven"] = new()
            {
                Hull = "Raven", ShipClass = "Battleship (7H 7M 5L 3R)", Role = "L4 Mission Runner",
                RequiredSkills = new[]
                {
                    "Caldari Battleship III — to fly",
                    "Cruise Missiles IV — to fit Cruise Missile Launcher II",
                    "Missile Launcher Operation V — prerequisite",
                    "Shield Operation IV",
                    "Shield Management IV — increases max shield HP",
                    "Shield Upgrades IV — for Large Shield Extender II",
                    "Electronics Upgrades IV — for Shield Boost Amplifier II",
                    "Energy Management IV — for X-Large Shield Booster II cap use",
                    "Missile Bombardment IV — range",
                    "Warhead Upgrades IV — damage",
                    "Target Painting III — to use Target Painter II",
                    "Drones IV — Raven has 50 Mbit = 5 medium drones",
                    "Medium Drone Operation III — for Hammerhead II",
                    "Drone Avionics III — for Drone Link Augmentor II",
                },
                HighSlots = new[]
                {
                    "Cruise Missile Launcher II [Scourge Cruise Missile]",
                    "Cruise Missile Launcher II [Scourge Cruise Missile]",
                    "Cruise Missile Launcher II [Scourge Cruise Missile]",
                    "Cruise Missile Launcher II [Scourge Cruise Missile]",
                    "Cruise Missile Launcher II [Scourge Cruise Missile]",
                    "Cruise Missile Launcher II [Scourge Cruise Missile]",
                    // 7th high slot empty — Raven has 7H but only 6 launcher hardpoints
                },
                MidSlots = new[]
                {
                    "Target Painter II",
                    "X-Large Shield Booster II",
                    "Kinetic Shield Amplifier II",
                    "Thermal Shield Amplifier II",
                    "Shield Boost Amplifier II",
                    "Cap Recharger II",
                    "Cap Recharger II",
                },
                LowSlots = new[]
                {
                    "Ballistic Control System II",
                    "Ballistic Control System II",
                    "Ballistic Control System II",
                    "Power Diagnostic System II",
                    "Damage Control II",
                },
                RigSlots = new[]
                {
                    "Large Core Defense Field Extender I",
                    "Large Kinetic Shield Reinforcer I",
                    "Large Thermal Shield Reinforcer I",
                },
                Notes = "Pyfa-verified (All V skills): 599 weapon DPS (Scourge CM) + ~50 drone DPS = ~650 DPS total. Volley: 3,910. 61.7k omni-EHP / 25.9k shield EHP (12.5% EM / 72.5% Kin / 79.4% Therm / 56.2% Expl). Active booster 418.6 EHP/s — pulse the booster, cap lasts 2m30s running continuously. CPU 895/937.5 tf. Raven has 7H but only 6 launcher hardpoints — 7th high slot empty. Drones: 5× Hammerhead II (50 Mbit). Use Scourge CM vs Guristas.",
                IskEstimate = "~200–230M ISK fitted",
            },

            ["Raven Navy Issue"] = new()
            {
                Hull = "Raven Navy Issue", ShipClass = "Navy Battleship", Role = "L4 Mission Runner (High-end)",
                RequiredSkills = new[]
                {
                    "Caldari Battleship IV — to fly Navy variant",
                    "Cruise Missiles V — for full DPS bonus",
                    "Missile Launcher Operation V",
                    "Shield Operation V",
                    "Shield Management V",
                    "Missile Bombardment V",
                    "Warhead Upgrades V",
                    "Target Painting III — for Target Painter II",
                    "Drones IV — 50 Mbit = 5 medium drones",
                    "Medium Drone Operation III — for Hammerhead II",
                },
                HighSlots = new[]
                {
                    "Cruise Missile Launcher II [Scourge Cruise Missile]",
                    "Cruise Missile Launcher II [Scourge Cruise Missile]",
                    "Cruise Missile Launcher II [Scourge Cruise Missile]",
                    "Cruise Missile Launcher II [Scourge Cruise Missile]",
                    "Cruise Missile Launcher II [Scourge Cruise Missile]",
                    "Cruise Missile Launcher II [Scourge Cruise Missile]",
                    "Target Painter II",
                },
                MidSlots = new[]
                {
                    "X-Large Shield Booster II",
                    "Kinetic Shield Amplifier II",
                    "Thermal Shield Amplifier II",
                    "Shield Boost Amplifier II",
                    "Cap Recharger II",
                    "Cap Recharger II",
                    "10MN Afterburner II",
                },
                LowSlots = new[]
                {
                    "Ballistic Control System II",
                    "Ballistic Control System II",
                    "Ballistic Control System II",
                    "Ballistic Control System II",
                    "Damage Control II",
                },
                RigSlots = new[]
                {
                    "Large Core Defense Field Extender I",
                    "Large Kinetic Shield Reinforcer I",
                    "Large Thermal Shield Reinforcer I",
                },
                Notes = "Significant upgrade over T1 Raven — same slot layout but more bonuses. 900+ DPS with faction missiles. Drones: 5× Hammerhead II (50 Mbit). Expensive but noticeably faster L4 clears.",
                IskEstimate = "~1.5–2B ISK fitted",
            },

            ["Golem"] = new()
            {
                Hull = "Golem", ShipClass = "Marauder T2 (8H 7M 4L 2R)", Role = "L4 Speed Runner",
                RequiredSkills = new[]
                {
                    "Caldari Battleship V — prerequisite",
                    "Marauders I — to fly (IV for full Bastion bonus)",
                    "Cruise Missiles V — for T2 launchers",
                    "Missile Launcher Operation V",
                    "Tactical Weapon Reconfiguration I — to fit Bastion Module I",
                    "Shield Operation V",
                    "Shield Management IV",
                    "Shield Upgrades IV — for Kinetic/Thermal hardeners",
                    "Target Painting III — for Target Painter II",
                    "Missile Bombardment IV — required for Missile Guidance Computer II",
                    "Drones III — improves drone range and damage",
                    "Light Drone Operation I — to deploy Warrior I (T1 light drones)",
                    "Drone Avionics III — for Drone Link Augmentor I",
                },
                HighSlots = new[]
                {
                    "Cruise Missile Launcher II [Scourge Cruise Missile]",
                    "Cruise Missile Launcher II [Scourge Cruise Missile]",
                    "Cruise Missile Launcher II [Scourge Cruise Missile]",
                    "Cruise Missile Launcher II [Scourge Cruise Missile]",
                    "Bastion Module I",
                    "Small Tractor Beam II",
                    "Drone Link Augmentor I",
                    // 8th high slot empty — Golem only has 4 launcher hardpoints
                },
                MidSlots = new[]
                {
                    "X-Large Ancillary Shield Booster [Navy Cap Booster 400]",
                    "Kinetic Shield Amplifier II",
                    "Thermal Shield Amplifier II",
                    "Shield Boost Amplifier II",
                    "Missile Guidance Computer II",
                    "Cap Recharger II",
                    "Target Painter II",
                },
                LowSlots = new[]
                {
                    "Ballistic Control System II",
                    "Ballistic Control System II",
                    "Ballistic Control System II",
                    "Damage Control II",
                },
                RigSlots = new[]
                {
                    "Large Core Defense Field Extender I",
                    "Large Warhead Calefaction Catalyst I",
                },
                Notes = "Pyfa-verified (All V skills): 1231 weapon DPS (Scourge CM) + ~25 drone DPS = ~1256 DPS total. Volley: 5,361. 95.2k omni-EHP / 38.7k shield EHP (37.6% EM / 80.1% Kin / 82.6% Therm / 68.8% Expl). XLASB active regen 2162.7 EHP/s. Cap stable 93.8%. CPU 825.7/893.8 tf. Bastion doubles tank/range while stationary. Load Navy Cap Booster 400 in XLASB. Golem has 25 Mbit — 5× Warrior I. Top L4 ISK/hr ship.",
                IskEstimate = "~950M–1.2B ISK fitted",
            },

            ["Tengu"] = new()
            {
                Hull = "Tengu", ShipClass = "Strategic Cruiser T3 (subsystem-dependent)", Role = "L4 Speed Runner",
                RequiredSkills = new[]
                {
                    "Caldari Strategic Cruiser I — to fly",
                    "Caldari Defensive Systems I — defensive subsystem skill",
                    "Caldari Offensive Systems I — offensive subsystem skill",
                    "Caldari Propulsion Systems I — propulsion subsystem skill",
                    "Caldari Electronic Systems I — electronic subsystem skill",
                    "Caldari Engineering Systems I — engineering subsystem skill",
                    "Heavy Missiles V — for T2 launchers",
                    "Missile Launcher Operation V",
                    "Shield Operation IV",
                    "Shield Upgrades IV — for hardeners",
                },
                HighSlots = new[]
                {
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                },
                MidSlots = new[]
                {
                    "Large Shield Extender II",
                    "Large Shield Extender II",
                    "Kinetic Shield Amplifier II",
                    "Thermal Shield Amplifier II",
                    "10MN Afterburner II",
                },
                LowSlots = new[]
                {
                    "Ballistic Control System II",
                    "Ballistic Control System II",
                    "Damage Control II",
                    "Power Diagnostic System II",
                },
                RigSlots = new[]
                {
                    "Medium Core Defense Field Extender I",
                    "Medium Kinetic Shield Reinforcer I",
                    "Medium Thermal Shield Reinforcer I",
                },
                Notes = "Slots vary by subsystem — shown is a typical 5H 5M 4L 3R Accelerated Ejection Bay combat config. Tengu has 0 drone bandwidth in missile config. Extremely fast L4 clears. Loses 1 skill level in subsystem skill on destruction.",
                IskEstimate = "~800M–1.5B ISK fitted",
            },

            // ── GALLENTE ─────────────────────────────────────────────────────────
            // Slot reference (EVE Uni Wiki):
            //   Tristan:   3H 3M 3L 3R | 2 turrets 0 launchers | 25 Mbit 40m³
            //   Vexor:     4H 4M 5L 3R | 4 turrets 0 launchers | 75 Mbit 125m³
            //   Myrmidon:  5H 5M 6L 3R | 5 turrets 0 launchers | 100 Mbit 200m³
            //   Ishtar:    4H 4M 6L 2R | 4 turrets 0 launchers | 125 Mbit 375m³
            //   Dominix:   6H 5M 7L 3R | 6 turrets 0 launchers | 125 Mbit 375m³
            //   Kronos:    7H 5M 7L 2R | 4 turrets 0 launchers | 50 Mbit 125m³

            ["Tristan"] = new()
            {
                Hull = "Tristan", ShipClass = "Frigate (3H 3M 3L 3R)", Role = "L1 Mission Runner",
                RequiredSkills = new[]
                {
                    "Gallente Frigate I — to fly",
                    "Small Hybrid Turret I — for 75mm Railgun I",
                    "Drones III — to use light drones (25 Mbit = 5 light drones)",
                    "Light Drone Operation I — for Hobgoblin I / Warrior I",
                },
                HighSlots = new[]
                {
                    "75mm Railgun I",
                    "75mm Railgun I",
                    "— (empty)",
                },
                MidSlots = new[]
                {
                    "1MN Afterburner I",
                    "Small Shield Extender I",
                    "— (empty)",
                },
                LowSlots = new[]
                {
                    "Damage Control I",
                    "Small Armor Repairer I",
                    "— (empty)",
                },
                RigSlots = new[]
                {
                    "Small Anti-Thermal Pump I",
                    "Small Anti-Kinetic Pump I",
                    "— (empty)",
                },
                Notes = "Tristan has 2 turret hardpoints filling both gun slots. 25 Mbit drone bandwidth = 5 light drones (Hobgoblin I or Warrior I). Very forgiving L1 ship — let drones do the work. Mixed tank shown; swap rigs to match enemy damage type.",
                IskEstimate = "~1–3M ISK fitted",
            },

            ["Vexor"] = new()
            {
                Hull = "Vexor", ShipClass = "Cruiser (4H 4M 5L 3R)", Role = "L2–L3 Mission Runner",
                RequiredSkills = new[]
                {
                    "Gallente Cruiser III — to fly",
                    "Drones V — for 5× heavy drones (75 Mbit = 5 medium or 7 light)",
                    "Medium Drone Operation III — for Hammerhead II",
                    "Drone Interfacing I — increases drone damage",
                    "Repair Systems III — to use Medium Armor Repairer II",
                    "Hull Upgrades IV — to fit Kinetic/Thermal Energized Membranes",
                    "EM Armor Compensation II — for Kinetic Energized Membrane II bonus",
                    "Thermal Armor Compensation II — for Thermal Energized Membrane II bonus",
                },
                HighSlots = new[]
                {
                    "250mm Railgun I",
                    "250mm Railgun I",
                    "— (empty)",
                    "— (empty)",
                },
                MidSlots = new[]
                {
                    "10MN Afterburner II",
                    "Cap Recharger II",
                    "— (empty)",
                    "— (empty)",
                },
                LowSlots = new[]
                {
                    "Medium Armor Repairer II",
                    "Kinetic Energized Membrane II",
                    "Thermal Energized Membrane II",
                    "Damage Control II",
                    "Drone Damage Amplifier II",
                },
                RigSlots = new[]
                {
                    "Medium Auxiliary Nano Pump I",
                    "Medium Nanobot Accelerator I",
                    "Medium Anti-Explosive Pump I",
                },
                Notes = "Vexor has 4 turret hardpoints but only 4 high slots total — only 2 turrets shown to leave utility slots empty. The real DPS comes from 5× Hammerhead II drones (75 Mbit = 5 mediums). Armor tank against Serpentis (Kinetic/Thermal). Excellent value ship.",
                IskEstimate = "~30–50M ISK fitted",
            },

            ["Myrmidon"] = new()
            {
                Hull = "Myrmidon", ShipClass = "Battlecruiser (5H 5M 6L 3R)", Role = "L3 Mission Runner",
                RequiredSkills = new[]
                {
                    "Gallente Battlecruiser III — to fly",
                    "Drones V — 100 Mbit = 5 heavy or 10 medium drones",
                    "Heavy Drone Operation III — for Ogre II",
                    "Drone Interfacing II — increases drone DPS",
                    "Repair Systems IV — to use Large Armor Repairer II",
                    "Hull Upgrades IV — for Energized Membranes",
                    "EM Armor Compensation III — for Kinetic Energized Membrane II bonus",
                    "Thermal Armor Compensation III — for Thermal Energized Membrane II bonus",
                },
                HighSlots = new[]
                {
                    "250mm Railgun II [Antimatter Charge M]",
                    "250mm Railgun II [Antimatter Charge M]",
                    "250mm Railgun II [Antimatter Charge M]",
                    "— (empty)",
                    "— (empty)",
                },
                MidSlots = new[]
                {
                    "10MN Afterburner II",
                    "Cap Recharger II",
                    "Cap Recharger II",
                    "— (empty)",
                    "— (empty)",
                },
                LowSlots = new[]
                {
                    "Large Armor Repairer II",
                    "Kinetic Energized Membrane II",
                    "Thermal Energized Membrane II",
                    "Damage Control II",
                    "Drone Damage Amplifier II",
                    "Drone Damage Amplifier II",
                },
                RigSlots = new[]
                {
                    "Medium Auxiliary Nano Pump I",
                    "Medium Nanobot Accelerator I",
                    "Medium Anti-Explosive Pump I",
                },
                Notes = "Myrmidon has 5 turret hardpoints but only 5 high slots. Shown with 3 railguns to save powergrid and leave 2 empty highs. Main DPS from drones — 100 Mbit = 5× Ogre II (heavy) or 10× Hammerhead II (medium). Armor tank — remember to run rep cycle. Cap-hungry: run 2 cap rechargers.",
                IskEstimate = "~80–130M ISK fitted",
            },

            ["Ishtar"] = new()
            {
                Hull = "Ishtar", ShipClass = "Heavy Assault Cruiser T2 (4H 4M 6L 2R)", Role = "L4 HAC",
                RequiredSkills = new[]
                {
                    "Gallente Cruiser V — prerequisite",
                    "Heavy Assault Cruisers I — to fly",
                    "Drones V — 125 Mbit = 5× sentry or 5 heavy drones",
                    "Heavy Drone Operation IV — for Ogre II",
                    "Sentry Drone Interfacing III — for Garde II",
                    "Drone Interfacing III — increases drone DPS",
                    "Repair Systems IV — for Large Armor Repairer II",
                    "Hull Upgrades IV — for Energized Membranes",
                    "Explosive Armor Compensation III — for Explosive Energized Membrane II",
                    "Thermal Armor Compensation III — for Thermal Energized Membrane II",
                    "Assault Damage Control I — to fit ADC module",
                },
                HighSlots = new[]
                {
                    "250mm Railgun II [Antimatter Charge M]",
                    "250mm Railgun II [Antimatter Charge M]",
                    "— (empty)",
                    "— (empty)",
                },
                MidSlots = new[]
                {
                    "10MN Afterburner II",
                    "Cap Recharger II",
                    "Cap Recharger II",
                    "— (empty)",
                },
                LowSlots = new[]
                {
                    "Large Armor Repairer II",
                    "Explosive Energized Membrane II",
                    "Thermal Energized Membrane II",
                    "Damage Control II",
                    "Assault Damage Control I",
                    "Drone Damage Amplifier II",
                },
                RigSlots = new[]
                {
                    "Medium Auxiliary Nano Pump I",
                    "Medium Nanobot Accelerator I",
                },
                Notes = "Ishtar has 4H 4M 6L 2R with only 4 turret hardpoints and 2 rig slots. Deploy 5× Garde II sentry drones at 50km and orbit at 40km. 125 Mbit = 5 sentries, heavies, or mediums. ADC gives 30s emergency burst. Effectively AFK for many missions. 900+ DPS with good drone skills.",
                IskEstimate = "~350–600M ISK fitted",
            },

            ["Dominix"] = new()
            {
                Hull = "Dominix", ShipClass = "Battleship (6H 5M 7L 3R)", Role = "L4 Mission Runner",
                RequiredSkills = new[]
                {
                    "Gallente Battleship III — to fly",
                    "Drones V — 125 Mbit = 5 heavy/sentry drones",
                    "Heavy Drone Operation IV — for Ogre II",
                    "Sentry Drone Interfacing III — for Garde II",
                    "Drone Interfacing III — DPS bonus",
                    "Repair Systems V — for Large Armor Repairer II",
                    "Hull Upgrades IV — for Energized Membranes",
                    "Explosive Armor Compensation III",
                    "Thermal Armor Compensation III",
                    "Long Range Targeting IV — for Omnidirectional Tracking Link II",
                    "Drone Avionics III — for Drone Link Augmentor II",
                },
                HighSlots = new[]
                {
                    "Electron Blaster Cannon II [Void L] (or 425mm Railgun II [Spike L] for range)",
                    "Electron Blaster Cannon II [Void L]",
                    "Electron Blaster Cannon II [Void L]",
                    "Drone Link Augmentor II",
                    "Small Tractor Beam II",
                    "— (empty)",
                },
                MidSlots = new[]
                {
                    "10MN Afterburner II",
                    "Omnidirectional Tracking Link II",
                    "Cap Recharger II",
                    "Cap Recharger II",
                    "— (empty)",
                },
                LowSlots = new[]
                {
                    "Large Armor Repairer II",
                    "Explosive Energized Membrane II",
                    "Thermal Energized Membrane II",
                    "Damage Control II",
                    "Drone Damage Amplifier II",
                    "Drone Damage Amplifier II",
                    "— (empty)",
                },
                RigSlots = new[]
                {
                    "Large Auxiliary Nano Pump I",
                    "Large Nanobot Accelerator I",
                    "Large Anti-Explosive Pump I",
                },
                Notes = "Dominix has 6H 5M 7L 3R with 6 turret hardpoints (no launcher HP). 3 blasters + DLA + Tractor Beam fill 5 highs, 1 empty. 125 Mbit = 5× Garde II sentries. Omnidirectional Tracking Link improves sentry optimal. Budget L4 option vs Kronos/Ishtar.",
                IskEstimate = "~200–350M ISK fitted",
            },

            ["Kronos"] = new()
            {
                Hull = "Kronos", ShipClass = "Marauder T2 (7H 5M 7L 2R)", Role = "L4 Speed Runner",
                RequiredSkills = new[]
                {
                    "Gallente Battleship V — prerequisite",
                    "Marauders I — to fly (IV for full Bastion bonus)",
                    "Large Hybrid Turret V — for Neutron Blaster Cannon II",
                    "Gunnery V",
                    "Motion Prediction IV — tracking",
                    "Sharpshooter IV — optimal range",
                    "Repair Systems V — for Large Armor Repairer II",
                    "Hull Upgrades IV — for Energized Membranes",
                    "Tactical Weapon Reconfiguration I — to fit Bastion Module I",
                    "Drone Avionics III — for Drone Link Augmentor II",
                    "Drones III — 50 Mbit = 5 medium drones",
                    "Medium Drone Operation II — for Hammerhead II",
                },
                HighSlots = new[]
                {
                    "Neutron Blaster Cannon II [Void L]",
                    "Neutron Blaster Cannon II [Void L]",
                    "Neutron Blaster Cannon II [Void L]",
                    "Neutron Blaster Cannon II [Void L]",
                    "Bastion Module I",
                    "Drone Link Augmentor II",
                    "Small Tractor Beam II",
                },
                MidSlots = new[]
                {
                    "Cap Recharger II",
                    "Tracking Computer II",
                    "Omnidirectional Tracking Link II",
                    "10MN Afterburner II",
                    "Cap Recharger II",
                },
                LowSlots = new[]
                {
                    "Large Armor Repairer II",
                    "Explosive Energized Membrane II",
                    "Thermal Energized Membrane II",
                    "Magnetic Field Stabilizer II",
                    "Magnetic Field Stabilizer II",
                    "Damage Control II",
                    "— (empty)",
                },
                RigSlots = new[]
                {
                    "Large Auxiliary Nano Pump I",
                    "Large Hybrid Burst Aerator I",
                },
                Notes = "Kronos has 7H 5M 7L 2R and only 4 turret hardpoints (not 6). 4 Neutron Blasters + Bastion + DLA + Tractor = 7 highs filled. Only 2 rig slots. 50 Mbit = 5× Hammerhead II. Use Void ammo close range, Null for kiting. 1 100+ DPS in Bastion.",
                IskEstimate = "~3–5B ISK fitted",
            },

            // ── AMARR ────────────────────────────────────────────────────────────
            // Slot reference (EVE Uni Wiki):
            //   Omen:      5H 3M 6L 3R | 5 turrets 0 launchers | 40 Mbit 40m³
            //   Harbinger: 7H 4M 6L 3R | 6 turrets 0 launchers | 50 Mbit 75m³
            //   Abaddon:   8H 4M 7L 3R | 8 turrets 1 launcher  | 75 Mbit 75m³
            //   Paladin:   8H 4M 7L 2R | 4 turrets 0 launchers | 25 Mbit 75m³

            ["Omen"] = new()
            {
                Hull = "Omen", ShipClass = "Cruiser (5H 3M 6L 3R)", Role = "L2 Mission Runner",
                RequiredSkills = new[]
                {
                    "Amarr Cruiser III — to fly",
                    "Medium Energy Turret IV — for Dual Medium Pulse Laser II",
                    "Gunnery IV",
                    "Controlled Bursts III — reduces laser cap use",
                    "Energy Management III — for cap stability",
                    "Repair Systems III — for Medium Armor Repairer II",
                    "Hull Upgrades III — for armor mods",
                    "Drones III — 40 Mbit = 4 light drones",
                    "Light Drone Operation II — for Hobgoblin II",
                },
                HighSlots = new[]
                {
                    "Dual Medium Pulse Laser II [Imperial Navy Multifrequency M]",
                    "Dual Medium Pulse Laser II [Imperial Navy Multifrequency M]",
                    "Dual Medium Pulse Laser II [Imperial Navy Multifrequency M]",
                    "Dual Medium Pulse Laser II [Imperial Navy Multifrequency M]",
                    "Dual Medium Pulse Laser II [Imperial Navy Multifrequency M]",
                },
                MidSlots = new[]
                {
                    "10MN Afterburner II",
                    "Cap Recharger II",
                    "Cap Recharger II",
                },
                LowSlots = new[]
                {
                    "Medium Armor Repairer II",
                    "Heat Sink II",
                    "Heat Sink II",
                    "Damage Control II",
                    "EM Energized Membrane II",
                    "Thermal Energized Membrane II",
                },
                RigSlots = new[]
                {
                    "Medium Auxiliary Nano Pump I",
                    "Medium Energy Locus Coordinator I",
                    "Medium Anti-Explosive Pump I",
                },
                Notes = "Omen has only 3 mid slots — no room for tank modules there. Full defense goes in 6 lows. 5 turret hardpoints fills all 5 highs. 40 Mbit = 4 light drones. Lasers use Scorch (long range) or Multifrequency (close, max DPS) crystals — no ammo to reload. Cap-hungry: run 2 cap rechargers.",
                IskEstimate = "~30–50M ISK fitted",
            },

            ["Harbinger"] = new()
            {
                Hull = "Harbinger", ShipClass = "Battlecruiser (7H 4M 6L 3R)", Role = "L3 Mission Runner",
                RequiredSkills = new[]
                {
                    "Amarr Battlecruiser III — to fly",
                    "Medium Energy Turret V — for Heavy Pulse Laser II",
                    "Gunnery V",
                    "Controlled Bursts IV — reduces laser cap use",
                    "Energy Management IV — for cap stability",
                    "Repair Systems IV — for Large Armor Repairer II",
                    "Hull Upgrades IV — for Energized Membranes",
                    "EM Armor Compensation III — for EM Energized Membrane II",
                    "Thermal Armor Compensation III — for Thermal Energized Membrane II",
                    "Drones III — 50 Mbit = 5 light drones",
                    "Light Drone Operation II — for Hobgoblin II",
                },
                HighSlots = new[]
                {
                    "Heavy Pulse Laser II [Imperial Navy Multifrequency M]",
                    "Heavy Pulse Laser II [Imperial Navy Multifrequency M]",
                    "Heavy Pulse Laser II [Imperial Navy Multifrequency M]",
                    "Heavy Pulse Laser II [Imperial Navy Multifrequency M]",
                    "Heavy Pulse Laser II [Imperial Navy Multifrequency M]",
                    "Heavy Pulse Laser II [Imperial Navy Multifrequency M]",
                    "Small Energy Nosferatu II",
                },
                MidSlots = new[]
                {
                    "10MN Afterburner II",
                    "Cap Recharger II",
                    "Cap Recharger II",
                    "Cap Recharger II",
                },
                LowSlots = new[]
                {
                    "Large Armor Repairer II",
                    "Heat Sink II",
                    "Heat Sink II",
                    "EM Energized Membrane II",
                    "Thermal Energized Membrane II",
                    "Damage Control II",
                },
                RigSlots = new[]
                {
                    "Medium Auxiliary Nano Pump I",
                    "Medium Energy Locus Coordinator I",
                    "Medium Anti-Explosive Pump I",
                },
                Notes = "Harbinger has only 4 mid slots — all used for cap management. 6 Heavy Pulse Lasers + Small Energy Nosferatu II fill 7 highs. Use Scorch crystals for 40km range without reloading. Nos on first enemy helps cap stability. 50 Mbit = 5 light drones.",
                IskEstimate = "~80–130M ISK fitted",
            },

            ["Paladin"] = new()
            {
                Hull = "Paladin", ShipClass = "Marauder T2 (8H 4M 7L 2R)", Role = "L4 Speed Runner",
                RequiredSkills = new[]
                {
                    "Amarr Battleship V — prerequisite",
                    "Marauders I — to fly (IV for full Bastion bonus)",
                    "Large Energy Turret V — for Mega Pulse Laser II",
                    "Gunnery V",
                    "Controlled Bursts V — cap efficiency",
                    "Energy Management V — cap stability",
                    "Repair Systems V — for Large Armor Repairer II",
                    "Hull Upgrades V — for T2 membranes",
                    "EM Armor Compensation IV — for EM Energized Membrane II",
                    "Thermal Armor Compensation IV — for Thermal Energized Membrane II",
                    "Tactical Weapon Reconfiguration I — to fit Bastion Module I",
                    "Drones III — 25 Mbit = 5 light drones",
                    "Drone Avionics III — for Drone Link Augmentor I",
                },
                HighSlots = new[]
                {
                    "Mega Pulse Laser II [Imperial Navy Multifrequency L]",
                    "Mega Pulse Laser II [Imperial Navy Multifrequency L]",
                    "Mega Pulse Laser II [Imperial Navy Multifrequency L]",
                    "Mega Pulse Laser II [Imperial Navy Multifrequency L]",
                    "Bastion Module I",
                    "Drone Link Augmentor I",
                    "Small Tractor Beam II",
                    "— (empty)",
                },
                MidSlots = new[]
                {
                    "Cap Recharger II",
                    "Cap Recharger II",
                    "Tracking Computer II",
                    "— (empty)",
                },
                LowSlots = new[]
                {
                    "Large Armor Repairer II",
                    "EM Energized Membrane II",
                    "Thermal Energized Membrane II",
                    "Heat Sink II",
                    "Heat Sink II",
                    "Heat Sink II",
                    "Damage Control II",
                },
                RigSlots = new[]
                {
                    "Large Auxiliary Nano Pump I",
                    "Large Energy Locus Coordinator I",
                },
                Notes = "Paladin has 8H 4M 7L 2R with only 4 turret hardpoints and 2 rig slots. 4 Mega Pulse Lasers + Bastion + DLA + Tractor + 1 empty = 8 highs. 4 mids for cap management only. Use Scorch crystals for 80km+ range in Bastion. 25 Mbit = 5 light drones. 900–1 200 DPS.",
                IskEstimate = "~4–6B ISK fitted",
            },

            ["Abaddon"] = new()
            {
                Hull = "Abaddon", ShipClass = "Battleship (8H 4M 7L 3R)", Role = "L4 Mission Runner",
                RequiredSkills = new[]
                {
                    "Amarr Battleship III — to fly",
                    "Large Energy Turret V — for Mega Pulse Laser II",
                    "Gunnery V",
                    "Controlled Bursts IV — cap management",
                    "Energy Management V — cap stability",
                    "Repair Systems IV — for Large Armor Repairer II",
                    "Hull Upgrades IV — for Energized Membranes",
                    "EM Armor Compensation III",
                    "Thermal Armor Compensation III",
                    "Drones III — 75 Mbit = 5 medium drones",
                    "Medium Drone Operation II — for Hammerhead II",
                },
                HighSlots = new[]
                {
                    "Mega Pulse Laser II [Imperial Navy Multifrequency L]",
                    "Mega Pulse Laser II [Imperial Navy Multifrequency L]",
                    "Mega Pulse Laser II [Imperial Navy Multifrequency L]",
                    "Mega Pulse Laser II [Imperial Navy Multifrequency L]",
                    "Mega Pulse Laser II [Imperial Navy Multifrequency L]",
                    "Mega Pulse Laser II [Imperial Navy Multifrequency L]",
                    "Mega Pulse Laser II [Imperial Navy Multifrequency L]",
                    "Mega Pulse Laser II [Imperial Navy Multifrequency L]",
                },
                MidSlots = new[]
                {
                    "Cap Recharger II",
                    "Cap Recharger II",
                    "Cap Recharger II",
                    "Tracking Computer II",
                },
                LowSlots = new[]
                {
                    "Large Armor Repairer II",
                    "EM Energized Membrane II",
                    "Thermal Energized Membrane II",
                    "Heat Sink II",
                    "Heat Sink II",
                    "Heat Sink II",
                    "Damage Control II",
                },
                RigSlots = new[]
                {
                    "Large Auxiliary Nano Pump I",
                    "Large Energy Locus Coordinator I",
                    "Large Anti-Explosive Pump I",
                },
                Notes = "Abaddon has 8 turret hardpoints filling all 8 high slots. Only 4 mid slots — cap management only, no active shield. Massive armor tank and DPS (800–900). Very cap-hungry: run 3 cap rechargers and manage carefully. Drones: 5× Hammerhead II (75 Mbit).",
                IskEstimate = "~350–600M ISK fitted",
            },

            // ── MINMATAR ─────────────────────────────────────────────────────────
            // Slot reference (EVE Uni Wiki):
            //   Rupture:    5H 4M 5L 3R | 4 turrets 1 launcher | 40 Mbit 40m³
            //   Hurricane:  7H 4M 6L 3R | 6 turrets 3 launchers | 40 Mbit 40m³
            //   Machariel:  8H 6M 6L 3R | 7 turrets 0 launchers | 100 Mbit 125m³
            //   Vargur:     7H 6M 6L 2R | 4 turrets 0 launchers | 50 Mbit 75m³

            ["Rupture"] = new()
            {
                Hull = "Rupture", ShipClass = "Cruiser (5H 4M 5L 3R)", Role = "L2 Mission Runner",
                RequiredSkills = new[]
                {
                    "Minmatar Cruiser III — to fly",
                    "Medium Projectile Turret IV — for 220mm Vulcan AutoCannon II",
                    "Gunnery IV",
                    "Motion Prediction III — tracking",
                    "Repair Systems III — for Medium Armor Repairer II",
                    "Drones III — 40 Mbit = 4 light drones",
                    "Light Drone Operation I — for Warrior I",
                },
                HighSlots = new[]
                {
                    "220mm Vulcan AutoCannon II [Republic Fleet EMP M]",
                    "220mm Vulcan AutoCannon II [Republic Fleet EMP M]",
                    "220mm Vulcan AutoCannon II [Republic Fleet EMP M]",
                    "220mm Vulcan AutoCannon II [Republic Fleet EMP M]",
                    "— (empty — 1 launcher hardpoint available for utility)",
                },
                MidSlots = new[]
                {
                    "10MN Afterburner II",
                    "Cap Recharger II",
                    "Medium Shield Extender I",
                    "— (empty)",
                },
                LowSlots = new[]
                {
                    "Medium Armor Repairer II",
                    "Gyrostabilizer II",
                    "Gyrostabilizer II",
                    "Damage Control II",
                    "Tracking Enhancer II",
                },
                RigSlots = new[]
                {
                    "Medium Projectile Burst Aerator I",
                    "Medium Auxiliary Nano Pump I",
                    "— (empty)",
                },
                Notes = "Rupture has 4 turret hardpoints + 1 launcher hardpoint. 4 autocannons fill turret slots. 40 Mbit = 4 light drones. Use Republic Fleet EMP (EM) against Angels; RF Phased Plasma (Thermal) against Sansha. No cap cost on ACs = great sustained DPS.",
                IskEstimate = "~30–50M ISK fitted",
            },

            ["Hurricane"] = new()
            {
                Hull = "Hurricane", ShipClass = "Battlecruiser (7H 4M 6L 3R)", Role = "L3 Mission Runner",
                RequiredSkills = new[]
                {
                    "Minmatar Battlecruiser III — to fly",
                    "Medium Projectile Turret V — for 425mm AutoCannon II",
                    "Gunnery V",
                    "Motion Prediction IV — tracking",
                    "Repair Systems IV — for Large Armor Repairer II",
                    "Hull Upgrades IV — for Energized Membranes",
                    "Explosive Armor Compensation III — for Explosive Energized Membrane II",
                    "Kinetic Armor Compensation III — for Kinetic Energized Membrane II",
                    "Drones III — 40 Mbit = 4 light drones",
                    "Light Drone Operation V — required for Warrior II (also needs Drones V + Minmatar Drone Spec I)",
                },
                HighSlots = new[]
                {
                    "425mm AutoCannon II [Republic Fleet EMP M]",
                    "425mm AutoCannon II [Republic Fleet EMP M]",
                    "425mm AutoCannon II [Republic Fleet EMP M]",
                    "425mm AutoCannon II [Republic Fleet EMP M]",
                    "425mm AutoCannon II [Republic Fleet EMP M]",
                    "425mm AutoCannon II [Republic Fleet EMP M]",
                    "— (empty — 1 extra high available for utility)",
                },
                MidSlots = new[]
                {
                    "10MN Afterburner II",
                    "Cap Recharger II",
                    "Cap Recharger II",
                    "— (empty)",
                },
                LowSlots = new[]
                {
                    "Large Armor Repairer II",
                    "Gyrostabilizer II",
                    "Gyrostabilizer II",
                    "Explosive Energized Membrane II",
                    "Kinetic Energized Membrane II",
                    "Damage Control II",
                },
                RigSlots = new[]
                {
                    "Medium Projectile Burst Aerator I",
                    "Medium Auxiliary Nano Pump I",
                    "Medium Nanobot Accelerator I",
                },
                Notes = "Hurricane has 6 turret hardpoints + 3 launcher hardpoints, 7 high slots total. 6 ACs + 1 empty utility high. Only 4 mid slots. Armor tank variant shown — Explosive/Kinetic membranes for Angel Cartel. No cap cost on ACs. 40 Mbit = 4 light drones.",
                IskEstimate = "~80–130M ISK fitted",
            },

            ["Machariel"] = new()
            {
                Hull = "Machariel", ShipClass = "Faction Battleship (8H 6M 6L 3R)", Role = "L4 Speed Runner",
                RequiredSkills = new[]
                {
                    "Minmatar Battleship III AND Gallente Battleship III — both required to fly",
                    "Large Projectile Turret V — for 800mm Repeating Cannon II",
                    "Gunnery V",
                    "Motion Prediction IV — tracking",
                    "Navigation IV — speed bonus",
                    "Shield Operation IV",
                    "Shield Upgrades IV — for X-Large Shield Booster II",
                    "Energy Management IV — for shield booster cap",
                    "Electronics Upgrades IV — for Shield Boost Amplifier II",
                    "Drones IV — 100 Mbit = 5 medium drones",
                    "Medium Drone Operation III — for Hammerhead II",
                },
                HighSlots = new[]
                {
                    "800mm Repeating Cannon II [Republic Fleet EMP L]",
                    "800mm Repeating Cannon II [Republic Fleet EMP L]",
                    "800mm Repeating Cannon II [Republic Fleet EMP L]",
                    "800mm Repeating Cannon II [Republic Fleet EMP L]",
                    "800mm Repeating Cannon II [Republic Fleet EMP L]",
                    "800mm Repeating Cannon II [Republic Fleet EMP L]",
                    "800mm Repeating Cannon II [Republic Fleet EMP L]",
                    "— (empty)",
                },
                MidSlots = new[]
                {
                    "X-Large Shield Booster II",
                    "Explosive Shield Amplifier II",
                    "Kinetic Shield Amplifier II",
                    "Shield Boost Amplifier II",
                    "10MN Afterburner II",
                    "Tracking Computer II",
                },
                LowSlots = new[]
                {
                    "Gyrostabilizer II",
                    "Gyrostabilizer II",
                    "Gyrostabilizer II",
                    "Tracking Enhancer II",
                    "Damage Control II",
                    "— (empty)",
                },
                RigSlots = new[]
                {
                    "Large Core Defense Field Extender I",
                    "Large Explosive Shield Reinforcer I",
                    "Large Projectile Burst Aerator I",
                },
                Notes = "Machariel has 7 turret hardpoints and 8 high slots — 7 ACs + 1 empty utility. Full 6 mids used. 100 Mbit = 5× Hammerhead II. Fastest battleship in EVE (~650 m/s). Speed-tank playstyle. 1 000+ DPS with autocannons.",
                IskEstimate = "~1–2B ISK fitted",
            },

            ["Vargur"] = new()
            {
                Hull = "Vargur", ShipClass = "Marauder T2 (7H 6M 6L 2R)", Role = "L4 Speed Runner",
                RequiredSkills = new[]
                {
                    "Minmatar Battleship V — prerequisite",
                    "Marauders I — to fly (IV for full Bastion bonus)",
                    "Large Projectile Turret V — for 800mm Repeating Cannon II",
                    "Gunnery V",
                    "Motion Prediction IV",
                    "Tactical Weapon Reconfiguration I — to fit Bastion Module I",
                    "Shield Operation IV",
                    "Shield Upgrades IV — for hardeners",
                    "Electronics Upgrades IV — for Shield Boost Amplifier II",
                    "Drones III — 50 Mbit = 5 light drones",
                    "Drone Avionics III — for Drone Link Augmentor I",
                },
                HighSlots = new[]
                {
                    "800mm Repeating Cannon II [Republic Fleet EMP L]",
                    "800mm Repeating Cannon II [Republic Fleet EMP L]",
                    "800mm Repeating Cannon II [Republic Fleet EMP L]",
                    "800mm Repeating Cannon II [Republic Fleet EMP L]",
                    "Bastion Module I",
                    "Drone Link Augmentor I",
                    "Small Tractor Beam II",
                },
                MidSlots = new[]
                {
                    "X-Large Ancillary Shield Booster [Navy Cap Booster 400]",
                    "Explosive Shield Amplifier II",
                    "Kinetic Shield Amplifier II",
                    "Shield Boost Amplifier II",
                    "Tracking Computer II",
                    "Cap Recharger II",
                },
                LowSlots = new[]
                {
                    "Gyrostabilizer II",
                    "Gyrostabilizer II",
                    "Gyrostabilizer II",
                    "Tracking Enhancer II",
                    "Damage Control II",
                    "— (empty)",
                },
                RigSlots = new[]
                {
                    "Large Core Defense Field Extender I",
                    "Large Explosive Shield Reinforcer I",
                },
                Notes = "Vargur has 4 turret hardpoints, 7 high slots, 6 mid slots, 6 low slots, 2 rig slots. 4 ACs + Bastion + DLA + Tractor = 7 highs filled. No cap cost on ACs = Bastion runs indefinitely. 50 Mbit = 5 light drones. 1 000–1 200 DPS.",
                IskEstimate = "~3–5B ISK fitted",
            },

            // ── MINING ───────────────────────────────────────────────────────────
            // Slot reference (EVE Uni Wiki):
            //   Venture:   3H 3M 1L 3R | 0 turrets 0 launchers | 10 Mbit 10m³

            ["Venture"] = new()
            {
                Hull = "Venture", ShipClass = "Mining Frigate (3H 3M 1L 3R)", Role = "Ore Mining",
                RequiredSkills = new[]
                {
                    "Mining Frigate I — to fly",
                    "Mining I — to use Miner I / II",
                },
                HighSlots = new[]
                {
                    "Miner II",
                    "Miner II",
                    "— (empty)",
                },
                MidSlots = new[]
                {
                    "1MN Afterburner I",
                    "Survey Scanner I",
                    "Warp Core Stabilizer I",
                },
                LowSlots = new[]
                {
                    "Mining Laser Upgrade I",
                },
                RigSlots = new[]
                {
                    "Small Mining Laser Augmentor I",
                    "Small Mining Laser Augmentor I",
                    "— (empty)",
                },
                Notes = "Venture has 3H 3M 1L 3R and only 1 low slot. 5 000 m³ ore bay. Warp Core Stabilizer in mid for dangerous space. Survey Scanner shows remaining ore in each asteroid. Very limited fitting — prioritise mining yield.",
                IskEstimate = "~5–10M ISK fitted",
            },

            ["Retriever"] = new()
            {
                Hull = "Retriever", ShipClass = "Mining Barge", Role = "Ore Mining (Solo)",
                RequiredSkills = new[]
                {
                    "Mining Barge I — to fly",
                    "Astrogeology III — prerequisite for Mining Barge",
                    "Mining IV — for Strip Miner I efficiency",
                },
                HighSlots = new[]
                {
                    "Strip Miner I",
                    "Strip Miner I",
                },
                MidSlots = new[]
                {
                    "Survey Scanner II",
                },
                LowSlots = new[]
                {
                    "Mining Laser Upgrade II",
                    "Mining Laser Upgrade II",
                },
                RigSlots = new[]
                {
                    "Medium Mining Laser Augmentor II",
                    "Medium Mining Laser Augmentor II",
                    "— (empty)",
                },
                Notes = "Largest ore bay of the T1 barges — ideal for AFK solo mining. Low tank — avoid low-sec. Upgrade to Mackinaw for higher yield or Skiff for survivability in risky space.",
                IskEstimate = "~50–80M ISK fitted",
            },

            // ── GENERIC / FACTION ────────────────────────────────────────────────

            ["Any T1 Frigate"] = new()
            {
                Hull = "Any T1 Frigate", ShipClass = "Frigate", Role = "L1 Combat",
                RequiredSkills = new[]{ "Relevant racial Frigate I", "Core fitting skills III" },
                HighSlots = new[]{ "2× T1 guns matching your racial type" },
                MidSlots  = new[]{ "1MN Afterburner I", "Small Shield Extender I" },
                LowSlots  = new[]{ "Damage Control I", "Small Armor Repairer I" },
                RigSlots  = new[]{ "Damage rig for your weapon type" },
                Notes     = "Any T1 frigate works for L1 combat. Fit damage and tank matching your NPC enemies. Focus on staying alive — you can clear L1 in almost anything.",
                IskEstimate = "~1–5M ISK fitted",
            },

            ["Gila"] = new()
            {
                Hull = "Gila", ShipClass = "Pirate Faction Cruiser (5H 6M 3L 3R)", Role = "L3–L4 HAC Alternative",
                RequiredSkills = new[]
                {
                    "Caldari Cruiser III AND Gallente Cruiser III — both required to fly",
                    "Drones V — 20 Mbit = 2 medium drones active (but Gila doubles drone damage)",
                    "Medium Drone Operation III — for Hammerhead II",
                    "Drone Interfacing III — DPS bonus",
                    "Shield Operation IV",
                    "Shield Upgrades IV — for Large Shield Extender II",
                },
                HighSlots = new[]
                {
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "Heavy Missile Launcher II [Scourge Heavy Missile]",
                    "— (empty)",
                    "— (empty)",
                    "— (empty)",
                },
                MidSlots = new[]
                {
                    "Large Shield Extender II",
                    "Large Shield Extender II",
                    "Kinetic Shield Amplifier II",
                    "Thermal Shield Amplifier II",
                    "10MN Afterburner II",
                    "Cap Recharger II",
                },
                LowSlots = new[]
                {
                    "Drone Damage Amplifier II",
                    "Drone Damage Amplifier II",
                    "Damage Control II",
                },
                RigSlots = new[]
                {
                    "Medium Core Defense Field Extender I",
                    "Medium Kinetic Shield Reinforcer I",
                    "Medium Thermal Shield Reinforcer I",
                },
                Notes = "Gila has 4 launcher hardpoints but only 3 low slots and 20 Mbit drone bandwidth. IMPORTANT: Gila's role bonus doubles drone damage — 2 active medium drones hit as hard as 4. Use 2× Hammerhead II or Gecko for damage. 500–700 effective DPS. Extremely versatile — swap drone types to match any NPC.",
                IskEstimate = "~600M–1.2B ISK fitted",
            },
        };

        /// <summary>Returns fit data for the given hull name, or null if not catalogued.</summary>
        public static ShipFitData? TryGet(string hullName)
        {
            if (string.IsNullOrWhiteSpace(hullName)) return null;
            return _fits.TryGetValue(hullName.Trim(), out var fit) ? fit : null;
        }
    }
}
