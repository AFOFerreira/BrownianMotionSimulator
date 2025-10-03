using CommunityToolkit.Mvvm.ComponentModel;

namespace BrownianMotionSimulator.ViewModel.Pages
{
    public partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty] bool isBusy;
    }
}
