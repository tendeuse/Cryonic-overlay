// filename: ViewModels/FirstRunViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace OverlayMVP.ViewModels
{
    public partial class FirstRunViewModel : ObservableObject
    {
        [ObservableProperty] private string alphaOmega = "ALPHA";
        [ObservableProperty] private string factionFocus = "CALDARI";
        [ObservableProperty] private string status = "Log in with your EVE character to get started.";
    }
}
