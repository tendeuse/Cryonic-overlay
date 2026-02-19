// filename: ViewModels/FirstRunViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace OverlayMVP.ViewModels
{
    public partial class FirstRunViewModel : ObservableObject
    {
        [ObservableProperty] private string apiBaseUrl = "";
        [ObservableProperty] private string apiKey = "";
        [ObservableProperty] private string alphaOmega = "ALPHA";
        [ObservableProperty] private string factionFocus = "CALDARI";
        [ObservableProperty] private string status = "Enter your bot API URL and overlay key. You can change faction focus later.";

        public bool Validate(out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(ApiBaseUrl) || !ApiBaseUrl.StartsWith("http"))
            {
                error = "API Base URL must start with http/https (example: https://yourbot.up.railway.app).";
                return false;
            }
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                error = "API Key is required (must match OVERLAY_API_KEY on Railway).";
                return false;
            }
            return true;
        }
    }
}
