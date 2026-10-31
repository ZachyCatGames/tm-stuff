using CommunityToolkit.Mvvm.ComponentModel;

namespace TmGui.ViewModels;

public partial class TargetInfoViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string SerialNumber { get; private set; }
    
    [ObservableProperty]
    public partial string Address { get; private set; }
    
    [ObservableProperty]
    public partial string ConnectionType { get; private set; }
    
    [ObservableProperty]
    public partial TargetStatus Status { get; private set; }

    public TargetInfoViewModel(TargetInfo info)
    {
        /* Copy infos over. */
        SerialNumber   = info.SerialNumber;
        Address        = info.Address;
        ConnectionType = info.ConnectionType;
        Status         = info.Status;
    }
}