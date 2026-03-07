// filename: Models/MissionModels.cs
using System;
using System.Collections.Generic;

namespace OverlayMVP.Models
{
    // -----------------------------------------------------------------------
    // Missions (from Discord bot API)
    // -----------------------------------------------------------------------

    public sealed class Mission
    {
        public int    Id          { get; set; }
        public string Title       { get; set; } = "";
        public string Description { get; set; } = "";
        public string Reward      { get; set; } = "";
        public string Status      { get; set; } = "open";       // open | in_progress | completed | cancelled
        public string CreatedBy   { get; set; } = "";
        public string AssignedTo  { get; set; } = "";
        public string CreatedAt   { get; set; } = "";
        public string UpdatedAt   { get; set; } = "";

        public string StatusEmoji => Status switch
        {
            "open"        => "🔵",
            "in_progress" => "🟠",
            "completed"   => "✅",
            "cancelled"   => "❌",
            _             => "❓"
        };

        public string StatusLabel => Status.Replace("_", " ").ToUpperInvariant();
    }

    public sealed class MissionListResponse
    {
        public List<Mission> Missions { get; set; } = new();
    }

    // -----------------------------------------------------------------------
    // EVE Character (from ESI via backend proxy)
    // -----------------------------------------------------------------------

    public sealed class CharacterInfo
    {
        public long   CharacterId   { get; set; }
        public string CharacterName { get; set; } = "Unknown Pilot";
        public string Corporation   { get; set; } = "";
        public string Alliance      { get; set; } = "";
        public string ShipName      { get; set; } = "";
        public string ShipType      { get; set; } = "";
        public string SolarSystem   { get; set; } = "";
        public string Region        { get; set; } = "";
        public float  SecurityStatus { get; set; }

        public string SecurityColour => SecurityStatus switch
        {
            >= 0.5f  => "#00FF80",
            >= 0.0f  => "#FFDD00",
            >= -5.0f => "#FF8800",
            _        => "#FF3333"
        };
    }

    // -----------------------------------------------------------------------
    // Intel / Gate Camp alerts (pushed from Discord bot or submitted by pilot)
    // -----------------------------------------------------------------------

    public enum IntelType
    {
        Neutral,
        Pirate,
        GateCamp,
        Roaming,
        Clear
    }

    public sealed class IntelReport
    {
        public string    System      { get; set; } = "";
        public IntelType Type        { get; set; } = IntelType.Neutral;
        public int       Count       { get; set; } = 1;
        public string    Notes       { get; set; } = "";
        public string    ReportedBy  { get; set; } = "";
        public DateTime  ReportedAt  { get; set; } = DateTime.UtcNow;

        public string TypeEmoji => Type switch
        {
            IntelType.GateCamp => "⛔",
            IntelType.Pirate   => "💀",
            IntelType.Roaming  => "⚠️",
            IntelType.Clear    => "✅",
            _                  => "👁️"
        };

        public string TypeLabel => Type switch
        {
            IntelType.GateCamp => "GATE CAMP",
            IntelType.Pirate   => "PIRATES",
            IntelType.Roaming  => "ROAMING",
            IntelType.Clear    => "CLEAR",
            _                  => "NEUTRAL"
        };

        public string AgeLabel
        {
            get
            {
                var age = DateTime.UtcNow - ReportedAt;
                if (age.TotalSeconds < 60)  return $"{(int)age.TotalSeconds}s ago";
                if (age.TotalMinutes < 60)  return $"{(int)age.TotalMinutes}m ago";
                return $"{(int)age.TotalHours}h ago";
            }
        }
    }

    // -----------------------------------------------------------------------
    // API wrapper responses
    // -----------------------------------------------------------------------

    public sealed class OverlayDataResponse
    {
        public CharacterInfo?    Character { get; set; }
        public List<Mission>     Missions  { get; set; } = new();
        public List<IntelReport> Intel     { get; set; } = new();
    }
}
