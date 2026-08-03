// filename: Services/Localization.cs
// Bilingual EN/FR support for the ARC Overlay.
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

        public OverlayLanguage Language
        {
            get => _lang;
            set
            {
                if (_lang == value) return;
                _lang = value;
                // Notify ALL properties so every bound string refreshes
                OnPropertyChanged(string.Empty);
            }
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

        // Title bar
        public string AppTitle          => T("🛸  ARC MISSION OVERLAY",      "🛸  OVERLAY MISSIONS ARC");
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
        public string BtnGateCamp       => T("⛔  Gate Camp",                "⛔  Camp de Gate");
        public string BtnPirates        => T("💀  Pirates",                  "💀  Pirates");
        public string BtnRoaming        => T("⚠️  Roaming Gang",            "⚠️  Gang en Maraude");
        public string BtnClear          => T("✅  System Clear",             "✅  Système Libre");
        public string IntelNone         => T("No recent intel.",             "Aucun intel récent.");

        // Intel type labels
        public string IntelGateCamp     => T("GATE CAMP",                   "CAMP DE GATE");
        public string IntelPirates      => T("PIRATES",                     "PIRATES");
        public string IntelRoaming      => T("ROAMING",                     "MARAUDE");
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
        public string WizardTitle       => T("ARC Mission Overlay — First Run", "ARC Overlay Missions — Premier Lancement");
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
        public string HelpHotkeyShow   => T("Show or hide the overlay",
                                            "Afficher ou masquer l'overlay");
        public string HelpHotkeyClick  => T("Click-through — let clicks pass to the game",
                                            "Clic traversant — laisse passer les clics vers le jeu");
        public string HelpHotkeyIntel  => T("Report a roaming gang in your system",
                                            "Signaler une bande itinérante dans votre système");

        public string HelpPanelsHead   => T("PANELS",
                                            "PANNEAUX");
        public string HelpPanelStanding=> T("Standing — faction standing guide, and which agents to run.",
                                            "Standing — guide des standings de faction, et quels agents faire.");
        public string HelpPanelSession => T("Session — what you have earned this session.",
                                            "Session — ce que vous avez gagné durant cette session.");
        public string HelpPanelSkills  => T("Skills — the skill plan for your chosen faction.",
                                            "Compétences — le plan de compétences pour votre faction.");
        public string HelpPanelOrders  => T("Orders — tasks from your corp or coalition. A ⚠ marks a new one.",
                                            "Ordres — tâches de votre corpo ou coalition. Un ⚠ signale un nouvel ordre.");

        public string HelpSetupHead    => T("GETTING STARTED",
                                            "PREMIERS PAS");
        public string HelpSetupLink    => T("Open ⚙ Settings and add a character to link EVE. Standings, skills and your ship only appear once a character is linked.",
                                            "Ouvrez ⚙ Paramètres et ajoutez un personnage pour lier EVE. Standings, compétences et vaisseau n'apparaissent qu'une fois un personnage lié.");
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
