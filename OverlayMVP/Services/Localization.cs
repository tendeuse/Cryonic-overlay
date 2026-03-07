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
    }
}
