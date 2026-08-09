// filename: ViewModels/FirstRunViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;

using OverlayMVP.Services;

namespace OverlayMVP.ViewModels
{
    public partial class FirstRunViewModel : ObservableObject
    {
        /// <summary>Translations, for {Binding Loc.X} in FirstRunWindow.xaml.</summary>
        public LocalizationManager Loc => LocalizationManager.Instance;

        [ObservableProperty] private string alphaOmega = "ALPHA";
        [ObservableProperty] private string factionFocus = "CALDARI";
        [ObservableProperty] private string status = "Log in with your EVE character to get started.";
    }
}
