using System.Diagnostics;
using System.Threading.Channels;
using LibUsbDotNet;
using LibUsbDotNet.Info;
using LibUsbDotNet.LibUsb;
using Test.Services;
using Test.Target;

namespace Test.TargetManager;

public class UsbTargetProvider : ITargetProvider, IDisposable
{
    const int SdkProductId = 0x3000;
    const int SdkVendorId = 0x57E;

    private ITargetManager parent;

    private SemaphoreSlim asyncLock = new(1);

    private Dictionary<LocationId, Target.Target> connectedTargets = new();

    private UsbContext context;

    private Task? hotplugMonitor;
    private CancellationTokenSource? hotplugCts;
    private readonly Channel<DeviceEventArgs> hotplugEvents = Channel.CreateUnbounded<DeviceEventArgs>();
    private readonly Dictionary<LocationId, TargetInfo> discoveryInfos = new();
    private bool discoveryEnabled = false;

    public UsbTargetProvider(ITargetManager parent)
    {
        this.parent = parent;
        
        /* Setup USB Context */
        context = new UsbContext();
    }

    public void Dispose()
    {
        context.Dispose();
    }
    
    private async Task<(Target.Target?, bool)> CreateTargetAsyncUnsafe(UsbDevice device)
    {
        LocationId id;
        try
        {
            id = device.LocationId;
        }
        catch (UsbException e)
        {
            return (null, false);
        }
        
        /* Check if we already have a target for this device. */
        if (connectedTargets.ContainsKey(id))
        {
            return (connectedTargets[id], false);
        }
        
        /* Open the device. */
        device.Open();

        foreach (var c in device.Configs)
        {
            Console.WriteLine(c.ToString());
        }

        foreach (var c in device.Configs[0].Interfaces)
        {
            Console.WriteLine(c.ToString());
        }

        /* Create a new target. */
        UsbPacketManager pktMgr;
        Target.Target targ;
        var serviceMgr = new ServiceManager();
        
        try
        {
            pktMgr = new UsbPacketManager(device, serviceMgr, this);
        }
        catch (UsbException e)
        {
            Debug.WriteLine("Non-USB Exception in CreateTarget: {0}", e);
            device.Dispose();
            return (null, false);
        }
        catch (Exception e)
        {
            device.Dispose();
            return (null, false);
        }

        try
        {
            targ = new Target.Target(parent, serviceMgr, pktMgr);
        }
        catch (UsbException e)
        {
            await pktMgr.CloseAsync();
            pktMgr.Dispose();
            return (null, false);
        }
        catch (Exception e)
        {
            Debug.WriteLine("Non-USB Exception in CreateTarget: {0}", e);
            await pktMgr.CloseAsync();
            pktMgr.Dispose();
            return (null, false);
        }

        /* Initialize target. */
        bool success = false;
        try
        {
            success = await targ.InitializeAsync();
        }
        catch (UsbException e)
        {
            /* ... */
        }
        catch (Exception e)
        {
            Debug.WriteLine("Non-USB Exception in CreateTarget: {0}", e);
        }

        if (!success)
        {
            await targ.DisconnectAsync();
            await targ.DisposeAsync();
            return (null, false);
        }
        
        return (targ, true);
    }
    
    public async Task<TargetList> QueryTargetsImplUnsafeAsync(Func<Target.Target, bool> pred)
    {
        var list = new TargetList();
        
        using (var sdkDevices = context.FindAll(d => d.VendorId == SdkVendorId && d.ProductId == SdkProductId))
        {
            foreach (var dev in sdkDevices)
            {
                var (target, isNew) = await CreateTargetAsyncUnsafe((UsbDevice)(dev.Clone()));
                if (isNew && target != null)
                {
                    if (pred(target))
                    {
                        list.Add(target);
                    }
                    else
                    {
                        await target.CloseAsync();
                        await target.DisposeAsync();
                    }
                } 
            }
        }

        return list;
    }

    public Task<TargetList> QueryTargetsImplUnsafeAsync()
    {
        return QueryTargetsImplUnsafeAsync((_) => true);
    } 
    
    public async Task<int> QueryDisconnectedCountAsync()
    {
        using var l = await AsyncLockHolder.EnterAsync(asyncLock);

        int count = 0;
        await using (var targets = await QueryTargetsImplUnsafeAsync())
        {
            count = targets.Count;
        }

        return count;
    }

    public async Task<List<TargetInfo>> QueryDisconnectedInfosAsync()
    {
        using var l = await AsyncLockHolder.EnterAsync(asyncLock);
        
        var list = new List<TargetInfo>();
        await using (var targets = await QueryTargetsImplUnsafeAsync())
        {
            targets.ForEach(t => list.Add(t.Info));
        }

        return list;
    }

    public async Task<int> QueryConnectedCountAsync()
    {
        using var l = await AsyncLockHolder.EnterAsync(asyncLock);
        
        return connectedTargets.Count;
    }

    public async Task<List<TargetInfo>> QueryConnectedInfosAsync()
    {
        using var l = await AsyncLockHolder.EnterAsync(asyncLock);
        
        var list = new List<TargetInfo>();
        foreach (var target in connectedTargets.Values)
        {
            list.Add(target.Info);
        }

        return list;
    }
    
    public async Task<Target.Target?> ConnectAsync(string serialNumber)
    {
        using var l = await AsyncLockHolder.EnterAsync(asyncLock);
        
        var list = await QueryTargetsImplUnsafeAsync();
        Target.Target? selected = null;
        foreach (var targ in list)
        {
            TargetInfo info = targ.Info;
            if (info.SerialNumber == serialNumber)
            {
                /* Connect to it. */
                await targ.ConnectAsync();

                /* Add it to the connected list. */
                var id = ((UsbPacketManager)targ.GetPacketManager()).UsbLocationId;
                connectedTargets[id] = targ;

                /* Dispose of the last selected target. */
                if (selected != null)
                {
                    await selected.CloseAsync();
                    selected.Dispose();
                }

                selected = targ;
            }
            else
            {
                await targ.DisposeAsync();
            }
        }
        return selected;
    }

    public void BeginTargetHotplug()
    {
        Debug.Assert(hotplugMonitor == null);
        Debug.Assert(hotplugCts == null);
        
        if (context.IsUsingHotplug)
            return;

        /* Register an event listener. */
        context.DeviceEvent += HotplugCallback;
        
        /* Start the hotplug monitor. */
        hotplugCts = new();
        hotplugMonitor = HotplugWorker();
        
        /* Begin usb hotplug. */
        context.RegisterHotPlug();
        
        Console.WriteLine("here");
    }

    public async Task EndTargetHotplug()
    {
        Debug.Assert(hotplugCts != null);
        Debug.Assert(hotplugMonitor != null);
        
        /* Stop hotplug. */
        context.UnregisterHotPlug();
        
        /* Signal that we're done. */
        await hotplugCts.CancelAsync();
        await hotplugMonitor;
        
        /* Remove ourself as an event listen. */
        context.DeviceEvent -= HotplugCallback;
        
        /* Cleanup. */
        discoveryInfos.Clear();
        hotplugCts = null;
        hotplugMonitor = null;
    }
    
    private void HotplugCallback(object? sender, DeviceEventArgs e)
    {
        hotplugEvents.Writer.TryWrite(e);
    }

    private async Task HandleDevicePlugged(DeviceArrivedEventArgs e)
    {
        UsbDevice dev = e.Device;
            
        /* Ignore it if it's not a NintendoSDK device. */
        if (dev.ProductId == SdkProductId || dev.VendorId == SdkVendorId)
        {
            /* Grab device info. */
            var (targ, isNew) = await CreateTargetAsyncUnsafe(dev);
            if (targ == null)
                return;

            /* Notify the manager. */
            var info = targ.Info;
            parent.NotifyDiscoveryEvent(info, true);

            /* Save info the target info ourself. */
            discoveryInfos[dev.LocationId] = info;

            /* Destroy the target if it was a preexisting connection. */
            if (isNew)
            {
                await targ.CloseAsync();
                await targ.DisposeAsync();
            }
        }
        else
        {
            dev.Dispose();
        }
    }

    private async Task HandleDeviceUnplugged(DeviceLeftEventArgs e)
    {
        var devInfo = e.DeviceInfo.PortInfo;
        if (!discoveryInfos.ContainsKey(devInfo))
            return;
            
        /* Remove it. */
        parent.NotifyDiscoveryEvent(discoveryInfos[devInfo], false);
        discoveryInfos.Remove(devInfo);
    }
    
    private async Task HotplugWorker()
    {
        while (true)
        {
            DeviceEventArgs e;
            try
            {
                e = await hotplugEvents.Reader.ReadAsync(hotplugCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            
            if (e is DeviceArrivedEventArgs)
            {
                HandleDevicePlugged((DeviceArrivedEventArgs)e);
            }
            else if (e is DeviceLeftEventArgs)
            {
                await HandleDeviceUnplugged((DeviceLeftEventArgs)e);
            }
        }

    }

    public void NotifyClose(UsbPacketManager child)
    {
        //using var l = AsyncLockHolder.Enter(asyncLock);
        
        /* Do nothing if it isn't registered. */
        var id = child.UsbLocationId;
        if (!connectedTargets.ContainsKey(id))
            return;
        
        /* Remove it from our target list. */
        connectedTargets.Remove(id);
    }

    public void NotifyAbruptClose(UsbPacketManager child)
    {
        //using var l = AsyncLockHolder.Enter(asyncLock);
        
        /* Do nothing if it isn't registered. */
        var id = child.UsbLocationId;
        if (!connectedTargets.ContainsKey(id))
            return;

        /* Notify the parent target. */
        connectedTargets[id].NotifyAbruptClose();
    }
}