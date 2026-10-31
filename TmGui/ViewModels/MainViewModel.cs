using CommunityToolkit.Mvvm.ComponentModel;
using TmGui.Models;

namespace TmGui.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public TargetListViewModel TargetList { get; init; }

    public MainViewModel(TargetManagerProxy proxy)
    {
        TargetList = new TargetListViewModel(proxy);
    }
    
    [ObservableProperty] public partial string Greeting { get; set; } = "Welcome to Avalonia!";
}