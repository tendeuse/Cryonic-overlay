// filename: ViewModels/MainViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
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
        // ── Services ──────────────────────────────────────────────────────
        private readonly AppDb              _db;
        public  AppDb         Db     => _db;
        public  OverlayConfig Config => _cfg;
        private readonly OverlayApiClient   _api;
        private readonly OverlayConfig      _cfg;
        private readonly EsiClient          _esi;
        public  SessionTracker Session { get; }
        private readonly BackendSession     _backend;
        private readonly IntelSearchService _intel = new();
        private readonly EveLogWatcher      _logWatcher = new();
        private CancellationTokenSource?    _pollCts;
        private bool                        _autoSwitchingCharacter = false;

        private const int PollingIntervalMs        = 10_000;
        private const int EveWindowRefreshMs       = 3_000;
        private const int AP_PER_MISSION           = 50;
        private const int SponsorVersionIntervalMs = 3_600_000; // hourly

        // ── Localization ──────────────────────────────────────────────────
        public LocalizationManager Loc => LocalizationManager.Instance;

        // ── App version (shown in the footer) ───────────────────────────────
        public string AppVersion => "Alpha · v" + (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?");

        // ── Connection / Status ───────────────────────────────────────────
        [ObservableProperty] private string connectionStatus  = "";
        [ObservableProperty] private bool   isConnected       = false;
        [ObservableProperty] private bool   isClickThrough    = false;
        [ObservableProperty] private string clickThroughLabel = "🖱️ Interactive";

        // ── Pilot / ESI ───────────────────────────────────────────────────
        [ObservableProperty] private string characterName   = "—";
        [ObservableProperty] private string corporation     = "—";
        [ObservableProperty] private string shipType        = "—";
        [ObservableProperty] private string solarSystem     = "—";
        [ObservableProperty] private string securityStatus  = "—";
        [ObservableProperty] private string securityColour  = "#FFFFFF";
        [ObservableProperty] private string eveCharacterName = "";
        [ObservableProperty] private string eveLinkStatus   = "";
        [ObservableProperty] private bool   eveLinked       = false;
        [ObservableProperty] private string currentSystem   = "";

        // ESI multi-account
        [ObservableProperty] private EsiToken? activeCharacter;
        public ObservableCollection<EsiToken> LinkedCharacters { get; } = new();
        public bool HasMultipleCharacters => LinkedCharacters.Count > 1;

        // ── Super-user / control panel ──────────────────────────────────────
        [ObservableProperty] private bool isSuperUser = false;

        /// <summary>Mints (or reuses) a backend session for the character and updates
        /// IsSuperUser from the resulting role claim (global/coalition/ceo).</summary>
        private async Task RefreshSuperUserAsync(EsiToken character)
        {
            try { await _backend.GetTokenAsync(character); }
            catch { /* leave IsSuperUser as-is on failure */ }
            Application.Current.Dispatcher.Invoke(() =>
                IsSuperUser = _backend.IsSuperUser(character.CharacterId));
        }

        partial void OnActiveCharacterChanged(EsiToken? value)
        {
            if (value is null) { IsSuperUser = false; return; }
            EveCharacterName = value.CharacterName;
            // Refresh both standings AND skills whenever the active character changes
            _ = RefreshSkillPlanAsync();
            EveLinkStatus    = $"ESI: {value.CharacterName}";
            _ = RefreshSuperUserAsync(value);
            if (_autoSwitchingCharacter) return;
            _standings.Clear();
            StandingsLoaded  = false;
            StandingsStatus  = $"⏳ Switching to {value.CharacterName}…";
            OnSelectedCatalogueFactionChanged(SelectedCatalogueFaction);
            _ = RefreshStandingsAsync();
        }

        // ── Skills ────────────────────────────────────────────────────────
        private readonly Dictionary<int, double> _standings = new();

        [ObservableProperty] private int connectionsSkill = 0;
        [ObservableProperty] private int diplomacySkill   = 0;
        [ObservableProperty] private int socialSkill      = 0;

        partial void OnConnectionsSkillChanged(int value)
        {
            SaveSkillLevel("skill_connections", value);
            RecalcEffectiveStanding();
        }
        partial void OnDiplomacySkillChanged(int value)
        {
            SaveSkillLevel("skill_diplomacy", value);
            RecalcEffectiveStanding();
        }
        partial void OnSocialSkillChanged(int value)
        {
            SaveSkillLevel("skill_social", value);
            // Social boosts standing GAINS (not effective standing), so it only
            // changes the per-mission gain and the "missions to next threshold" math.
            RefreshStandingProgress();
        }

        private void SaveSkillLevel(string key, int val)
        {
            try {
                using var con = _db.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText = "INSERT INTO meta(k,v) VALUES($k,$v) ON CONFLICT(k) DO UPDATE SET v=excluded.v";
                cmd.Parameters.AddWithValue("$k", key);
                cmd.Parameters.AddWithValue("$v", val.ToString());
                cmd.ExecuteNonQuery();
            } catch { }
        }

        private int LoadSkillLevel(string key)
        {
            try {
                using var con = _db.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText = "SELECT v FROM meta WHERE k=$k";
                cmd.Parameters.AddWithValue("$k", key);
                var raw = cmd.ExecuteScalar() as string;
                return int.TryParse(raw, out var v) ? Math.Clamp(v, 0, 5) : 0;
            } catch { return 0; }
        }

        private void RecalcEffectiveStanding()
        {
            if (SelectedCatalogueFaction is null) return;
            OnSelectedCatalogueFactionChanged(SelectedCatalogueFaction);
        }

        public float GetEffectiveStanding(float raw)
        {
            if (raw >= 0) return raw + (10f - raw) * 0.04f * ConnectionsSkill;
            else          return raw + (10f + raw) * 0.04f * DiplomacySkill;
        }

        // ── Intel ─────────────────────────────────────────────────────────
        [ObservableProperty] private string intelStatus = "";
        [ObservableProperty] private bool   hasAlerts   = false;

        // ── Missions (basic) ──────────────────────────────────────────────
        [ObservableProperty] private string missionSummary = "";
        [ObservableProperty] private int    missionCount   = 0;

        // ── Faction / standings guide ─────────────────────────────────────
        [ObservableProperty] private string factionFocus = "";
        partial void OnFactionFocusChanged(string value)
        {
            _cfg.FactionFocus = value;
            _cfg.Save(_db);
            ApplySkillPlan(value);
        }

        [ObservableProperty] private bool   standingsLoaded  = false;
        [ObservableProperty] private string standingsStatus  = "";
        [ObservableProperty] private FactionCatalogue? selectedCatalogueFaction;
        public ObservableCollection<FactionCatalogue> CatalogueFactions { get; } = new();
        public float   CurrentFactionStanding    => SelectedCatalogueFaction is null ? 0f
            : _standings.TryGetValue(SelectedCatalogueFaction.EsiFactionId, out var s) ? (float)s : 0f;
        public string  StandingLabel             => $"{GetEffectiveStanding(CurrentFactionStanding):F2}";
        public string  StandingColour            => CurrentFactionStanding >= 5 ? "#FF2ECC71"
            : CurrentFactionStanding >= 0 ? "#FFF39C12" : "#FFEF5350";

        // The single step that matches the current standing level
        public TutorialStep? CurrentCatalogueStep =>
            SelectedCatalogueFaction?.GetActiveStep(CurrentFactionStanding);

        partial void OnSelectedCatalogueFactionChanged(FactionCatalogue? value)
        {
            OnPropertyChanged(nameof(CurrentFactionStanding));
            OnPropertyChanged(nameof(StandingLabel));
            OnPropertyChanged(nameof(StandingColour));
            OnPropertyChanged(nameof(CurrentCatalogueStep));
            RefreshStandingProgress();
        }

        // ── Skill plan ────────────────────────────────────────────────────
        public ObservableCollection<SkillRequirement> CurrentSkillPlan { get; } = new();
        [ObservableProperty] private string skillPlanTitle       = "";
        [ObservableProperty] private string skillPlanShips       = "";
        [ObservableProperty] private string skillPlanNotes       = "";
        [ObservableProperty] private string skillPlanStatus      = "Link EVE character to load skills";
        [ObservableProperty] private int    skillPlanTotal       = 0;
        [ObservableProperty] private int    skillPlanDone        = 0;
        [ObservableProperty] private string copySkillPlanLabel   = "📋 Copy Plan";
        private Dictionary<int, int> _characterSkills = new();
        [ObservableProperty] private bool   isOmega          = true;
        [ObservableProperty] private string accountTypeLabel = "Ω Omega";

        // ── Feature toggles ───────────────────────────────────────────────
        [ObservableProperty] private bool showMultibox        = true;
        [ObservableProperty] private bool showIntelAlerts     = true;
        [ObservableProperty] private bool showPilotStatus     = true;
        [ObservableProperty] private bool showActiveMissions  = true;
        [ObservableProperty] private bool showStandingGuide   = true;
        [ObservableProperty] private bool showMissionProgress = true;
        [ObservableProperty] private bool showSkillPlan       = true;
        [ObservableProperty] private bool showPilotIntelBtn   = true;
        [ObservableProperty] private bool showSystemInfoBtn   = true;

        // ── Monetisation (EULA §4.4) ──────────────────────────────────────
        // COMPLIANCE: the sponsor banner is shown to EVERY user identically and
        // never interferes with app use (§4.4c). The support/Patreon link is a
        // purely voluntary donation and is NOT tied to the banner or to any
        // feature — do not gate functionality or ad-removal on donating (§4.4b/§4.2).
        // Values are server-driven (see RefreshSponsorAsync), but the banner must
        // NEVER be empty — it defaults to (and falls back to) a "house" ad for
        // this project itself when the server sponsor is disabled/unset, both at
        // startup (before the first fetch) and whenever the fetch comes back empty.
        [ObservableProperty] private bool   showSponsorBanner = true;
        [ObservableProperty] private string sponsorHeadline   = "Sponsor this slot";
        [ObservableProperty] private string sponsorSubtext    = "Contact tendeuse on Discord";
        [ObservableProperty] private string sponsorUrl        = "https://discord.gg/KndrZYnWP";
        [ObservableProperty] private string supportUrl        = "https://github.com/sponsors/tendeuse";

        // ── Update notice (server-driven; dismissible, non-blocking) ──────
        [ObservableProperty] private bool   showUpdateNotice = false;
        [ObservableProperty] private string updateVersion    = "";
        [ObservableProperty] private string updateNotes      = "";
        [ObservableProperty] private string updateUrl        = "";

        partial void OnIsOmegaChanged(bool value)
        {
            _cfg.AlphaOmega  = value ? "OMEGA" : "ALPHA";
            _cfg.Save(_db);
            AccountTypeLabel = value ? "Ω Omega" : "α Alpha";
            ApplySkillPlan();
        }

        public static IReadOnlyList<string> SkillLevels { get; } = new[] { "Level 0","Level 1","Level 2","Level 3","Level 4","Level 5" };
        public static IReadOnlyList<string> FactionKeys { get; } = new[]
        {
            // Empire
            "CALDARI","GALLENTE","AMARR","MINMATAR",
            // Neutral / special
            "SOE","CONCORD","ORE","EDENCOM","TRIGLAVIAN",
            // Pirate (all have unique faction ship hulls)
            "GURISTAS","ANGELS","BLOOD","SERPENTIS","SANSHA","MORDUS",
            // Other (unique ships available to all)
            "SOCT",
        };

        // ── Mission progress (new) ────────────────────────────────────────
        public ObservableCollection<MissionRecord>         MissionHistory   { get; } = new();
        public ObservableCollection<StandingThresholdInfo> StandingProgress { get; } = new();
        public ObservableCollection<LpEstimate>            LpEstimates      { get; } = new();
        [ObservableProperty] private string missionStatus        = "";
        [ObservableProperty] private string activeMissionName    = "";
        [ObservableProperty] private string nextThresholdLabel   = "";
        [ObservableProperty] private double nextThresholdValue   = 0;
        [ObservableProperty] private double nextThresholdProgress = 0;
        [ObservableProperty] private string missionsToNextLabel  = "";
        [ObservableProperty] private int    sessionMissionCount  = 0;
        [ObservableProperty] private int    sessionAP            = 0;

        // Regular missions raise CORPORATION standing (not faction) — this is the
        // number a "missions to go" countdown may legitimately be based on.
        [ObservableProperty] private double bestCorpStanding;
        [ObservableProperty] private string corpStandingLabel = "—";
        [ObservableProperty] private string corpMissionsToNextLabel = "";
        [ObservableProperty] private string factionStorylinesToNextLabel = "";
        [ObservableProperty] private string standingCaveatLabel =
            "Regular missions raise corporation standing only. Faction standing comes from storylines, epic arcs, COSMOS, career agents and datacenters.";

        // ── EVE Multibox ──────────────────────────────────────────────────
        [ObservableProperty] private bool   hasEveWindows  = false;
        [ObservableProperty] private string eveWindowCount = "No EVE clients detected";
        [ObservableProperty] private bool   isAnchored     = false;
        [ObservableProperty] private string anchorLabel    = "📌 Anchor to EVE";
        public IntPtr AnchoredEveHandle { get; private set; } = IntPtr.Zero;
        public event Action<IntPtr>? AnchorRequested;

        // ── Collections ───────────────────────────────────────────────────
        public ObservableCollection<Mission>     Missions   { get; } = new();
        public ObservableCollection<IntelReport> Intel      { get; } = new();
        public ObservableCollection<EveWindow>   EveWindows { get; } = new();

        // ── Orders (CEO/coalition broadcasts — read-only) ─────────────────
        public ObservableCollection<Mission> Orders { get; } = new();
        [ObservableProperty] private string ordersStatus = "";

        // ── System-change notification callback (set by MainWindow) ──────
        // Allows MainWindow to forward log-watcher system changes to SystemWindow
        // without creating a hard dependency between ViewModel and the View.
        public Action<string>? SystemChangedNotify { get; set; }

        // ── Constructor ───────────────────────────────────────────────────
        public MainViewModel(AppDb db, OverlayConfig cfg)
        {
            _db  = db;
            _cfg = cfg;
            _esi = new EsiClient(db);
            Session = new SessionTracker(db, _esi);
            Session.Start();
            _backend = new BackendSession(_esi);
            // Token provider always resolves against whichever character is active *at call
            // time* (the lambda re-reads ActiveCharacter on every invocation), so intel is
            // attributed to whoever is currently flying.
            _api = new OverlayApiClient(async forceRefresh =>
            {
                if (ActiveCharacter is null) return null;
                return await _backend.GetTokenAsync(ActiveCharacter, forceRefresh);
            });

            FactionFocus   = cfg.FactionFocus ?? string.Empty;
            ConnectionStatus = Loc.StatusConnecting;
            MissionSummary   = Loc.MissionsNone;
            IntelStatus      = Loc.IntelNone;

            // Alpha / Omega status from stored config (auto-detected on ESI skill load)
            isOmega          = cfg.AlphaOmega != "ALPHA";
            accountTypeLabel = isOmega ? "Ω Omega" : "α Alpha";

            // Feature toggles from config
            showMultibox        = cfg.ShowMultibox;
            showIntelAlerts     = cfg.ShowIntelAlerts;
            showPilotStatus     = cfg.ShowPilotStatus;
            showActiveMissions  = cfg.ShowActiveMissions;
            showStandingGuide   = cfg.ShowStandingGuide;
            showMissionProgress = cfg.ShowMissionProgress;
            showSkillPlan       = cfg.ShowSkillPlan;
            showPilotIntelBtn   = cfg.ShowPilotIntelBtn;
            showSystemInfoBtn   = cfg.ShowSystemInfoBtn;

            // Load saved skill levels
            connectionsSkill = LoadSkillLevel("skill_connections");
            diplomacySkill   = LoadSkillLevel("skill_diplomacy");
            socialSkill      = LoadSkillLevel("skill_social");

            // Build faction catalogue
            foreach (var f in MissionCatalogueData.Factions) CatalogueFactions.Add(f);

            // Wire log watcher
            _logWatcher.SystemChanged += system =>
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CurrentSystem = system;
                    SystemChangedNotify?.Invoke(system);
                });
            _logWatcher.MissionEvent += OnMissionEvent;

            // Initialise skill plan and LP estimates
            ApplySkillPlan(FactionFocus);
            RefreshLpEstimates();
        }

        // ── Lifecycle ─────────────────────────────────────────────────────
        public void StartPolling()
        {
            _pollCts = new CancellationTokenSource();
            _ = PollLoopAsync(_pollCts.Token);
            _ = EveWindowLoopAsync(_pollCts.Token);
            _ = EsiStatusLoopAsync(_pollCts.Token);
            _ = SponsorVersionLoopAsync(_pollCts.Token);
        }

        // ── Sponsor banner + update notice (server-driven, hourly) ────────
        private async Task SponsorVersionLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await RefreshSponsorAsync();
                await CheckVersionAsync();
                try { await Task.Delay(SponsorVersionIntervalMs, ct); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task RefreshSponsorAsync()
        {
            var s = await _api.GetSponsorAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (s is not null && s.Enabled && !string.IsNullOrWhiteSpace(s.Headline))
                {
                    ShowSponsorBanner = true;
                    SponsorHeadline   = s.Headline;
                    SponsorSubtext    = s.Subtext ?? "";
                    SponsorUrl        = s.Url     ?? "";
                }
                else
                {
                    // Server sponsor disabled/unset — fall back to the house ad so the
                    // slot is never blank.
                    ShowSponsorBanner = true;
                    SponsorHeadline   = "Sponsor this slot";
                    SponsorSubtext    = "Contact tendeuse on Discord";
                    SponsorUrl        = "https://discord.gg/KndrZYnWP";
                }
            });
        }

        private async Task CheckVersionAsync()
        {
            var v = await _api.GetVersionAsync();
            if (v is null || string.IsNullOrWhiteSpace(v.Version)) return;
            var local = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (!Version.TryParse(v.Version, out var remote) || local is null || remote <= local) return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateVersion = v.Version; UpdateNotes = v.Notes ?? ""; UpdateUrl = v.DownloadUrl ?? "";
                ShowUpdateNotice = true;
            });
        }

        [RelayCommand] public void DismissUpdate() => ShowUpdateNotice = false;

        private async Task PollLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await RefreshAsync();
                await LoadOrdersAsync();
                try { await Task.Delay(PollingIntervalMs, ct); }
                catch (TaskCanceledException) { break; }
            }
        }

        // ── Orders (CEO/coalition broadcasts) ──────────────────────────────
        public async Task LoadOrdersAsync()
        {
            try
            {
                var list = await _api.GetOrdersAsync(string.IsNullOrWhiteSpace(CurrentSystem) ? null : CurrentSystem);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Orders.Clear();
                    foreach (var o in list) Orders.Add(o);
                    OrdersStatus = list.Count == 0 ? "No active orders." : $"{list.Count} active";
                });
            }
            catch (Exception ex) { OrdersStatus = $"⚠️ {ex.Message}"; }
        }

        [RelayCommand]
        public async Task JoinOrderAsync(Mission? order)
        {
            if (order is null) return;
            try   { await _api.JoinOrderAsync(order.Id); OrdersStatus = $"Joined: {order.Title}"; }
            catch (Exception ex) { OrdersStatus = $"⚠️ {ex.Message}"; }
            await LoadOrdersAsync();
        }

        [RelayCommand]
        public async Task LeaveOrderAsync(Mission? order)
        {
            if (order is null) return;
            try   { await _api.LeaveOrderAsync(order.Id); OrdersStatus = $"Left: {order.Title}"; }
            catch (Exception ex) { OrdersStatus = $"⚠️ {ex.Message}"; }
            await LoadOrdersAsync();
        }

        [RelayCommand]
        public async Task CompleteOrderAsync(Mission? order)
        {
            if (order is null) return;
            // Native WPF prompt (Views/NotePromptWindow) — no Microsoft.VisualBasic dependency.
            // Returns null if the pilot cancels, in which case we do NOT mark it complete.
            string? note = Views.NotePromptWindow.Prompt(order.Title);
            if (note is null) return;
            try   { await _api.CompleteOrderAsync(order.Id, note ?? ""); OrdersStatus = $"Marked complete: {order.Title}"; }
            catch (Exception ex) { OrdersStatus = $"⚠️ {ex.Message}"; }
            await LoadOrdersAsync();
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

        private async Task EsiStatusLoopAsync(CancellationToken ct)
        {
            bool firstLoad = true;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var tokens = EsiClient.LoadTokens(_db);
                    if (tokens.Count > 0)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            LinkedCharacters.Clear();
                            foreach (var t in tokens) LinkedCharacters.Add(t);
                            OnPropertyChanged(nameof(HasMultipleCharacters));
                            if (ActiveCharacter is null || !tokens.Contains(ActiveCharacter))
                                ActiveCharacter = tokens[0];
                            EveLinked     = true;
                            EveLinkStatus = $"ESI: {ActiveCharacter.CharacterName}";
                        });

                        var charInfo = await _esi.GetCharacterAsync(ct, ActiveCharacter?.CharacterId ?? 0);
                        if (charInfo is not null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                CharacterName  = charInfo.CharacterName;
                                Corporation    = charInfo.Corporation;
                                ShipType       = string.IsNullOrEmpty(charInfo.ShipType) ? "—" : charInfo.ShipType;
                                SolarSystem    = charInfo.SolarSystem;
                                SecurityStatus = charInfo.SecurityStatus.ToString("F1");
                                SecurityColour = charInfo.SecurityColour;
                            });

                            if (firstLoad)
                            {
                                _ = RefreshStandingsAsync();
                                _ = RefreshSkillPlanAsync();
                                firstLoad = false;
                            }
                        }
                    }
                }
                catch { }
                try { await Task.Delay(30_000, ct); }
                catch (TaskCanceledException) { break; }
            }
        }

        // ── Standings ─────────────────────────────────────────────────────
        [RelayCommand]
        public async Task RefreshStandingsAsync()
        {
            if (ActiveCharacter is null) return;
            var charId   = ActiveCharacter.CharacterId;
            var charName = ActiveCharacter.CharacterName;
            try
            {
                StandingsStatus = $"⏳ Loading standings for {charName}…";
                var skillTask    = _esi.GetSkillsAsync(characterId: charId);
                var standingTask = _esi.GetStandingsAsync(characterId: charId);
                await Task.WhenAll(skillTask, standingTask);
                var (conn, dipl, soc) = await skillTask;
                var list              = await standingTask;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (conn != ConnectionsSkill) { SaveSkillLevel("skill_connections", conn); connectionsSkill = conn; OnPropertyChanged(nameof(ConnectionsSkill)); }
                    if (dipl != DiplomacySkill)   { SaveSkillLevel("skill_diplomacy",   dipl); diplomacySkill   = dipl; OnPropertyChanged(nameof(DiplomacySkill)); }
                    if (soc  != SocialSkill)      { SaveSkillLevel("skill_social",      soc);  socialSkill      = soc;  OnPropertyChanged(nameof(SocialSkill)); }

                    _standings.Clear();
                    foreach (var s in list) _standings[s.FactionId] = s.Standing;
                    StandingsLoaded = true;
                    StandingsStatus = $"✅ {list.Count} faction standings loaded — {charName}";

                    OnSelectedCatalogueFactionChanged(SelectedCatalogueFaction);
                    OnPropertyChanged(nameof(CurrentFactionStanding));
                    OnPropertyChanged(nameof(StandingLabel));
                    OnPropertyChanged(nameof(StandingColour));
                    ApplySkillPlan();
                    RefreshStandingProgress();
                    AutoAdvanceBriefings();
                });

                // Regular missions raise CORPORATION standing, not faction standing —
                // load it alongside so the threshold countdown can be driven by the
                // number that missions actually move.
                var corpStandings = await _esi.GetCorpStandingsAsync(default, charId);

                // "Corp standing" must reflect the corp the current catalogue step is
                // actually telling the pilot to grind — not the highest standing with
                // any random NPC corp, which is meaningless in this context.
                double corpValue;
                string corpLabel;
                if (corpStandings.Count == 0)
                {
                    corpValue = 0;
                    corpLabel = "—";
                }
                else
                {
                    string? cleanCorpName = CleanCorporationName(CurrentCatalogueStep?.Corporation);
                    double? matched = null;
                    if (!string.IsNullOrEmpty(cleanCorpName))
                    {
                        int corpId = await _esi.ResolveCorporationIdAsync(cleanCorpName);
                        if (corpId > 0)
                        {
                            var hit = corpStandings.FirstOrDefault(c => c.CorporationId == corpId);
                            if (hit is not null) matched = hit.Standing;
                        }
                    }

                    if (matched.HasValue && cleanCorpName is not null)
                    {
                        corpValue = matched.Value;
                        corpLabel = $"{cleanCorpName}: {corpValue:F2}";
                    }
                    else
                    {
                        corpValue = corpStandings.Max(c => c.Standing);
                        corpLabel = $"best corp: {corpValue:F2}";
                    }
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    BestCorpStanding  = corpValue;
                    CorpStandingLabel = corpLabel;
                    RefreshStandingProgress();
                });
            }
            catch (Exception ex)
            {
                StandingsStatus = $"⚠️ {ex.Message[..Math.Min(ex.Message.Length, 50)]}";
            }
        }

        /// <summary>
        /// Catalogue step corp names can be messy, e.g.
        /// "Expert Distribution (Penumbra epic arc) / Caldari Navy (L4 grind)" —
        /// take the text before the first " / " and before the first " (", trimmed.
        /// </summary>
        private static string? CleanCorporationName(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string name = raw;
            int slashIdx = name.IndexOf(" / ", StringComparison.Ordinal);
            if (slashIdx >= 0) name = name[..slashIdx];
            int parenIdx = name.IndexOf(" (", StringComparison.Ordinal);
            if (parenIdx >= 0) name = name[..parenIdx];
            name = name.Trim();
            return string.IsNullOrEmpty(name) ? null : name;
        }

        // ── ESI character ticker (single fetch on active window switch) ───
        private async Task EsiStatusLoopTickAsync(int characterId)
        {
            try
            {
                var charInfo = await _esi.GetCharacterAsync(default, characterId);
                if (charInfo is null) return;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CharacterName  = charInfo.CharacterName;
                    Corporation    = charInfo.Corporation;
                    ShipType       = string.IsNullOrEmpty(charInfo.ShipType) ? "—" : charInfo.ShipType;
                    SolarSystem    = charInfo.SolarSystem;
                    SecurityStatus = charInfo.SecurityStatus.ToString("F1");
                    SecurityColour = charInfo.SecurityColour;
                });
            }
            catch { }
        }

        // ── EVE Windows ───────────────────────────────────────────────────
        private void RefreshEveWindows()
        {
            var windows = EveWindowManager.GetEveWindows();
            Application.Current.Dispatcher.Invoke(() =>
            {
                EveWindows.Clear();
                foreach (var w in windows) EveWindows.Add(w);
                HasEveWindows  = windows.Count > 0;
                EveWindowCount = windows.Count == 0 ? "No EVE clients detected"
                    : $"{windows.Count} EVE client{(windows.Count > 1 ? "s" : "")} detected";

                if (IsAnchored && AnchoredEveHandle != IntPtr.Zero)
                {
                    if (!windows.Exists(w => w.Handle == AnchoredEveHandle)) ReleaseAnchor();
                }
                if (IsAnchored && AnchoredEveHandle != IntPtr.Zero)
                    AnchorRequested?.Invoke(AnchoredEveHandle);

                // Auto-switch ESI to active window
                var active = windows.Find(w => w.IsActive);
                if (active is not null && !string.IsNullOrEmpty(active.CharacterName))
                {
                    var match = LinkedCharacters.FirstOrDefault(
                        t => string.Equals(t.CharacterName, active.CharacterName, StringComparison.OrdinalIgnoreCase));
                    if (match is not null && match != ActiveCharacter)
                    {
                        _autoSwitchingCharacter = true;
                        ActiveCharacter  = match;
                        _autoSwitchingCharacter = false;
                        EveCharacterName = match.CharacterName;
                        CharacterName    = match.CharacterName;
                        EveLinkStatus    = $"ESI: {match.CharacterName}";
                        _ = EsiStatusLoopTickAsync(match.CharacterId);
                    }
                }
            });
        }

        // ── EVE Commands ──────────────────────────────────────────────────
        [RelayCommand] public void FocusEveWindow(EveWindow window)  => EveWindowManager.FocusWindow(window.Handle);

        [RelayCommand]
        public void AnchorToEveWindow(EveWindow window)
        {
            AnchoredEveHandle = window.Handle;
            IsAnchored        = true;
            AnchorLabel       = "📍 Anchored";
            AnchorRequested?.Invoke(window.Handle);
        }

        public void ReleaseAnchor()
        {
            AnchoredEveHandle = IntPtr.Zero;
            IsAnchored        = false;
            AnchorLabel       = "📌 Anchor to EVE";
        }

        [RelayCommand] public void ToggleLanguage() { Loc.Toggle(); OnPropertyChanged(nameof(Loc)); }

        [RelayCommand]
        public void SetManualStanding(string? value)
        {
            if (SelectedCatalogueFaction is null) return;
            if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float s))
            {
                s = Math.Clamp(s, -10f, 10f);
                _standings[SelectedCatalogueFaction.EsiFactionId] = (double)s;
                OnSelectedCatalogueFactionChanged(SelectedCatalogueFaction);
                RefreshStandingProgress();
                AutoAdvanceBriefings();
            }
        }

        [ObservableProperty] private string orderBroadcastStatus = "";

        /// <summary>
        /// Broadcast a guide step to the pilot's corporation as an Order. Only meaningful for
        /// super-users (CEO / coalition / global) — the backend authorises the target itself.
        /// </summary>
        [RelayCommand]
        public async Task BroadcastStepAsOrderAsync(TutorialStep? step)
        {
            if (step is null) return;
            try
            {
                var info = await _esi.GetCharacterAsync(default, ActiveCharacter?.CharacterId ?? 0);
                int corpId = string.IsNullOrWhiteSpace(info?.Corporation)
                    ? 0
                    : await _esi.ResolveCorporationIdAsync(info.Corporation);
                if (corpId == 0) { OrderBroadcastStatus = "⚠️ Could not determine your corporation."; return; }

                string title = string.IsNullOrWhiteSpace(step.StepLabel) ? "Standing objective" : step.StepLabel;
                string desc  = step.WhyText ?? "";
                await _api.PostOrderAsync(title, desc, "corporation", corpId);
                OrderBroadcastStatus = $"📢 Order broadcast to your corporation: {title}";
            }
            catch (Exception ex)
            {
                OrderBroadcastStatus = $"⚠️ {ex.Message}";
            }
        }

        /// <summary>Play the officer's lore briefing for a step (opaque popup, autoplay + sound).</summary>
        [RelayCommand]
        public void PlayBriefing(TutorialStep? step)
        {
            if (step is null || string.IsNullOrEmpty(step.MediaKey)) return;
            var path = System.IO.Path.Combine(
                System.AppContext.BaseDirectory, "Assets", "clips", step.MediaKey + ".mp4");
            if (!System.IO.File.Exists(path)) return;   // briefing not rendered yet — no-op
            var w = new Views.BriefingWindow(path, $"◈ TRANSMISSION — {step.StepLabel}");
            w.Show();
        }

        // ── Auto-advance briefing on standing change ──────────────────────────
        [ObservableProperty] private bool autoplayBriefingOnAdvance = true;
        private readonly Dictionary<int, string> _lastBriefingKey      = new();
        private readonly Dictionary<int, double> _lastBriefingStanding = new();

        /// <summary>
        /// When a standing update pushes the SELECTED faction into a new (later)
        /// guide step, automatically play that step's briefing. The first time a
        /// faction is seen only records a baseline — it never autoplays on the
        /// initial standings load, and never on a mere faction-selection change.
        /// </summary>
        private void AutoAdvanceBriefings()
        {
            var f = SelectedCatalogueFaction;
            if (f is null) return;
            int    fid    = f.EsiFactionId;
            double cur    = CurrentFactionStanding;
            var    step   = f.GetActiveStep((float)cur);
            string curKey = step?.MediaKey ?? "";

            bool   hadBaseline  = _lastBriefingKey.TryGetValue(fid, out var prevKey);
            double prevStanding = _lastBriefingStanding.TryGetValue(fid, out var ps) ? ps : cur;

            _lastBriefingKey[fid]      = curKey;
            _lastBriefingStanding[fid] = cur;

            if (!AutoplayBriefingOnAdvance) return;
            if (!hadBaseline) return;                       // first sighting — baseline only
            if (string.IsNullOrEmpty(curKey) || curKey == prevKey) return;
            if (cur <= prevStanding) return;                // only on forward progression
            PlayBriefing(step);
        }

        [RelayCommand]
        public void CopyCurrentStepInfo()
        {
            var step    = CurrentCatalogueStep;
            var faction = SelectedCatalogueFaction;
            if (step is null || faction is null) return;
            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"Faction:     {faction.FactionName}");
            lines.AppendLine($"Step:        {step.StepLabel}");
            if (!string.IsNullOrWhiteSpace(step.Corporation))
                lines.AppendLine($"Corporation: {step.Corporation}");
            if (!string.IsNullOrWhiteSpace(step.Agent))
                lines.AppendLine($"Agent:       {step.Agent}");
            if (!string.IsNullOrWhiteSpace(step.Station))
                lines.AppendLine($"Location:    {step.Station}");
            if (!string.IsNullOrWhiteSpace(step.Region))
                lines.AppendLine($"Region:      {step.Region}");
            lines.AppendLine($"Standing:    {CurrentFactionStanding:+0.00;-0.00;0.00}  →  goal {step.StandingGoal:+0.0;-0.0;0.0}");
            System.Windows.Clipboard.SetText(lines.ToString().TrimEnd());
        }

    [RelayCommand]
    public void OpenShipFit(string? hullName)
    {
        if (string.IsNullOrWhiteSpace(hullName)) return;
        // Find the spec for this hull across all catalogue factions
        ShipSpec? spec = null;
        foreach (var faction in MissionCatalogueData.Factions)
        {
            foreach (var step in faction.Steps)
            {
                if (step.Spec is { } s && System.Array.Exists(s.Ships,
                    h => string.Equals(h, hullName, System.StringComparison.OrdinalIgnoreCase)))
                {
                    spec = s;
                    break;
                }
            }
            if (spec != null) break;
        }
        var w = new Views.ShipFitWindow(hullName, spec);
        w.Show();
    }

        // ── Intel Commands ────────────────────────────────────────────────
        [RelayCommand] public async Task ReportGateCampAsync() => await PostIntelAsync(IntelType.GateCamp, "Gate camp reported");
        [RelayCommand] public async Task ReportPiratesAsync()  => await PostIntelAsync(IntelType.Pirate,   "Pirates reported");
        [RelayCommand] public async Task ReportRoamingAsync()  => await PostIntelAsync(IntelType.Roaming,  "Roaming gang reported");

        private async Task PostIntelAsync(IntelType type, string notes)
        {
            if (ActiveCharacter is null)
            {
                IntelStatus = "Link a character first";
                return;
            }

            var system = string.IsNullOrEmpty(CurrentSystem) ? SolarSystem : CurrentSystem;
            if (string.IsNullOrEmpty(system)) system = "Unknown";

            var backendType = type switch
            {
                IntelType.GateCamp => "gate_camp",
                IntelType.Pirate   => "hostiles",
                IntelType.Roaming  => "roaming",
                _                  => "gate_camp"
            };

            try
            {
                await _api.PostIntelAsync(system, backendType, notes);
                await RefreshAsync();
            }
            catch (OverlayApiException ex) when (ex.StatusCode == 403)
            {
                IntelStatus = "Account too new to post intel";
            }
            catch (Exception ex)
            {
                IntelStatus = $"⚠️ {ex.Message[..Math.Min(ex.Message.Length, 60)]}";
            }
        }

        // ── ESI Link/Unlink ───────────────────────────────────────────────
        [RelayCommand]
        public async Task LinkEveAsync(string? clientId = null)
        {
            try
            {
                var url = await _esi.AuthorizeAsync();
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
                    { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                EveLinkStatus = $"⚠️ {ex.Message[..Math.Min(ex.Message.Length, 40)]}";
            }
        }

        [RelayCommand]
        public async Task UnlinkEveAsync(int characterId = 0)
        {
            if (characterId > 0)
            {
                EsiClient.DeleteToken(_db, characterId);
                var token = LinkedCharacters.FirstOrDefault(t => t.CharacterId == characterId);
                if (token is not null)
                {
                    LinkedCharacters.Remove(token);
                    OnPropertyChanged(nameof(HasMultipleCharacters));
                }
                if (ActiveCharacter?.CharacterId == characterId)
                    ActiveCharacter = LinkedCharacters.FirstOrDefault();
            }
            else
            {
                EsiClient.DeleteAllTokens(_db);
                LinkedCharacters.Clear();
                ActiveCharacter  = null;
                EveLinked        = false;
                EveLinkStatus    = "";
                EveCharacterName = "";
                CharacterName    = "—";
                _standings.Clear();
            }
            await Task.CompletedTask;
        }

        public void SelectNextCharacter()
        {
            if (LinkedCharacters.Count < 2) return;
            int idx = ActiveCharacter is null ? 0
                : (LinkedCharacters.IndexOf(ActiveCharacter) + 1) % LinkedCharacters.Count;
            ActiveCharacter = LinkedCharacters[idx];
        }

        public void SelectPrevCharacter()
        {
            if (LinkedCharacters.Count < 2) return;
            int idx = ActiveCharacter is null ? 0
                : (LinkedCharacters.IndexOf(ActiveCharacter) - 1 + LinkedCharacters.Count) % LinkedCharacters.Count;
            ActiveCharacter = LinkedCharacters[idx];
        }

        // ── Mission Event (from Gamelog detection) ────────────────────────
        private void OnMissionEvent(string charName, string missionName, string eventType)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (eventType == "accepted")
                {
                    ActiveMissionName = missionName;
                    MissionStatus = $"📋 Active: {missionName}";
                }
                else if (eventType == "completed")
                {
                    ActiveMissionName = "";
                    SessionMissionCount++;
                    SessionAP += AP_PER_MISSION;

                    double rawStanding = CurrentFactionStanding;
                    // Mission GAINS scale with the Social skill (+5%/level), not Connections.
                    double gain = MissionProgressService.StandingGainPerMission(rawStanding, SocialSkill);

                    var record = new MissionRecord
                    {
                        CharacterName = charName,
                        MissionName   = missionName,
                        FactionKey    = FactionFocus,
                        MissionLevel  = 4,
                        CompletedAt   = DateTime.UtcNow,
                        StandingGain  = gain,
                        EstimatedLP   = StandingThresholds.LpByLevel.TryGetValue(4, out var lp)
                                        ? (lp.minLP + lp.maxLP) / 2 : 0,
                    };
                    MissionHistory.Insert(0, record);
                    if (MissionHistory.Count > 50) MissionHistory.RemoveAt(50);
                    MissionStatus = $"✅ {missionName}  (+{gain:F3} standing)  +{AP_PER_MISSION} AP";
                    _ = SendMissionApAsync(record);
                    RefreshStandingProgress();
                }
            });
        }

        // ── Standing Progress ─────────────────────────────────────────────
        /// <summary>Clamp big-but-finite grind counts so no raw cap-sized number reaches the UI.</summary>
        private static string FormatCount(int n) => n > 999 ? "999+" : n.ToString();

        public void RefreshStandingProgress()
        {
            // Thresholds/progress are driven by CORPORATION standing, because that is
            // what completing regular missions actually raises. Faction standing is
            // tracked separately and only advances via storyline/epic-arc content.
            double corp    = BestCorpStanding;
            double faction = CurrentFactionStanding;

            var thresholds = MissionProgressService.GetThresholds(corp, SocialSkill);
            StandingProgress.Clear();
            foreach (var t in thresholds) StandingProgress.Add(t);

            var next = thresholds.FirstOrDefault(t => t.IsNext);
            if (next is not null)
            {
                NextThresholdLabel    = next.Label;
                NextThresholdValue    = next.Value;
                NextThresholdProgress = next.ProgressToThis;
                MissionsToNextLabel   = next.MissionsLabel;

                // 10.0 is an asymptotically-unreachable display ceiling, never a grind
                // target — and MissionsToTarget/StorylinesToTarget return -1 ("not
                // reachable") whenever the iteration cap is hit, which must never leak
                // a raw count into the UI. Both cases collapse to "no countdown".
                bool nextIsCeiling = next.Value >= 10.0;

                CorpMissionsToNextLabel = (nextIsCeiling || string.IsNullOrEmpty(next.MissionsLabel))
                    ? ""
                    : $"{next.MissionsLabel} → {next.Label} (corp standing)";

                int storylines = MissionProgressService.StorylinesToTarget(faction, next.Value, SocialSkill);
                if (nextIsCeiling || storylines < 0)
                {
                    FactionStorylinesToNextLabel = "";
                }
                else if (storylines == 0)
                {
                    FactionStorylinesToNextLabel = "Faction standing already at this threshold";
                }
                else
                {
                    string sCount = FormatCount(storylines);
                    string mCount = FormatCount(storylines * MissionProgressService.MissionsPerStoryline);
                    FactionStorylinesToNextLabel =
                        $"{sCount} storyline{(storylines == 1 ? "" : "s")} " +
                        $"(≈{mCount} missions) → faction {next.Value:F1}";
                }
            }
            else
            {
                NextThresholdLabel           = "✅ Max standing reached!";
                NextThresholdProgress        = 1.0;
                MissionsToNextLabel          = "";
                CorpMissionsToNextLabel      = "";
                FactionStorylinesToNextLabel = "";
            }
        }

        public void RefreshLpEstimates()
        {
            LpEstimates.Clear();
            foreach (var e in MissionProgressService.GetLpEstimates()) LpEstimates.Add(e);
        }

        // ── Send AP to bot ────────────────────────────────────────────────
        private async Task SendMissionApAsync(MissionRecord record)
        {
            try
            {
                var token = _cfg.OverlayToken;
                if (string.IsNullOrEmpty(token)) return;
                var payload = new { character_name = record.CharacterName, mission_name = record.MissionName,
                    faction = record.FactionKey, level = record.MissionLevel,
                    standing_gain = record.StandingGain, ap = AP_PER_MISSION };
                var json = JsonSerializer.Serialize(payload);
                var req  = new HttpRequestMessage(HttpMethod.Post,
                    $"{_cfg.ApiBaseUrl}/overlay/api/v1/mission_complete")
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var resp = await http.SendAsync(req);
                record.SentToBot = resp.IsSuccessStatusCode;
            }
            catch { }
        }

        // ── Skill Plan ────────────────────────────────────────────────────
        public void ApplySkillPlan(string? factionKey = null)
        {
            var key  = factionKey ?? FactionFocus;
            var plan = FactionSkillPlans.GetByKey(key);
            if (plan is null) { CurrentSkillPlan.Clear(); return; }
            SkillPlanTitle = $"{plan.FactionName} — Skill Plan";
            SkillPlanShips = plan.Ships;
            SkillPlanNotes = plan.Notes;
            CurrentSkillPlan.Clear();
            foreach (var tmpl in plan.Skills)
            {
                // Always create a FRESH copy — never mutate the shared static template objects.
                // Mutating the static list caused stale TrainedLevel values (e.g. 0 instead of 2)
                // if ApplySkillPlan was called before ESI data arrived and the objects were reused.
                int trained = _characterSkills.TryGetValue(tmpl.SkillId, out var lvl) ? lvl : -1;
                CurrentSkillPlan.Add(new SkillRequirement
                {
                    SkillId          = tmpl.SkillId,
                    SkillName        = tmpl.SkillName,
                    Category         = tmpl.Category,
                    RecommendedLevel = tmpl.RecommendedLevel,
                    WhyText          = tmpl.WhyText,
                    AlphaCap         = tmpl.AlphaCap,
                    TrainedLevel     = trained,
                    IsOmega          = IsOmega,
                });
            }
            UpdateSkillPlanProgress();
        }

        private void UpdateSkillPlanProgress()
        {
            // Omega-only skills (AlphaCap == -1) don't count toward the total for Alpha players
            var relevant   = CurrentSkillPlan.Where(s => !s.IsOmegaOnly || IsOmega).ToList();
            SkillPlanTotal = relevant.Count;
            SkillPlanDone  = relevant.Count(s => s.IsMaxed);
            string esiInfo = _characterSkills.Count > 0 ? $"  ·  {_characterSkills.Count} ESI skills" : "  ·  ESI not loaded";
            SkillPlanStatus = SkillPlanTotal == 0 ? "No plan" :
                SkillPlanDone == SkillPlanTotal ? $"✅ All {SkillPlanTotal} skills at recommended level!{esiInfo}" :
                $"{SkillPlanDone}/{SkillPlanTotal} skills ready{esiInfo}";
        }

        [RelayCommand]
        private void CopySkillPlan()
        {
            if (CurrentSkillPlan.Count == 0) return;
            var sb = new System.Text.StringBuilder();
            foreach (var skill in CurrentSkillPlan)
            {
                // Skip Omega-only skills when on Alpha — they can't be trained
                if (skill.IsOmegaOnly && !IsOmega) continue;
                sb.AppendLine($"{skill.SkillName} {skill.RecommendedLevel}");
            }
            try
            {
                Clipboard.SetText(sb.ToString().TrimEnd());
                CopySkillPlanLabel = "✅ Copied!";
                // Reset label after 2 seconds
                var timer = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (_, _) => { CopySkillPlanLabel = "📋 Copy Plan"; timer.Stop(); };
                timer.Start();
            }
            catch { }
        }

        [RelayCommand]
        public async Task RefreshSkillPlanAsync()
        {
            if (ActiveCharacter is null) { SkillPlanStatus = "No character linked"; return; }
            SkillPlanStatus = "⏳ Loading skills from ESI…";
            try
            {
                var skills = await _esi.GetAllSkillsAsync(characterId: ActiveCharacter.CharacterId);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (skills.Count == 0) { SkillPlanStatus = "⚠️ Re-link EVE in ⚙ Settings (new scope required)"; return; }
                    _characterSkills = skills;
                    SkillPlanStatus  = $"⏳ {skills.Count} skills loaded — applying…";

                    // Auto-detect Alpha/Omega from trained skills
                    bool detected = EsiClient.DetectIsOmega(skills);
                    if (detected != IsOmega)
                    {
                        // Update backing field directly to avoid triggering OnIsOmegaChanged
                        // (which calls ApplySkillPlan) before _characterSkills is fully set
                        isOmega          = detected;
                        _cfg.AlphaOmega  = detected ? "OMEGA" : "ALPHA";
                        _cfg.Save(_db);
                        AccountTypeLabel = detected ? "Ω Omega" : "α Alpha";
                        OnPropertyChanged(nameof(IsOmega));
                    }

                    if (skills.TryGetValue(3359, out var conn)) { SaveSkillLevel("skill_connections", conn); connectionsSkill = conn; OnPropertyChanged(nameof(ConnectionsSkill)); }
                    if (skills.TryGetValue(3357, out var dipl)) { SaveSkillLevel("skill_diplomacy",   dipl); diplomacySkill   = dipl; OnPropertyChanged(nameof(DiplomacySkill)); }
                    if (skills.TryGetValue(3355, out var soc))  { SaveSkillLevel("skill_social",      soc);  socialSkill      = soc;  OnPropertyChanged(nameof(SocialSkill)); }
                    ApplySkillPlan();
                    RecalcEffectiveStanding();
                });
            }
            catch (Exception ex)
            {
                SkillPlanStatus = ex.Message.Contains("403") || ex.Message.Contains("Forbidden")
                    ? "⚠️ Re-link EVE in ⚙ Settings — new ESI scope required"
                    : $"⚠️ {ex.Message[..Math.Min(ex.Message.Length, 50)]}";
            }
        }

        // ── Feature flags ─────────────────────────────────────────────────
        public void ApplyFeatureFlags()
        {
            ShowMultibox        = _cfg.ShowMultibox;
            ShowIntelAlerts     = _cfg.ShowIntelAlerts;
            ShowPilotStatus     = _cfg.ShowPilotStatus;
            ShowActiveMissions  = _cfg.ShowActiveMissions;
            ShowStandingGuide   = _cfg.ShowStandingGuide;
            ShowMissionProgress = _cfg.ShowMissionProgress;
            ShowSkillPlan       = _cfg.ShowSkillPlan;
            ShowPilotIntelBtn   = _cfg.ShowPilotIntelBtn;
            ShowSystemInfoBtn   = _cfg.ShowSystemInfoBtn;
        }

        // ── Click-through ─────────────────────────────────────────────────
        public void ToggleClickThrough(bool isNowClickThrough)
        {
            IsClickThrough    = isNowClickThrough;
            ClickThroughLabel = isNowClickThrough ? "👁️ Click-Through ON" : "🖱️ Interactive";
        }

        // ── Refresh (intel feed) ────────────────────────────────────────────
        private async Task RefreshAsync()
        {
            try
            {
                List<IntelReport>? intel = string.IsNullOrEmpty(CurrentSystem)
                    ? null
                    : await _api.GetIntelAsync(CurrentSystem);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsConnected      = true;
                    ConnectionStatus = $"✅ Online  •  {DateTime.Now:HH:mm:ss}";

                    if (intel is not null)
                    {
                        HasAlerts   = intel.Count > 0;
                        IntelStatus = intel.Count == 0 ? Loc.IntelNone
                            : $"⚠ {intel.Count} active report(s)";
                        Intel.Clear();
                        foreach (var i in intel) Intel.Add(i);
                    }
                    // Orders are loaded separately by LoadOrdersAsync (called from PollLoopAsync).
                });
            }
            catch
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsConnected      = false;
                    ConnectionStatus = "⚠️ Offline";
                });
            }
        }

        // ── Dispose ───────────────────────────────────────────────────────
        public void Dispose()
        {
            _pollCts?.Cancel();
            _pollCts?.Dispose();
            _api.Dispose();
            _backend.Dispose();
            _esi.Dispose();
            _intel.Dispose();
            _logWatcher.Dispose();
            Session.Dispose();
        }
    }
}
