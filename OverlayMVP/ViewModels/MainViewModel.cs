using System.Collections.Generic;
// filename: ViewModels/MainViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OverlayMVP.Models;
using System.Linq;
using OverlayMVP.Services;

namespace OverlayMVP.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        // ----------------------------------------------------------------
        // Services
        // ----------------------------------------------------------------
        private readonly AppDb            _db;
        private readonly OverlayApiClient _api;
        private readonly OverlayConfig    _cfg;
        private readonly EveLogWatcher    _logWatcher = new();

        private CancellationTokenSource? _pollCts;
        private const int PollingIntervalMs    = 10_000;
        private const int EveWindowRefreshMs   = 3_000;   // scan EVE windows every 3s

        // ----------------------------------------------------------------
        // Localization
        // ----------------------------------------------------------------
        public LocalizationManager Loc => LocalizationManager.Instance;

        // ----------------------------------------------------------------
        // Observable properties — connection / status
        // ----------------------------------------------------------------
        [ObservableProperty] private string connectionStatus  = "";
        [ObservableProperty] private bool   isConnected       = false;
        [ObservableProperty] private bool   isClickThrough    = false;
        [ObservableProperty] private string clickThroughLabel = "🖱️ Interactive";

        // Character
        [ObservableProperty] private string characterName  = "—";
        [ObservableProperty] private string corporation    = "—";
        [ObservableProperty] private string shipType       = "—";
        [ObservableProperty] private string solarSystem    = "—";
        [ObservableProperty] private string securityStatus = "—";
        [ObservableProperty] private string securityColour = "#FFFFFF";

        // Missions
        [ObservableProperty] private string missionSummary = "";
        [ObservableProperty] private int    missionCount   = 0;

        // Intel
        [ObservableProperty] private string intelStatus = "";
        [ObservableProperty] private bool   hasAlerts   = false;
        [ObservableProperty] private string currentSystem = "";

        // ── Mission Catalogue — standing-based tutorial ───────────────────
        public List<FactionCatalogue> CatalogueFactions => MissionCatalogueData.Factions;

        [ObservableProperty]
        private FactionCatalogue? selectedCatalogueFaction;

        [ObservableProperty]
        private TutorialStep? activeTutorialStep;

        [ObservableProperty]
        private float currentFactionStanding = 0.0f;

        [ObservableProperty]
        private bool standingsLoaded = false;

        [ObservableProperty]
        private string standingsStatus = "ESI standings not linked";

        // Standing display helpers
        [ObservableProperty] private string standingLabel  = "NEUTRAL";
        [ObservableProperty] private string standingColour = "#FFFFD54F";

        // Dictionary: ESI faction_id → standing value
        private readonly Dictionary<int, float> _standings = new();

        // Personal presets saved by the player
        public ObservableCollection<TutorialStep> PersonalPresets { get; } = new();

        // Faction
        [ObservableProperty] private string factionFocus;

        // ----------------------------------------------------------------
        // EVE Multibox
        // ----------------------------------------------------------------
        [ObservableProperty] private bool   hasEveWindows    = false;
        [ObservableProperty] private string eveWindowCount   = "No EVE clients detected";
        [ObservableProperty] private bool   isAnchored       = false;
        [ObservableProperty] private string anchorLabel      = "📌 Anchor to EVE";

        // The handle of the EVE window the overlay is currently anchored to
        public IntPtr AnchoredEveHandle { get; private set; } = IntPtr.Zero;

        // Raised when the overlay should reposition itself (MainWindow listens)
        public event Action<IntPtr>? AnchorRequested;

        // ----------------------------------------------------------------
        // Collections
        // ----------------------------------------------------------------
        public ObservableCollection<Mission>     Missions   { get; } = new();
        public ObservableCollection<IntelReport> Intel      { get; } = new();
        public ObservableCollection<EveWindow>   EveWindows { get; } = new();

        // ----------------------------------------------------------------
        // Constructor
        // ----------------------------------------------------------------
        public MainViewModel(AppDb db, OverlayConfig cfg)
        {
            _db  = db;
            _cfg = cfg;
            FactionFocus     = cfg.FactionFocus ?? string.Empty;
            _api             = new OverlayApiClient(cfg.ApiBaseUrl, cfg.OverlayToken);
            ConnectionStatus = Loc.StatusConnecting;
            MissionSummary   = Loc.MissionsNone;
            IntelStatus      = Loc.IntelNone;
        }

        // ----------------------------------------------------------------
        // Lifecycle
        // ----------------------------------------------------------------
        public void StartPolling()
        {
            _pollCts = new CancellationTokenSource();
            _ = PollLoopAsync(_pollCts.Token);
            _ = EveWindowLoopAsync(_pollCts.Token);

            // Pre-populate from the active window's log at startup
            var snapshot = _logWatcher.GetCurrentSystemSnapshot();
            if (!string.IsNullOrEmpty(snapshot))
                CurrentSystem = snapshot;

            // Live updates: fires only when the ACTIVE window's character jumps
            _logWatcher.SystemChanged += system =>
                Application.Current.Dispatcher.Invoke(() => CurrentSystem = system);

            // Try to load ESI standings at startup
            _ = RefreshStandingsAsync();

        }

        private async Task PollLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await RefreshAsync();
                try { await Task.Delay(PollingIntervalMs, ct); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task EveWindowLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                RefreshEveWindows();
                try { await Task.Delay(EveWindowRefreshMs, ct); }
                catch (TaskCanceledException) { break; }
            }
        }

        // ----------------------------------------------------------------
        // EVE Window detection
        // ----------------------------------------------------------------
        private void RefreshEveWindows()
        {
            var windows = EveWindowManager.GetEveWindows();

            Application.Current.Dispatcher.Invoke(() =>
            {
                EveWindows.Clear();
                foreach (var w in windows) EveWindows.Add(w);

                HasEveWindows  = windows.Count > 0;
                EveWindowCount = windows.Count == 0
                    ? "No EVE clients detected"
                    : $"{windows.Count} EVE client{(windows.Count > 1 ? "s" : "")} detected";

                // If anchored window was closed, release anchor
                if (IsAnchored && AnchoredEveHandle != IntPtr.Zero)
                {
                    bool stillOpen = windows.Exists(w => w.Handle == AnchoredEveHandle);
                    if (!stillOpen) ReleaseAnchor();
                }

                // Re-fire anchor position if still anchored
                if (IsAnchored && AnchoredEveHandle != IntPtr.Zero)
                    AnchorRequested?.Invoke(AnchoredEveHandle);
            });
        }

        // ----------------------------------------------------------------
        // EVE Commands
        // ----------------------------------------------------------------

        /// <summary>Switch focus to the given EVE window.</summary>
        [RelayCommand]
        public void FocusEveWindow(EveWindow window)
        {
            EveWindowManager.FocusWindow(window.Handle);
        }

        /// <summary>Anchor overlay to the given EVE window.</summary>
        [RelayCommand]
        public void AnchorToEveWindow(EveWindow window)
        {
            AnchoredEveHandle = window.Handle;
            IsAnchored        = true;
            AnchorLabel       = $"📌 Anchored: {window.DisplayLabel}";
            AnchorRequested?.Invoke(window.Handle);
        }

        /// <summary>Release anchor — overlay becomes freely movable again.</summary>
        [RelayCommand]
        public void ReleaseAnchor()
        {
            AnchoredEveHandle = IntPtr.Zero;
            IsAnchored        = false;
            AnchorLabel       = "📌 Anchor to EVE";
        }

        // ----------------------------------------------------------------
        // Language toggle
        // ----------------------------------------------------------------
        [RelayCommand]
        public void ToggleLanguage()
        {
            Loc.Toggle();
            MissionSummary = MissionCount == 0
                ? Loc.MissionsNone
                : $"{MissionCount} {(Loc.Language == OverlayLanguage.EN ? "mission(s) active" : "mission(s) active(s)")}";
            IntelStatus = Intel.Count == 0
                ? Loc.IntelNone
                : $"⚠️ {Intel.Count} {(Loc.Language == OverlayLanguage.EN ? "active report(s)" : "rapport(s) actif(s)")}";
            ClickThroughLabel = IsClickThrough
                ? (Loc.Language == OverlayLanguage.EN ? "👁️ Click-Through ON" : "👁️ Clic-Passant ACTIF")
                : (Loc.Language == OverlayLanguage.EN ? "🖱️ Interactive"      : "🖱️ Interactif");
        }

        // ----------------------------------------------------------------
        // Refresh / Sync from API
        // ----------------------------------------------------------------
        [RelayCommand]
        public async Task RefreshAsync()
        {
            try
            {
                var data = await _api.GetSnapshotAsync();
                if (data is null) return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Character
                    if (data.Character is not null)
                    {
                        CharacterName  = data.Character.CharacterName;
                        Corporation    = data.Character.Corporation;
                        ShipType       = string.IsNullOrEmpty(data.Character.ShipType)
                                            ? "—" : data.Character.ShipType;
                        SolarSystem    = data.Character.SolarSystem;
                        SecurityStatus = data.Character.SecurityStatus.ToString("F1");
                        SecurityColour = data.Character.SecurityColour;
                        // Auto-fill system input if user hasn't typed one
                        if (string.IsNullOrWhiteSpace(CurrentSystem) &&
                            !string.IsNullOrEmpty(data.Character.SolarSystem))
                            CurrentSystem = data.Character.SolarSystem;
                    }

                    // Missions
                    Missions.Clear();
                    foreach (var m in data.Missions) Missions.Add(m);
                    MissionCount   = data.Missions.Count;
                    MissionSummary = data.Missions.Count == 0
                        ? Loc.MissionsNone
                        : $"{data.Missions.Count} {(Loc.Language == OverlayLanguage.EN ? "mission(s) active" : "mission(s) active(s)")}";

                    // Intel
                    Intel.Clear();
                    foreach (var i in data.Intel) Intel.Add(i);
                    HasAlerts   = data.Intel.Count > 0;
                    IntelStatus = data.Intel.Count == 0
                        ? Loc.IntelNone
                        : $"⚠️ {data.Intel.Count} {(Loc.Language == OverlayLanguage.EN ? "active report(s)" : "rapport(s) actif(s)")}";

                    ConnectionStatus = $"{Loc.StatusOnline}  •  {DateTime.Now:HH:mm:ss}";
                    IsConnected      = true;
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ConnectionStatus = $"{Loc.StatusOffline}  •  {ex.Message[..Math.Min(ex.Message.Length, 40)]}";
                    IsConnected      = false;
                });
            }
        }

        // ----------------------------------------------------------------
        // Intel commands
        // ----------------------------------------------------------------
        [RelayCommand] public async Task ReportGateCampAsync() => await PostIntelAsync(IntelType.GateCamp, "Gate camp reported");
        [RelayCommand] public async Task ReportPiratesAsync()  => await PostIntelAsync(IntelType.Pirate,   "Pirates reported");
        [RelayCommand] public async Task ReportRoamingAsync()  => await PostIntelAsync(IntelType.Roaming,  "Roaming gang reported");
        [RelayCommand] public async Task ReportClearAsync()    => await PostIntelAsync(IntelType.Clear,    "System clear");

        // ── Catalogue commands ────────────────────────────────────────────
        partial void OnSelectedCatalogueFactionChanged(FactionCatalogue? value)
        {
            if (value is null) { ActiveTutorialStep = null; return; }
            var s = _standings.TryGetValue(value.EsiFactionId, out var v) ? v : 0.0f;
            CurrentFactionStanding = s;
            StandingLabel  = StandingInfo.GetLabel(s);
            StandingColour = StandingInfo.GetColour(s);
            ActiveTutorialStep = value.GetActiveStep(s);
        }

        [RelayCommand]
        private async Task RefreshStandingsAsync()
        {
            try
            {
                StandingsStatus = "Loading from ESI...";
                var list = await _api.GetStandingsAsync();
                _standings.Clear();
                foreach (var s in list)
                    _standings[s.FactionId] = s.Standing;

                StandingsLoaded = list.Count > 0;
                StandingsStatus = list.Count > 0
                    ? $"ESI: {list.Count} factions loaded"
                    : "ESI not linked — enter standing manually";

                if (SelectedCatalogueFaction is not null)
                    OnSelectedCatalogueFactionChanged(SelectedCatalogueFaction);
            }
            catch (Exception ex)
            {
                StandingsStatus = $"ESI error: {ex.Message}";
            }
        }

        [RelayCommand]
        private void SetManualStanding(string standingText)
        {
            if (!float.TryParse(standingText, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var val)) return;
            val = System.Math.Clamp(val, -10f, 10f);
            CurrentFactionStanding = val;
            if (SelectedCatalogueFaction is not null)
            {
                _standings[SelectedCatalogueFaction.EsiFactionId] = val;
                StandingLabel  = StandingInfo.GetLabel(val);
                StandingColour = StandingInfo.GetColour(val);
                ActiveTutorialStep = SelectedCatalogueFaction.GetActiveStep(val);
            }
        }

        [RelayCommand]
        private async Task CreateFromStepAsync(TutorialStep? step)
        {
            if (step is null) return;
            try
            {
                var description = $"Agent: {step.Agent} | Station: {step.Station} | {step.TipText}";
                var mission = await _api.CreateMissionAsync(step.StepLabel, description);
                if (mission is not null) Missions.Add(mission);
            }
            catch (Exception ex) { ConnectionStatus = $"Error: {ex.Message}"; }
        }

        [RelayCommand]
        private void SavePersonalPreset(TutorialStep? step)
        {
            if (step is null) return;
            if (!PersonalPresets.Any(p => p.StepLabel == step.StepLabel && p.Agent == step.Agent))
                PersonalPresets.Add(step);
        }

        [RelayCommand]
        private void RemovePersonalPreset(TutorialStep? step)
        {
            if (step is null) return;
            var existing = PersonalPresets.FirstOrDefault(
                p => p.StepLabel == step.StepLabel && p.Agent == step.Agent);
            if (existing is not null) PersonalPresets.Remove(existing);
        }

        private async Task PostIntelAsync(IntelType type, string notes)
        {
            try
            {
                // Priority: user-typed system > API system > "Unknown"
                var system = !string.IsNullOrWhiteSpace(CurrentSystem) ? CurrentSystem.Trim()
                           : !string.IsNullOrEmpty(SolarSystem) && SolarSystem != "—" ? SolarSystem
                           : "Unknown";
                await _api.PostIntelAsync(system, type, 1, notes);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"⚠️ {Loc.ErrIntelFailed}: {ex.Message[..Math.Min(ex.Message.Length, 30)]}";
            }
        }

        // ----------------------------------------------------------------
        // Mission commands
        // ----------------------------------------------------------------
        [RelayCommand]
        public async Task AssignMissionAsync(int missionId)
        {
            try   { await _api.AssignMissionAsync(missionId);  await RefreshAsync(); }
            catch (Exception ex) { ConnectionStatus = $"⚠️ {Loc.ErrAssignFailed}: {ex.Message[..Math.Min(ex.Message.Length, 30)]}"; }
        }

        [RelayCommand]
        public async Task CompleteMissionAsync(int missionId)
        {
            try   { await _api.CompleteMissionAsync(missionId); await RefreshAsync(); }
            catch (Exception ex) { ConnectionStatus = $"⚠️ {Loc.ErrCompleteFailed}: {ex.Message[..Math.Min(ex.Message.Length, 30)]}"; }
        }

        // ----------------------------------------------------------------
        // Click-through state (called from code-behind)
        // ----------------------------------------------------------------
        public void ToggleClickThrough(bool isNowClickThrough)
        {
            IsClickThrough    = isNowClickThrough;
            ClickThroughLabel = isNowClickThrough
                ? (Loc.Language == OverlayLanguage.EN ? "👁️ Click-Through ON" : "👁️ Clic-Passant ACTIF")
                : (Loc.Language == OverlayLanguage.EN ? "🖱️ Interactive"      : "🖱️ Interactif");
        }

        // ----------------------------------------------------------------
        // Faction save
        // ----------------------------------------------------------------
        partial void OnFactionFocusChanged(string value)
        {
            _cfg.FactionFocus = value;
            _cfg.Save(_db);
        }

        public void Dispose()
        {
            _logWatcher.Dispose();
            _pollCts?.Cancel();
            _pollCts?.Dispose();
            _api.Dispose();
        }
    }
}
