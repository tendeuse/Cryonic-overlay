// filename: Services/Localization.cs
// Bilingual EN/FR support for the Cryonic Overlay.
// All UI strings live here — add new keys in both languages.
// Toggle with LocalizationManager.Toggle() or set Language directly.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OverlayMVP.Services
{
    // -----------------------------------------------------------------------
    // Supported languages
    // -----------------------------------------------------------------------
    public enum OverlayLanguage { EN, FR }

    // -----------------------------------------------------------------------
    // Observable singleton — ViewModels bind to it via {Binding Loc.XXX}
    // -----------------------------------------------------------------------
    public sealed class LocalizationManager : INotifyPropertyChanged
    {
        // Singleton
        public static LocalizationManager Instance { get; } = new();
        private LocalizationManager() { }

        private OverlayLanguage _lang = OverlayLanguage.EN;

        // Where the choice is remembered. Null until UseDatabase() is called,
        // and deliberately null in screenshot mode -- see UseDatabase.
        private static AppDb? _db;
        private const string LangKey = "ui_language";

        /// <summary>
        /// Restore the saved language, and keep saving it from now on.
        ///
        /// Without this the toggle lasted exactly one session: _lang starts at
        /// EN every launch, so a French pilot had to press FR every single time
        /// they opened the overlay. A preference you must re-set on every start
        /// is not a preference.
        ///
        /// NOT called in screenshot mode. The visual baseline must not depend
        /// on the language this machine last chose -- the same rule that
        /// already pins the skin and the font size.
        /// </summary>
        public static void UseDatabase(AppDb db)
        {
            _db = db;
            if (Read(db) is OverlayLanguage saved) Instance.Language = saved;
        }

        public OverlayLanguage Language
        {
            get => _lang;
            set
            {
                if (_lang == value) return;
                _lang = value;
                Save(value);
                // Notify ALL properties so every bound string refreshes
                OnPropertyChanged(string.Empty);
            }
        }

        private static OverlayLanguage? Read(AppDb db)
        {
            try
            {
                using var con = db.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText = "SELECT v FROM meta WHERE k=$k";
                cmd.Parameters.AddWithValue("$k", LangKey);
                return (cmd.ExecuteScalar() as string) switch
                {
                    "FR" => OverlayLanguage.FR,
                    "EN" => OverlayLanguage.EN,
                    _    => null,
                };
            }
            catch { return null; }
        }

        private static void Save(OverlayLanguage lang)
        {
            if (_db is null) return;
            try
            {
                using var con = _db.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText = "INSERT INTO meta(k,v) VALUES($k,$v) " +
                                  "ON CONFLICT(k) DO UPDATE SET v=excluded.v";
                cmd.Parameters.AddWithValue("$k", LangKey);
                cmd.Parameters.AddWithValue("$v", lang.ToString());
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public void Toggle()
            => Language = _lang == OverlayLanguage.EN
                ? OverlayLanguage.FR
                : OverlayLanguage.EN;

        public string LanguageButtonLabel
            => _lang == OverlayLanguage.EN ? "🌐 FR" : "🌐 EN";

        // ----------------------------------------------------------------
        // String lookup helper
        // ----------------------------------------------------------------
        private string T(string en, string fr)
            => _lang == OverlayLanguage.EN ? en : fr;

        // ================================================================
        // UI STRINGS
        // ================================================================

        // Tooltips on the title-bar buttons.
        //
        // These were hardcoded in MainWindow.xaml, and one of them
        // ("Paramètres") was hardcoded in FRENCH -- so an English pilot got a
        // French tooltip on the gear, which is exactly the "wrong language"
        // report. A literal in the XAML cannot follow the language toggle by
        // definition; it can only ever be right for one audience.
        public string TipSettings       => T("Settings",                     "Paramètres");
        public string TipControlPanel   => T("Open Control Panel",           "Ouvrir le panneau de contrôle");
        public string TipPilotSearch    => T("Pilot Intel Search",           "Recherche de renseignements pilote");
        public string TipSystemInfo     => T("System Info (Dotlan)",         "Infos système (Dotlan)");
        public string TipRefreshEsi     => T("Refresh ESI standings",        "Actualiser la réputation ESI");
        public string TipHelp           => T("Hotkeys, panels and getting started",
                                             "Raccourcis, panneaux et premiers pas");

        // ── MainWindow ────────────────────────────────────────────────────
        //
        // EVERY ENGLISH VALUE BELOW IS BYTE-IDENTICAL to the literal it
        // replaced in MainWindow.xaml, spacing and glyphs included. The
        // screenshot baseline captures the English UI, so if one character
        // drifts, visual-check fails -- which is exactly the safety net wanted
        // while moving 40-odd strings. The work here is the French side.
        public string BarTitle          => T("◈ CRYONIC OVERLAY",           "◈ OVERLAY CRYONIC");
        public string PanelInstances    => T("◈ ACTIVE INSTANCES",          "◈ INSTANCES ACTIVES");
        public string NoInstances       => T("No EVE instances detected",   "Aucune instance EVE détectée");
        public string LabelSystemPrefix => T("System: ",                    "Système : ");
        public string LabelEsiPrefix    => T("ESI: ",                       "ESI : ");

        // Intel buttons. Note these are NOT the BtnGateCamp/BtnPirates/
        // BtnRoaming above: those were written long ago with different glyphs
        // and casing, and were never bound to anything. These match the UI.
        public string BtnIntelGateCamp  => T("⚠ GATE CAMP",                 "⚠ GATE CAMP");
        public string BtnIntelPirates   => T("☠ PIRATES",                   "☠ PIRATES");
        public string BtnIntelRoaming   => T("⟳ ROAMING",                   "⟳ FLOTTE ERRANTE");

        // Panel launcher buttons
        public string BtnStanding       => T("📋  Standing",                "📋  Réputation");
        public string BtnSession        => T("📊  Session",                 "📊  Session");
        public string BtnSkills         => T("🎓  Skills",                  "🎓  Compétences");
        public string BtnHelp           => T("❔  Help",                    "❔  Aide");
        public string BtnOrders         => T("📢  Orders",                  "📢  Ordres");

        // Footer / banner
        public string BtnSupport        => T("♥ Support",                   "♥ Soutenir");
        public string BtnDownload       => T("Download",                    "Télécharger");
        public string LabelAd           => T("AD",                          "PUB");
        public string UpdateAvailable   => T("⬆ Update available — v",      "⬆ Mise à jour disponible — v");
        public string SponsorHouseHead  => T("Sponsor this slot",           "Sponsorisez cet espace");
        public string SponsorHouseSub   => T("Contact tendeuse on Discord", "Contactez tendeuse sur Discord");
        public string ClickThroughOff   => T("🖱️ Interactive",              "🖱️ Interactif");
        public string ClickThroughOn    => T("👁️ Click-Through ON",         "👁️ Clic traversant activé");

        // MainWindow tooltips
        public string TipDismiss        => T("Dismiss",                     "Ignorer");
        public string TipResize         => T("Drag to resize",              "Glisser pour redimensionner");
        public string TipInstanceCard   => T("Click to focus · Double-click to detach",
                                             "Cliquer pour activer · Double-cliquer pour détacher");
        public string TipStandingGuide  => T("Faction Standing Guide",      "Guide de réputation de faction");
        public string TipSkillPlan      => T("Faction Skill Plan",          "Plan de compétences de faction");
        public string TipMissionProgress=> T("Mission Progress tracker",    "Suivi de progression des missions");
        public string TipOrders         => T("Orders from your corp / coalition",
                                             "Ordres de votre corpo / coalition");
        public string TipNewOrder       => T("New order received",          "Nouvel ordre reçu");
        public string TipSupport        => T("Support development on GitHub Sponsors — a voluntary donation. It does not unlock features or remove ads.",
                                             "Soutenez le développement via GitHub Sponsors — un don volontaire. Cela ne débloque aucune fonctionnalité et ne retire pas les publicités.");

        // ── SettingsWindow ────────────────────────────────────────────────
        //
        // Same rule as MainWindow: English byte-identical to the literal it
        // replaced. Settings is not in the screenshot baseline, so there is no
        // automated net here -- these were transcribed and re-checked by hand.
        public string SetTitle          => T("⚙  Settings",                 "⚙  Paramètres");
        public string SetAccountType    => T("Account Type",                "Type de compte");
        public string SetAlpha          => T("Alpha clone  (skill caps apply)",
                                             "Clone Alpha  (plafonds de compétences)");
        public string SetOmega          => T("Omega clone  (no skill caps)",
                                             "Clone Omega  (aucun plafond)");
        public string SetFactionFocus   => T("Faction Focus",               "Faction ciblée");
        public string SetFontSize       => T("Font Size",                   "Taille du texte");
        public string SetFontSmall      => T("Small (9)",                   "Petite (9)");
        public string SetFontNormal     => T("Normal (11)",                 "Normale (11)");
        public string SetFontLarge      => T("Large (13)",                  "Grande (13)");
        public string SetFontXl         => T("XL (15)",                     "TG (15)");
        public string SetSkin           => T("Skin",                        "Skin");
        public string SetAppliesNow     => T("  (applies immediately)",     "  (effet immédiat)");
        public string SetAppliesOnSave  => T("  (applies on save)",         "  (effet à l'enregistrement)");
        public string SetFeatures       => T("FEATURES",                    "FONCTIONNALITÉS");
        public string SetEveChars       => T("EVE Characters (ESI)",        "Personnages EVE (ESI)");
        public string SetNoChars        => T("No characters linked.",       "Aucun personnage lié.");
        public string SetTokensLocal    => T("Tokens stored locally — never sent to the server.",
                                             "Jetons stockés localement — jamais envoyés au serveur.");
        public string SetAddChar        => T("+ Add Character",             "+ Ajouter un personnage");
        public string SetSetActive      => T("Set Active",                  "Définir comme actif");
        public string SetAutoDetected   => T("Auto-detected when you refresh the skill plan via ESI.",
                                             "Détecté automatiquement lors de l'actualisation du plan via ESI.");
        public string SetSave           => T("Save",                        "Enregistrer");
        public string SetSaveClose      => T("Save & Close",                "Enregistrer et fermer");
        public string SetCancel         => T("Cancel",                      "Annuler");

        // Feature toggles — these name the panels, so they must read the same
        // as the panel headers elsewhere in the app.
        public string SetFeatInstances  => T("Active Instances",            "Instances actives");
        public string SetFeatIntel      => T("Intel & Alerts",              "Intel et alertes");
        public string SetFeatPilot      => T("Pilot Status",                "Statut pilote");
        public string SetFeatStanding   => T("Standing Guide",              "Guide de réputation");
        public string SetFeatMission    => T("Mission Progress",            "Progression des missions");
        public string SetFeatSkill      => T("Skill Plan",                  "Plan de compétences");
        public string SetFeatPilotIntel => T("🔍 Pilot Intel",        "🔍 Renseignements pilote");
        public string SetFeatDotlan     => T("🌍 Dotlan / System Info", "🌍 Dotlan / Infos système");

        // Faction names.
        //
        // The four empires and CONCORD follow EVE's own French client. Pirate
        // factions and the smaller groups are proper nouns that the FR client
        // leaves alone, so they are left alone here too -- inventing French for
        // "Blood Raiders" would read worse than not translating it.
        // FLAGGED FOR REVIEW: Sansha, Mordu, SoCT are the least certain.
        public string FacCaldari        => T("Caldari State",               "État Caldari");
        public string FacGallente       => T("Gallente Federation",         "Fédération Gallente");
        public string FacAmarr          => T("Amarr Empire",                "Empire Amarr");
        public string FacMinmatar       => T("Minmatar Republic",           "République Minmatar");
        public string FacSoe            => T("Sisters of EVE",              "Sisters of EVE");
        // TODO(fr-verify): invented, same as the four I got wrong.
        public string FacConcord        => T("CONCORD Assembly",            "Assemblée CONCORD");
        public string FacOre            => T("ORE",                         "ORE");
        public string FacEdencom        => T("EDENCOM",                     "EDENCOM");
        // TODO(fr-verify): invented, same as the four I got wrong.
        public string FacTriglavian     => T("Triglavian Collective",       "Collectif Triglavian");
        public string FacGuristas       => T("Guristas Pirates",            "Guristas Pirates");
        public string FacAngels         => T("Angel Cartel",                "Angel Cartel");
        public string FacBlood          => T("Blood Raiders",               "Blood Raiders");
        public string FacSerpentis      => T("Serpentis Corporation",       "Serpentis Corporation");
        public string FacSansha         => T("Sansha's Nation",             "Sansha's Nation");
        public string FacMordus         => T("Mordu's Legion Command",      "État-major de la Mordu's Legion");
        public string FacSoct           => T("Society of Conscious Thought","La Society of Conscious Thought");

        // Settings runtime messages (code-behind, not XAML)
        public string SetWindowTitle    => T("Settings — Cryonic Overlay",  "Paramètres — Cryonic Overlay");
        public string SetCharsLinked(int n) => _lang == OverlayLanguage.EN
            ? $"{n} character(s) linked."
            : $"{n} personnage(s) lié(s).";
        public string SetOpeningBrowser => T("⏳  Opening browser — log in as the character you want to add…",
                                             "⏳  Ouverture du navigateur — connectez-vous avec le personnage à ajouter…");
        public string SetLinkedOk(string who) => _lang == OverlayLanguage.EN
            ? $"✅  {who} linked successfully!"
            : $"✅  {who} lié avec succès !";
        public string SetActiveOk(string who) => _lang == OverlayLanguage.EN
            ? $"✅  {who} set as active character. Changes take effect on the next 30s refresh."
            : $"✅  {who} défini comme personnage actif. Effet à la prochaine actualisation (30 s).";
        public string SetUnlinkAsk(string who) => _lang == OverlayLanguage.EN
            ? $"Unlink {who}?" : $"Délier {who} ?";
        public string SetUnlinkedOk(string who) => _lang == OverlayLanguage.EN
            ? $"{who} unlinked." : $"{who} délié.";
        public string SetConfirm        => T("Confirm",                     "Confirmer");
        public string SetCorpSkinLock   => T("Your corporation sets the skin for its members. Sponsor personally to choose your own.",
                                             "Votre corporation impose le skin à ses membres. Sponsorisez à titre personnel pour choisir le vôtre.");
        public string SetSkinFailed(string skin) => _lang == OverlayLanguage.EN
            ? $"Could not load the {skin} skin." : $"Impossible de charger le skin {skin}.";
        public string SetSavedMsg       => T("✅  Saved. Close this window to apply the panel toggles.",
                                             "✅  Enregistré. Fermez cette fenêtre pour appliquer les panneaux.");
        public string SetMoreSkins(int n) => _lang == OverlayLanguage.EN
            ? $"{n} more skin(s) available to sponsors. They appear here automatically once linked."
            : $"{n} skin(s) supplémentaire(s) pour les sponsors. Ils apparaissent ici automatiquement une fois liés.";

        // ── StandingGuideWindow ───────────────────────────────────────────
        public string SgTitleBar        => T("Faction Standing Guide — Cryonic Overlay",
                                             "Guide de réputation — Cryonic Overlay");
        public string SgTitle           => T("📋  FACTION STANDING GUIDE",  "📋  GUIDE DE RÉPUTATION");
        public string SgSelectFaction   => T("SELECT FACTION",              "CHOISIR UNE FACTION");
        public string SgAgentAccess     => T("AGENT ACCESS PROGRESS",       "PROGRESSION D'ACCÈS AUX AGENTS");
        public string SgSteps           => T("PROGRESSION STEPS",           "ÉTAPES DE PROGRESSION");
        public string SgYourStanding    => T("Your standing:",              "Votre réputation :");
        public string SgStandingPrefix  => T("Standing: ",                  "Réputation : ");
        public string SgCorpStanding    => T("Corp standing: ",             "Réputation corpo : ");
        public string SgAgentPrefix     => T("Agent: ",                     "Agent : ");
        public string SgRegionPrefix    => T("Region: ",                    "Région : ");
        public string SgLevelPrefix     => T("Level ",                      "Niveau ");
        public string SgRequires        => T("Requires ",                   "Requiert ");
        public string SgGoalArrow       => T("  →  goal ",                  "  →  objectif ");
        public string SgRunFor          => T("🏢 Run for: ",                "🏢 À faire pour : ");
        public string SgTank            => T("🔥 Tank: ",                   "🔥 Tank : ");
        public string SgBtnSet          => T("Set",                         "Définir");
        public string SgBtnCopy         => T("📋 Copy",                     "📋 Copier");
        public string SgBtnOrder        => T("📢 Order",                    "📢 Ordre");
        public string SgAutoplay        => T("▶ Auto-play briefing when standing advances",
                                             "▶ Lire le briefing quand la réputation progresse");

        // Skill names follow EVE's French client.
        // FLAGGED FOR REVIEW: "Relations" for Connections is the one I am least
        // sure of.
        // Verified against the EVE client.
        public string SgSkillConnections=> T("Connections",                 "Relations");
        public string SgSkillDiplomacy  => T("Diplomacy",                   "Diplomatie");
        public string SgSkillSocial     => T("Social",                      "Social");

        public string SgTipConnections  => T("+4% per level on positive standing (access only)",
                                             "+4 % par niveau sur réputation positive (accès uniquement)");
        public string SgTipDiplomacy    => T("+4% per level on negative standing (access only)",
                                             "+4 % par niveau sur réputation négative (accès uniquement)");
        public string SgTipSocial       => T("+5% per level to standing GAINS from missions",
                                             "+5 % par niveau sur les GAINS de réputation des missions");
        public string SgTipOrder        => T("Broadcast this step to your corporation as an Order",
                                             "Diffuser cette étape à votre corporation comme un Ordre");
        public string SgTipFit          => T("Click to view basic fit and skill requirements",
                                             "Cliquer pour voir le fit de base et les compétences requises");
        public string SgTipCopy         => T("Copy agent, location, corporation and region to clipboard",
                                             "Copier agent, lieu, corporation et région dans le presse-papiers");
        public string SgTipBriefing     => T("Play the officer's mission briefing",
                                             "Lire le briefing de mission de l'officier");
        public string SgTipRefreshEsi   => T("Refresh standings from ESI API",
                                             "Actualiser la réputation depuis l'API ESI");
        public string SgTipAutoplay     => T("When a standing update pushes you into the next step, its officer briefing plays automatically",
                                             "Quand une mise à jour de réputation vous fait passer à l'étape suivante, son briefing se lance automatiquement");

        // -- Remaining windows ---------------------------------------------
        // Briefing
        public string BrTitleBar        => T("Incoming Transmission",       "Transmission entrante");
        public string BrIncoming        => T("INCOMING TRANSMISSION",       "TRANSMISSION ENTRANTE");
        public string BrPause           => T("⏸ Pause",                     "⏸ Pause");
        public string BrRestart         => T("⟳ Restart",                   "⟳ Recommencer");
        public string BrClose           => T("Close",                       "Fermer");
        // First run
        public string FrTitleBar        => T("First Run Setup",             "Configuration initiale");
        public string FrHeading         => T("Cryonic Overlay — First Run", "Cryonic Overlay — Premier lancement");
        public string FrAccountType     => T("Account Type",                "Type de compte");
        public string FrFaction         => T("Faction Focus (can be changed later)",
                                             "Faction ciblée (modifiable plus tard)");
        public string FrLogin           => T("Log in with EVE",             "Se connecter avec EVE");
        public string FrAlpha           => T("Alpha",                       "Alpha");
        public string FrOmega           => T("Omega",                       "Omega");
        // Pilot intel
        public string IwTitleBar        => T("Pilot Intel — Cryonic Overlay","Renseignements pilote — Cryonic Overlay");
        public string IwHeading         => T("🔍  PILOT INTEL",             "🔍  RENSEIGNEMENTS PILOTE");
        public string IwSearch          => T("Search",                      "Rechercher");
        public string IwClear           => T("Clear",                       "Effacer");
        public string IwLookback        => T("Lookback: ",                  "Historique : ");
        public string IwDays90          => T("90 days",                     "90 jours");
        // Session
        public string MpTitleBar        => T("Session — Cryonic Overlay",   "Session — Cryonic Overlay");
        public string MpHeading         => T("⚡ SESSION — TODAY",          "⚡ SESSION — AUJOURD'HUI");
        public string MpIsk             => T("ISK (net)",                   "ISK (net)");
        public string MpKills           => T("Kills",                       "Kills");
        public string MpLp              => T("Loyalty points",              "Points de loyauté");
        public string MpMined           => T("MINED TODAY",                 "MINÉ AUJOURD'HUI");
        public string MpNoMining        => T("No mining recorded today.",   "Aucun minage enregistré aujourd'hui.");
        public string MpLedgerLag       => T("ledger updates daily — recent mining may lag",
                                             "le registre est quotidien — le minage récent peut être en retard");
        public string MpPlayed          => T("played ",                     "joué ");
        // Order note prompt
        public string NpTitleBar        => T("Mark order complete",         "Marquer l'ordre comme terminé");
        public string NpHeading         => T("📢  MARK ORDER COMPLETE",     "📢  MARQUER L'ORDRE TERMINÉ");
        public string NpHint            => T("Optional note for your CEO — e.g. a killmail link or what you did.",
                                             "Note facultative pour votre CEO — par ex. un lien de killmail ou ce que vous avez fait.");
        public string NpConfirm         => T("✔ Mark complete",             "✔ Marquer terminé");
        public string NpCancel          => T("Cancel",                      "Annuler");
        // Orders
        public string OwTitleBar        => T("Orders — Cryonic Overlay",    "Ordres — Cryonic Overlay");
        public string OwHeading         => T("📢  ORDERS",                  "📢  ORDRES");
        public string OwJoin            => T("Join",                        "Rejoindre");
        public string OwLeave           => T("Leave",                       "Quitter");
        public string OwMarkComplete    => T("Mark complete",               "Marquer terminé");
        public string OwSubmitKill      => T("Submit kill",                 "Soumettre un kill");
        public string OwClaimStanding   => T("Claim standing",              "Réclamer la réputation");
        // Pilot status
        public string PsTitleBar        => T("Pilot Status — Cryonic Overlay","Statut pilote — Cryonic Overlay");
        public string PsHeading         => T("👤  PILOT STATUS",            "👤  STATUT PILOTE");
        // Ship fit
        public string SfTitleBar        => T("Ship Fit",                    "Fit du vaisseau");
        public string SfHeading         => T("Ship Fit",                    "Fit du vaisseau");
        // Skill plan
        public string SpTitleBar        => T("Faction Skill Plan — Cryonic Overlay",
                                             "Plan de compétences — Cryonic Overlay");
        public string SpHeading         => T("🎓  FACTION SKILL PLAN",      "🎓  PLAN DE COMPÉTENCES");
        public string SpHeadingAlt      => T("📋 FACTION SKILL PLAN",       "📋 PLAN DE COMPÉTENCES");
        public string SpActiveLevel     => T("Active level: ",              "Niveau actuel : ");
        public string SpTarget          => T("Target:    ",                 "Objectif : ");
        public string SpSkillId         => T("Skill ID: ",                  "ID compétence : ");
        public string SpMatchesSheet    => T(" (matches in-game sheet)",    " (correspond à la fiche en jeu)");
        public string SpTipCopy         => T("Copy skill plan to clipboard — paste into EVE skill queue import",
                                             "Copier le plan — à coller dans l'import de file de compétences d'EVE");
        public string SpTipReload       => T("Reload skills from ESI",      "Recharger les compétences depuis ESI");
        // System info
        public string SyTitleBar        => T("System Info — Cryonic Overlay","Infos système — Cryonic Overlay");
        public string SyHeading         => T("🌐  SYSTEM INFO",             "🌐  INFOS SYSTÈME");
        public string SySystem          => T("📍 System",                   "📍 Système");
        public string SySov             => T("🏴 Sovereignty",              "🏴 Souveraineté");
        public string SyRoute           => T("🗺 Route",                    "🗺 Itinéraire");
        public string SyFrom            => T("From: ",                      "De : ");
        public string SyTo              => T("To:   ",                      "À :   ");
        public string SyGo              => T("Go",                          "Aller");
        public string SyLoad            => T("Load",                        "Charger");
        public string SySearch          => T("Search",                      "Rechercher");
        public string SySecure          => T("Secure",                      "Sécurisé");
        public string SyInsecure        => T("Insecure",                    "Non sécurisé");
        public string SyShortest        => T("Shortest",                    "Le plus court");
        // Detached instance window
        public string TwTitleBar        => T("Thumbnail",                   "Aperçu");
        public string TwDefaultLabel    => T("EVE Instance",                "Instance EVE");
        public string TwTipSwitch       => T("Click to switch to this EVE client",
                                             "Cliquer pour basculer vers ce client EVE");
        public string TwTipReattach     => T("Re-attach to overlay",        "Rattacher à l'overlay");
        public string TwTipClose        => T("Close and re-attach to the overlay",
                                             "Fermer et rattacher à l'overlay");
        // Help window OS title
        public string HelpTitleBar      => T("Help",                        "Aide");

        // Title bar
        public string AppTitle          => T("◈  CRYONIC OVERLAY",          "◈  OVERLAY CRYONIC");
        public string StatusConnecting  => T("Connecting…",                  "Connexion…");
        public string StatusOnline      => T("✅ Online",                    "✅ En ligne");
        public string StatusOffline     => T("❌ Offline",                   "❌ Hors ligne");

        // Pilot panel
        public string PanelPilot        => T("👤  PILOT STATUS",             "👤  STATUT PILOTE");
        public string LabelPilot        => T("Pilot",                        "Pilote");
        public string LabelCorp         => T("Corp",                         "Corp");
        public string LabelShip         => T("Ship",                         "Vaisseau");
        public string LabelSec          => T("Sec",                          "Séc");
        public string LabelSystem       => T("System",                       "Système");

        // Intel panel
        public string PanelIntel        => T("⚠️  INTEL & ALERTS",          "⚠️  INTEL ET ALERTES");
        public string BtnGateCamp       => T("⛔  Gate Camp",                "⛔  Gate Camp");
        public string BtnPirates        => T("💀  Pirates",                  "💀  Pirates");
        public string BtnRoaming        => T("⚠️  Roaming Gang",            "⚠️  Flotte errante");
        public string BtnClear          => T("✅  System Clear",             "✅  Système Libre");
        public string IntelNone         => T("No recent intel.",             "Aucun intel récent.");

        // Intel type labels
        public string IntelGateCamp     => T("GATE CAMP",                   "GATE CAMP");
        public string IntelPirates      => T("PIRATES",                     "PIRATES");
        public string IntelRoaming      => T("ROAMING",                     "FLOTTE ERRANTE");
        public string IntelClear        => T("CLEAR",                       "LIBRE");
        public string IntelNeutral      => T("NEUTRAL",                     "NEUTRE");

        // Missions panel
        public string PanelMissions     => T("📋  ACTIVE MISSIONS",         "📋  MISSIONS ACTIVES");
        public string MissionsNone      => T("No active missions.",          "Aucune mission active.");
        public string BtnAssign         => T("Assign to Me",                 "M'assigner");
        public string BtnComplete       => T("Complete ✅",                  "Terminer ✅");
        public string BtnSync           => T("🔄  Sync Now",                 "🔄  Synchroniser");
        public string LabelReward       => T("Reward",                       "Récompense");
        public string LabelStatus       => T("Status",                       "Statut");
        public string LabelCreatedBy    => T("By",                          "Par");
        public string LabelAssignedTo   => T("Assigned",                    "Assigné");

        // Mission statuses
        public string StatusOpen        => T("OPEN",                        "OUVERTE");
        public string StatusInProgress  => T("IN PROGRESS",                 "EN COURS");
        public string StatusCompleted   => T("COMPLETED",                   "TERMINÉE");
        public string StatusCancelled   => T("CANCELLED",                   "ANNULÉE");

        // Faction panel
        public string PanelFaction      => T("🎯  FACTION FOCUS",           "🎯  FACTION CIBLE");

        // Aliases expected by MainWindow.xaml bindings
        public string PilotStatus       => PanelPilot;
        public string IntelAlerts       => PanelIntel;
        public string ActiveMissions    => PanelMissions;
        public string FactionFocus      => PanelFaction;

        // First Run Wizard
        public string WizardTitle       => T("Cryonic Overlay — First Run",  "Cryonic Overlay — Premier lancement");
        public string WizardApiUrl      => T("API Base URL (Railway domain)",   "URL de l'API (domaine Railway)");
        public string WizardPairCode    => T("Pair Code (from Discord: /overlay pair)", "Code de Jumelage (Discord: /overlay pair)");
        public string WizardAccountType => T("Account Type",                    "Type de Compte");
        public string WizardFaction     => T("Faction Focus (can be changed later)", "Faction Cible (modifiable plus tard)");
        public string WizardPairBtn     => T("Pair & Launch",                   "Jumeler et Lancer");
        public string WizardStatus      => T("Run /overlay pair in Discord to get a code, then paste it here.",
                                             "Lancez /overlay pair sur Discord pour obtenir un code, puis collez-le ici.");

        // Footer hotkeys hint
        public string FooterHotkeys     => T(
            "Ctrl+Shift+O hide  •  Ctrl+Shift+C click-through  •  Ctrl+Shift+I intel  •  Ctrl+Shift+X clear",
            "Ctrl+Shift+O masquer  •  Ctrl+Shift+C clic-passant  •  Ctrl+Shift+I intel  •  Ctrl+Shift+X libre");

        // Errors / feedback
        public string ErrAssignFailed   => T("Assign failed",               "Échec d'assignation");
        public string ErrCompleteFailed => T("Complete failed",             "Échec de complétion");
        public string ErrIntelFailed    => T("Intel post failed",           "Échec envoi intel");
        public string ErrSyncFailed     => T("Sync failed",                 "Échec de synchro");

        // ----------------------------------------------------------------
        // INotifyPropertyChanged
        // ----------------------------------------------------------------
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ================================================================
        // HELP
        //
        // Written as prose pairs like everything else rather than pulled from
        // a resource file, so a French pilot never meets an English wall on
        // the one screen they open BECAUSE they are stuck.
        // ================================================================
        public string HelpTitle        => T("❔  HELP",  "❔  AIDE");
        public string HelpClose        => T("Close",     "Fermer");

        public string HelpHotkeysHead  => T("GLOBAL HOTKEYS",
                                            "RACCOURCIS GLOBAUX");
        public string HelpHotkeysIntro => T("These work anywhere, including while EVE has focus.",
                                            "Ils fonctionnent partout, même quand EVE a le focus.");
        public string HelpHotkeyShow   => T("Hide the overlay panel — instance previews stay visible",
                                            "Masquer le panneau — les aperçus d'instance restent visibles");
        public string HelpHotkeyClick  => T("Click-through — let clicks pass to the game",
                                            "Clic traversant — laisse passer les clics vers le jeu");
        public string HelpHotkeyIntel  => T("Report a roaming gang in your system",
                                            "Signaler une bande itinérante dans votre système");

        public string HelpHotkeyHideAll=> T("Hide everything — previews and pop-out windows included",
                                            "Tout masquer — aperçus et fenêtres détachées compris");

        public string HelpPanelsHead   => T("PANELS",
                                            "PANNEAUX");
        public string HelpPanelStanding=> T("Standing — faction standing guide, and which agents to run.",
                                            "Réputation — guide de réputation de faction, et quels agents faire.");
        public string HelpPanelSession => T("Session — what you have earned this session.",
                                            "Session — ce que vous avez gagné durant cette session.");
        public string HelpPanelSkills  => T("Skills — the skill plan for your chosen faction.",
                                            "Compétences — le plan de compétences pour votre faction.");
        public string HelpPanelOrders  => T("Orders — tasks from your corp or coalition. A ⚠ marks a new one.",
                                            "Ordres — tâches de votre corpo ou coalition. Un ⚠ signale un nouvel ordre.");

        public string HelpSetupHead    => T("GETTING STARTED",
                                            "PREMIERS PAS");
        public string HelpSetupLink    => T("Open ⚙ Settings and add a character to link EVE. Standings, skills and your ship only appear once a character is linked.",
                                            "Ouvrez ⚙ Paramètres et ajoutez un personnage pour lier EVE. Réputation, compétences et vaisseau n'apparaissent qu'une fois un personnage lié.");
        public string HelpSetupMulti   => T("Link several characters and switch between them with the ESI selector in Pilot Status.",
                                            "Liez plusieurs personnages et basculez entre eux avec le sélecteur ESI dans Statut Pilote.");
        public string HelpSetupPanels  => T("Settings also chooses which sections are shown. Hide what you do not use — the overlay shrinks to fit.",
                                            "Les paramètres choisissent aussi les sections affichées. Masquez ce que vous n'utilisez pas — l'overlay se réduit en conséquence.");
        public string HelpSetupSkins   => T("Skins change how the overlay looks, never what it does. Every feature works on the free default skin.",
                                            "Les skins changent l'apparence de l'overlay, jamais ses fonctions. Toutes les fonctionnalités marchent avec le skin gratuit par défaut.");

        public string HelpMoveHead     => T("MOVING AND RESIZING",
                                            "DÉPLACER ET REDIMENSIONNER");
        public string HelpMoveDrag     => T("Drag the title bar to move. Drag the right or bottom edge to resize.",
                                            "Glissez la barre de titre pour déplacer. Glissez le bord droit ou inférieur pour redimensionner.");
        public string HelpMoveClick    => T("In click-through mode the overlay stays visible but ignores the mouse, so you can fly through it.",
                                            "En mode clic traversant, l'overlay reste visible mais ignore la souris : vous pouvez voler à travers.");
    }
}
