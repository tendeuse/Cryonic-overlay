// filename: Services/OverlayConfig.cs
namespace OverlayMVP.Services
{
    public sealed class OverlayConfig
    {
        public string ApiBaseUrl { get; set; } = "";
        public string OverlayToken { get; set; } = "";
        public string AlphaOmega { get; set; } = "ALPHA";
        public string FactionFocus { get; set; } = "CALDARI";

        // Feature toggles
        public bool ShowMultibox        { get; set; } = true;
        public bool ShowIntelAlerts     { get; set; } = true;
        public bool ShowPilotStatus     { get; set; } = true;
        public bool ShowActiveMissions  { get; set; } = true;
        public bool ShowStandingGuide   { get; set; } = true;
        public bool ShowMissionProgress { get; set; } = true;
        public bool ShowSkillPlan       { get; set; } = true;
        public bool ShowPilotIntelBtn   { get; set; } = true;
        public bool ShowSystemInfoBtn   { get; set; } = true;

        public static OverlayConfig? Load(AppDb db)
        {
            using var con = db.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"SELECT api_base_url, overlay_token, alpha_omega, faction_focus,
                show_multibox, show_intel_alerts, show_pilot_status, show_active_missions,
                show_standing_guide, show_mission_progress, show_skill_plan,
                show_pilot_intel_btn, show_system_info_btn
                FROM overlay_config WHERE id=1";
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            return new OverlayConfig
            {
                ApiBaseUrl          = r.GetString(0),
                OverlayToken        = r.GetString(1),
                AlphaOmega          = r.GetString(2),
                FactionFocus        = r.GetString(3),
                ShowMultibox        = r.IsDBNull(4)  ? true : r.GetInt32(4)  != 0,
                ShowIntelAlerts     = r.IsDBNull(5)  ? true : r.GetInt32(5)  != 0,
                ShowPilotStatus     = r.IsDBNull(6)  ? true : r.GetInt32(6)  != 0,
                ShowActiveMissions  = r.IsDBNull(7)  ? true : r.GetInt32(7)  != 0,
                ShowStandingGuide   = r.IsDBNull(8)  ? true : r.GetInt32(8)  != 0,
                ShowMissionProgress = r.IsDBNull(9)  ? true : r.GetInt32(9)  != 0,
                ShowSkillPlan       = r.IsDBNull(10) ? true : r.GetInt32(10) != 0,
                ShowPilotIntelBtn   = r.IsDBNull(11) ? true : r.GetInt32(11) != 0,
                ShowSystemInfoBtn   = r.IsDBNull(12) ? true : r.GetInt32(12) != 0,
            };
        }

        public void Save(AppDb db)
        {
            using var con = db.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
INSERT INTO overlay_config(id, api_base_url, overlay_token, alpha_omega, faction_focus,
  show_multibox, show_intel_alerts, show_pilot_status, show_active_missions,
  show_standing_guide, show_mission_progress, show_skill_plan,
  show_pilot_intel_btn, show_system_info_btn)
VALUES(1, $url, $tok, $ao, $ff, $smb, $sia, $sps, $sam, $ssg, $smp, $ssk, $spib, $ssib)
ON CONFLICT(id) DO UPDATE SET
  api_base_url=excluded.api_base_url,
  overlay_token=excluded.overlay_token,
  alpha_omega=excluded.alpha_omega,
  faction_focus=excluded.faction_focus,
  show_multibox=excluded.show_multibox,
  show_intel_alerts=excluded.show_intel_alerts,
  show_pilot_status=excluded.show_pilot_status,
  show_active_missions=excluded.show_active_missions,
  show_standing_guide=excluded.show_standing_guide,
  show_mission_progress=excluded.show_mission_progress,
  show_skill_plan=excluded.show_skill_plan,
  show_pilot_intel_btn=excluded.show_pilot_intel_btn,
  show_system_info_btn=excluded.show_system_info_btn
";
            cmd.Parameters.AddWithValue("$url",  ApiBaseUrl.Trim().TrimEnd('/'));
            cmd.Parameters.AddWithValue("$tok",  OverlayToken.Trim());
            cmd.Parameters.AddWithValue("$ao",   AlphaOmega.Trim().ToUpperInvariant());
            cmd.Parameters.AddWithValue("$ff",   FactionFocus.Trim().ToUpperInvariant());
            cmd.Parameters.AddWithValue("$smb",  ShowMultibox        ? 1 : 0);
            cmd.Parameters.AddWithValue("$sia",  ShowIntelAlerts     ? 1 : 0);
            cmd.Parameters.AddWithValue("$sps",  ShowPilotStatus     ? 1 : 0);
            cmd.Parameters.AddWithValue("$sam",  ShowActiveMissions  ? 1 : 0);
            cmd.Parameters.AddWithValue("$ssg",  ShowStandingGuide   ? 1 : 0);
            cmd.Parameters.AddWithValue("$smp",  ShowMissionProgress ? 1 : 0);
            cmd.Parameters.AddWithValue("$ssk",  ShowSkillPlan       ? 1 : 0);
            cmd.Parameters.AddWithValue("$spib", ShowPilotIntelBtn   ? 1 : 0);
            cmd.Parameters.AddWithValue("$ssib", ShowSystemInfoBtn   ? 1 : 0);
            cmd.ExecuteNonQuery();
        }
    }
}
