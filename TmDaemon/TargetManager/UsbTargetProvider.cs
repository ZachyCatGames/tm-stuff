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

    private Dictionary<string, Target.Target> connectedTargets = new();

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
        this.context = new UsbContext();
    }

    public void Dispose()
    {
        this.context.Dispose();
    }
    
    private async Task<(Target.Target?, bool)> CreateTargetAsyncUnsafe(UsbDevice device)
    {
        string id;
        try
        {
            id = device.LocationId.ToString();
        }
        catch (UsbException e)
        {
            return (null, false);
        }
        
        /* Check if we already have a target for this device. */
        if (this.connectedTargets.ContainsKey(id))
        {
            return (this.connectedTargets[id], false);
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
            await pktMgr.DisconnectAsync();
            pktMgr.Dispose();
            return (null, false);
        }
        catch (Exception e)
        {
            Debug.WriteLine("Non-USB Exception in CreateTarget: {0}", e);
            await pktMgr.DisconnectAsync();
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
            targ.Dispose();
            return (null, false);
        }
        
        return (targ, true);
    }

    public async Task<TargetList> QueryTargetsImplUnsafeAsync()
    {
        var list = new TargetList();
        
        using (var sdkDevices = this.context.FindAll(d => d.VendorId == SdkVendorId && d.ProductId == SdkProductId))
        {
            foreach (var dev in sdkDevices)
            {
                var (target, isNew) = await this.CreateTargetAsyncUnsafe((UsbDevice)(dev.Clone()));
                if (isNew && target != null)
                {
                    list.Add(target);
                } 
            }
        }

        return list;
    } 
    
    public async Task<int> QueryDisconnectedCountAsync()
    {
        using var l = await AsyncLockHolder.EnterAsync(asyncLock);

        int count = 0;
        await using (var targets = await this.QueryTargetsImplUnsafeAsync())
        {
            count = targets.Count;
        }

        return count;
    }

    public async Task<List<TargetInfo>> QueryDisconnectedInfosAsync()
    {
        using var l = await AsyncLockHolder.EnterAsync(asyncLock);
        
        var list = new List<TargetInfo>();
        await using (var targets = await this.QueryTargetsImplUnsafeAsync())
        {
            targets.ForEach(t => list.Add(t.Info));
        }

        return list;
    }

    public async Task<int> QueryConnectedCountAsync()
    {
        using var l = await AsyncLockHolder.EnterAsync(asyncLock);
        
        return this.connectedTargets.Count;
    }

    public async Task<List<TargetInfo>> QueryConnectedInfosAsync()
    {
        using var l = await AsyncLockHolder.EnterAsync(asyncLock);
        
        var list = new List<TargetInfo>();
        foreach (var target in this.connectedTargets.Values)
        {
            list.Add(target.Info);
        }

        return list;
    }
    
    public async Task<Target.Target?> ConnectAsync(string serialNumber)
    {
        using var l = await AsyncLockHolder.EnterAsync(asyncLock);
        
        var list = await this.QueryTargetsImplUnsafeAsync();
        Target.Target? selected = null;
        foreach (var targ in list)
        {
            TargetInfo info = targ.Info;
            if (info.SerialNumber == serialNumber)
            {
                /* Connect to it. */
                await targ.ConnectAsync();

                /* Add it to the connected list. */
                var id = ((UsbPacketManager)targ.GetPacketManager()).UsbLocationId.ToString();
                connectedTargets[id] = targ;

                /* Dispose of the last selected target. */
                if (selected != null)
                {
                    await selected.DisconnectAsync();
                    selected.Dispose();
                }

                selected = targ;
            }
            else
            {
                targ.Dispose();
            }
        }
        return selected;
    }

    public void BeginTargetHotplug()
    {
        Debug.Assert(this.hotplugMonitor == null);
        Debug.Assert(this.hotplugCts == null);
        
        if (this.context.IsUsingHotplug)
            return;

        /* Register an event listener. */
        this.context.DeviceEvent += this.HotplugCallback;
        
        /* Start the hotplug monitor. */
        this.hotplugCts = new();
        this.hotplugMonitor = this.HotplugWorker();
        
        /* Begin usb hotplug. */
        this.context.RegisterHotPlug();
        
        Console.WriteLine("here");
    }

    public async Task EndTargetHotplug()
    {
        Debug.Assert(hotplugCts != null);
        Debug.Assert(hotplugMonitor != null);
        
        /* Stop hotplug. */
        this.context.UnregisterHotPlug();
        
        /* Signal that we're done. */
        await this.hotplugCts.CancelAsync();
        await this.hotplugMonitor;
        
        /* Remove ourself as an event listen. */
        this.context.DeviceEvent -= this.HotplugCallback;
        
        /* Cleanup. */
        this.discoveryInfos.Clear();
        this.hotplugCts = null;
        this.hotplugMonitor = null;
    }
    
    private void HotplugCallback(object? sender, DeviceEventArgs e)
    {
        this.hotplugEvents.Writer.TryWrite(e);
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

            /* If it's not a new target, resume if it's suspended. */
            Debug.Assert(targ.Info != null);
            Console.WriteLine("{0}", isNew);
            if (!isNew && targ.Info.Status == TargetStatus.Asleep)
            {
                dev.Open();
                await targ.NotifyResume(dev);
            }
            else
            {
                /* Notify the manager. */
                var info = targ.Info;
                this.parent.NotifyDiscoveryEvent(info, true);

                /* Save info the target info ourself. */
                this.discoveryInfos[dev.LocationId] = info;

                /* Destroy the target if it was a preexisting connection. */
                if (isNew)
                {
                    await targ.DisconnectAsync();
                    targ.Dispose();
                }
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
        if (!this.discoveryInfos.ContainsKey(devInfo))
            return;
            
        /* Remove it. */
        parent.NotifyDiscoveryEvent(this.discoveryInfos[devInfo], false);
        this.discoveryInfos.Remove(devInfo);
    }
    
    private async Task HotplugWorker()
    {
        while (true)
        {
            DeviceEventArgs e;
            try
            {
                Debug.Assert(this.hotplugCts != null);
                e = await this.hotplugEvents.Reader.ReadAsync(this.hotplugCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            
            if (e is DeviceArrivedEventArgs)
            {
                await this.HandleDevicePlugged((DeviceArrivedEventArgs)e);
            }
            else if (e is DeviceLeftEventArgs)
            {
                await this.HandleDeviceUnplugged((DeviceLeftEventArgs)e);
            }
        }

    }

    public void NotifyClose(UsbPacketManager child)
    {
        //using var l = AsyncLockHolder.Enter(asyncLock);
        
        /* Do nothing if it isn't registered. */
        var id = child.UsbLocationId.ToString();
        if (!this.connectedTargets.ContainsKey(id))
            return;
        
        /* Remove it from our target list. */
        this.connectedTargets.Remove(id);
    }

    public void NotifyAbruptClose(UsbPacketManager child)
    {
        //using var l = AsyncLockHolder.Enter(asyncLock);
        
        /* Do nothing if it isn't registered. */
        var id = child.UsbLocationId.ToString();
        if (!this.connectedTargets.ContainsKey(id))
            return;

        /* Notify the parent target. */
        this.connectedTargets[id].NotifyAbruptClose();
    }
}