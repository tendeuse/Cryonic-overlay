// filename: ViewModels/FirstRunViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace OverlayMVP.ViewModels
{
    public partial class FirstRunViewModel : ObservableObject
    {
        [ObservableProperty] private string apiBaseUrl = "";
        [ObservableProperty] private string pairCode = "";
        [ObservableProperty] private string alphaOmega = "ALPHA";
        [ObservableProperty] private string factionFocus = "CALDARI";
        [ObservableProperty] private string status = "Run /overlay pair in Discord to get a code, then paste it here.";

        public bool Validate(out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(ApiBaseUrl) || !ApiBaseUrl.StartsWith("http"))
            {
                error = "API Base URL must start with http/https (example: https://yourbot.up.railway.app).";
                return false;
            }
            if (string.IsNullOrWhiteSpace(PairCode) || PairCode.Trim().Length < 8)
            {
                error = "Pair Code is required. In Discord: /overlay pair, then paste the code here.";
                return false;
            }
            return true;
        }
    }
}
