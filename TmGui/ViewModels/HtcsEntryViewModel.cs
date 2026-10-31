using System.Net;

namespace TmGui.ViewModels;

using System;
using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.ComponentModel;

public partial class HtcsEntryViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string PeerName { get; private set; }
    
    [ObservableProperty]
    public partial string PortName { get; private set; }
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LocalAddress))]
    public partial string LocalIp { get; private set; }
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LocalAddress))]
    public partial string LocalPort { get; private set; }

    public string LocalAddress => $"{LocalIp}:{LocalPort}";

    public HtcsEntryViewModel(htcs.PortInfo info)
    {
        Update(info);
    }
    
    public void Update(htcs.PortInfo info)
    {
        PeerName  = info.PeerName;
        PortName  = info.PortName;
        LocalIp   = new IPAddress(info.Ip).ToString();
        LocalPort = info.Port.ToString();
    }
}