using System.Diagnostics;

namespace TmGui.ViewModels;

using System.Threading.Tasks;
using Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class TargetInfoViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string SerialNumber { get; private set; }
    
    [ObservableProperty]
    public partial string Address { get; private set; }
    
    [ObservableProperty]
    public partial string ConnectionType { get; private set; }
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    [NotifyPropertyChangedFor(nameof(IsDisconnected))]
    [NotifyPropertyChangedFor(nameof(ConnOptionString))]
    public partial TargetStatus Status { get; private set; }
    
    [ObservableProperty]
    public partial bool IsConnecting { get; private set; }
    
    [ObservableProperty]
    public partial bool IsDisconnecting { get; private set; }
    
    [ObservableProperty]
    public partial TargetResult ConnectResult { get; private set; }

    public bool IsConnected => Status == TargetStatus.Connected;
    public bool IsDisconnected => Status == TargetStatus.Disconnected;
    
    public string ConnOptionString => Status == TargetStatus.Disconnected ? "Connect" : "Disconnect";

    public TargetInfoViewModel(TargetInfo info)
    {
        Update(info);
    }

    public void Update(TargetInfo info)
    {
        /* Copy infos over. */
        SerialNumber   = info.SerialNumber;
        Address        = info.Address;
        ConnectionType = info.ConnectionType;
        Status         = info.Status;
    }
    
    public async Task ConnectAsync()
    {
        string serial = SerialNumber;
        ConnectionList connList = ConnectionList.Get();
        
        /* Set connecting flag. */
        IsConnecting = true;
        
        /* If this target is already connected (shouldn't be possible?), do nothing. */
        if (connList.IsConnected(serial))
        {
            IsConnecting = false;
            return;
        }
        
        /* Attempt to connect to the target. */
        TargetProxy targ = new(serial);
        var (result, info) = await targ.ConnectAsync();
        
        /* Update connection result. */
        ConnectResult = result;
        
        /* If successful, register the connection. */
        if (result == TargetResult.Success)
        {
            connList.Register(serial, targ);
            Update(info);
        }

        /* We're done connecting, indicate so. */
        IsConnecting = false;
    }

    public async Task DisconnectAsync()
    {
        string serial = SerialNumber;
        ConnectionList connList = ConnectionList.Get();
        
        /* Signal that we're disconnecting. */
        IsDisconnecting = true;
        
        /* If this target is already disconnected, do nothing. */
        if (!connList.IsConnected(serial))
        {
            IsDisconnecting = false;
            return;
        }
        
        /* Get the proxy object. */
        TargetProxy? targ = connList.GetTarget(serial);
        Debug.Assert(targ != null);
        
        /* Unregister the target. */
        bool dis = connList.Unregister(serial);
        Debug.Assert(dis);    
    
        /* Actually disconnect. */
        await targ.DisconnectAsync();
        
        /* Set our status to disconnected. */
        Status = TargetStatus.Disconnected;
        
        /* We're done disconnecting, indicate so. */
        IsConnecting = false;
    }
}