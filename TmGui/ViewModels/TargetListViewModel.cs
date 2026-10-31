using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;

namespace TmGui.ViewModels;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TmGui.Models;

public partial class TargetListViewModel : ViewModelBase
{
    private TargetManagerProxy managerProxy;
    private ConnectionList connList;

    public ObservableCollection<TargetInfoViewModel> Targets { get; private set; } = new();
    
    [ObservableProperty]
    public partial TargetInfoViewModel SelectedTarget { get; set; }
    
    [ObservableProperty]
    public partial TargetResult LastRefreshResult { get; private set; }
    
    [ObservableProperty]
    public partial TargetResult ConnectResult { get; private set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; private set; }

    [ObservableProperty]
    public partial bool IsConnecting { get; private set; }
    
    [ObservableProperty]
    public partial bool IsDisconnecting { get; private set; }


    public TargetListViewModel(TargetManagerProxy proxy)
    {
        /* Init values. */
        managerProxy      = proxy;
        Targets           = new();
        LastRefreshResult = TargetResult.Success;
        IsRefreshing      = false;
        connList          = ConnectionList.Get();
        
        /* Perform initial refresh. */
        _ = RefreshTargetListImpl();
    }

    private async Task RefreshTargetListImpl()
    {
        /* Note that we're refreshing. */
        IsRefreshing = true;
        
        /* First, read the number of available targets. */
        var (r, count) = await managerProxy.QueryTargetCount(QueryType.All);
        if (r != TargetResult.Success)
        {
            LastRefreshResult = r;
            return;
        }
        
        /* Then, actually read the target infos. */
        var (r2, targs) = await managerProxy.QueryTargetInfo(QueryType.All, count);
        LastRefreshResult = r2;
        if (r2 == TargetResult.Success)
        {
            /* Get the connection list. */
            ConnectionList connList = ConnectionList.Get();
            
            /* Clear the current target list. */
            Targets.Clear();
            
            //targs.ForEach(t => Debug.WriteLine(t.SerialNumber));
            
            /* Add the newer targets to it. */
            foreach (var targ in targs)
            {
                /* Add to the list. */
                var targVm = new TargetInfoViewModel(targ);
                Targets.Add(targVm);
                
                /* Create a connection if it's connected and we don't already have one. */
                if (targ.Status == TargetStatus.Connected && !connList.IsConnected(targ.SerialNumber))
                    await ConnectImpl(targVm);
            }
        }
        
        /* Note that we're done refreshing. */
        IsRefreshing = false;
    }
    
    void OnTargetRefresh()
    {
        _ = RefreshTargetListImpl();
    }

    private async Task ConnectImpl(TargetInfoViewModel target)
    {
        /* Set connecting flag. */
        IsConnecting = true;
        
        /* Ask the target to connect. */
        await target.ConnectAsync();

        /* We're done connecting, indicate so. */
        ConnectResult = target.ConnectResult;
        IsConnecting = false;
    }

    private async Task DisconnectImpl(TargetInfoViewModel target)
    {
        /* Set disconnecting flag. */
        IsDisconnecting = true;
        
        /* Ask the target to disconnect. */
        await target.DisconnectAsync();
        
        /* We're done disconnecting, indicate so. */
        IsDisconnecting = false;
    }
    
    [RelayCommand]
    private async Task Connect()
    {
        var sel = SelectedTarget;

        /* Connect or disconnect depending on the target's status. */
        if (sel.IsConnected)
        {
            await DisconnectImpl(sel);
        }
        else
        {
            await ConnectImpl(sel);
        }
    }
}