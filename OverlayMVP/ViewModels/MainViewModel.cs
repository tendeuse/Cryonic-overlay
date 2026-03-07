// filename: ViewModels/MainViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OverlayMVP.Models;
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

        private CancellationTokenSource? _pollCts;
        private const int PollingIntervalMs = 10_000;

        // ----------------------------------------------------------------
        // Localization — expose singleton so XAML can bind {Binding Loc.XXX}
        // ----------------------------------------------------------------
        public LocalizationManager Loc => LocalizationManager.Instance;

        // ----------------------------------------------------------------
        // Observable properties
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

        // Faction
        [ObservableProperty] private string factionFocus;

        // Collections
        public ObservableCollection<Mission>     Missions { get; } = new();
        public ObservableCollection<IntelReport> Intel    { get; } = new();

        // ----------------------------------------------------------------
        // Constructor
        // ----------------------------------------------------------------
        public MainViewModel(AppDb db, OverlayConfig cfg)
        {
            _db           = db;
            _cfg          = cfg;
            FactionFocus = cfg.FactionFocus ?? string.Empty;
            _api          = new OverlayApiClient(cfg.ApiBaseUrl, cfg.OverlayToken);

            // Initialize status strings from localization
            ConnectionStatus = Loc.StatusConnecting;
            MissionSummary   = Loc.MissionsNone;
            IntelStatus      = Loc.IntelNone;
        }

        // ----------------------------------------------------------------
        // Language toggle command
        // ----------------------------------------------------------------
        [RelayCommand]
        public void ToggleLanguage()
        {
            Loc.Toggle();
            // Refresh status strings that are composed at runtime
            MissionSummary = MissionCount == 0
                ? Loc.MissionsNone
                : $"{MissionCount} {(Loc.Language == OverlayLanguage.EN ? "mission(s) active" : "mission(s) active(s)")}";
            IntelStatus    = Intel.Count == 0
                ? Loc.IntelNone
                : $"⚠️ {Intel.Count} {(Loc.Language == OverlayLanguage.EN ? "active report(s)" : "rapport(s) actif(s)")}";
            ClickThroughLabel = IsClickThrough
                ? (Loc.Language == OverlayLanguage.EN ? "👁️ Click-Through ON" : "👁️ Clic-Passant ACTIF")
                : (Loc.Language == OverlayLanguage.EN ? "🖱️ Interactive"     : "🖱️ Interactif");
        }

        // ----------------------------------------------------------------
        // Lifecycle
        // ----------------------------------------------------------------
        public void StartPolling()
        {
            _pollCts = new CancellationTokenSource();
            _ = PollLoopAsync(_pollCts.Token);
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

        // ----------------------------------------------------------------
        // Refresh / Sync
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
        [RelayCommand] public async Task ReportGateCampAsync()  => await PostIntelAsync(IntelType.GateCamp, "Gate camp reported");
        [RelayCommand] public async Task ReportPiratesAsync()   => await PostIntelAsync(IntelType.Pirate,   "Pirates reported");
        [RelayCommand] public async Task ReportRoamingAsync()   => await PostIntelAsync(IntelType.Roaming,  "Roaming gang reported");
        [RelayCommand] public async Task ReportClearAsync()     => await PostIntelAsync(IntelType.Clear,    "System clear");

        private async Task PostIntelAsync(IntelType type, string notes)
        {
            try
            {
                var system = string.IsNullOrEmpty(SolarSystem) ? "Unknown" : SolarSystem;
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
            catch (Exception ex) { ConnectionStatus = $"⚠️ {Loc.ErrAssignFailed}: {ex.Message[..Math.Min(ex.Message.Length,30)]}"; }
        }

        [RelayCommand]
        public async Task CompleteMissionAsync(int missionId)
        {
            try   { await _api.CompleteMissionAsync(missionId); await RefreshAsync(); }
            catch (Exception ex) { ConnectionStatus = $"⚠️ {Loc.ErrCompleteFailed}: {ex.Message[..Math.Min(ex.Message.Length,30)]}"; }
        }

        // ----------------------------------------------------------------
        // Click-through state (called from code-behind)
        // ----------------------------------------------------------------
        public void ToggleClickThrough(bool isNowClickThrough)
        {
            IsClickThrough    = isNowClickThrough;
            ClickThroughLabel = isNowClickThrough
                ? (Loc.Language == OverlayLanguage.EN ? "👁️ Click-Through ON" : "👁️ Clic-Passant ACTIF")
                : (Loc.Language == OverlayLanguage.EN ? "🖱️ Interactive"     : "🖱️ Interactif");
        }

        // ---- Faction save ----
        partial void OnFactionFocusChanged(string value)
        {
            _cfg.FactionFocus = value;
            _cfg.Save(_db);
        }

        public void Dispose()
        {
            _pollCts?.Cancel();
            _pollCts?.Dispose();
            _api.Dispose();
        }
    }
}
