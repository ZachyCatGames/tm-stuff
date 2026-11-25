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

    private Dictionary<int, Target.Target> connectedTargets = new();

    private UsbContext context;

    private Task? hotplugMonitor;
    private CancellationTokenSource? discoveryCts;
    private readonly Channel<DeviceEventArgs> discoveryEvents = Channel.CreateUnbounded<DeviceEventArgs>();
    private readonly Dictionary<LocationId, TargetInfo> discoveryInfos = new();

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
        int id = device.Address | (device.BusNumber << 8);
        
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
        var serviceMgr = new ServiceManager();
        var usbif      = new UsbInterface(device);
        var pktMgr     = new UsbPacketManager(usbif, serviceMgr, this);
        var targ       = new Target.Target(parent, serviceMgr, pktMgr);

        /* Initialize target. */
        await targ.InitializeAsync();
        
        return (targ, true);
    }
    
    public async Task<TargetList> QueryTargetsImplUnsafeAsync(Func<Target.Target, bool> pred)
    {
        var list = new TargetList();
        
        using (var sdkDevices = context.FindAll(d => d.VendorId == SdkVendorId && d.ProductId == SdkProductId))
        {
            foreach (var dev in sdkDevices)
            {
                var (targ, isNew) = await CreateTargetAsyncUnsafe((UsbDevice)(dev.Clone()));
                if (isNew)
                {
                    if (pred(targ))
                    {
                        list.Add(targ);
                    }
                    else
                    {
                        await targ.CloseAsync();
                        await targ.DisposeAsync();
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
                int id = ((UsbPacketManager)targ.GetPacketManager()).GetUsbDeviceNumber();
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

    public void BeginTargetDiscovery()
    {
        Debug.Assert(hotplugMonitor == null);
        Debug.Assert(discoveryCts == null);
        
        if (context.IsUsingHotplug)
            return;

        /* Register an event listener. */
        context.DeviceEvent += HotplugCallback;
        
        /* Start the hotplug monitor. */
        discoveryCts = new();
        hotplugMonitor = HotplugWorker();
        
        /* Begin usb hotplug. */
        context.RegisterHotPlug();
        
        Console.WriteLine("here");
    }

    public async Task EndTargetDiscoveryAsync()
    {
        Debug.Assert(discoveryCts != null);
        Debug.Assert(hotplugMonitor != null);
        
        /* Stop hotplug. */
        context.UnregisterHotPlug();
        
        /* Signal that we're done. */
        await discoveryCts.CancelAsync();
        await hotplugMonitor;
        
        /* Remove ourself as an event listen. */
        context.DeviceEvent -= HotplugCallback;
        
        /* Cleanup. */
        discoveryInfos.Clear();
        discoveryCts = null;
        hotplugMonitor = null;
    }

    private void HotplugCallback(object? sender, DeviceEventArgs e)
    {
        discoveryEvents.Writer.TryWrite(e);
    }
    
    private async Task HotplugWorker()
    {
        while (true)
        {
            DeviceEventArgs e;
            try
            {
                e = await discoveryEvents.Reader.ReadAsync(discoveryCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            
            if (e is DeviceArrivedEventArgs)
            {
                UsbDevice dev = (e as DeviceArrivedEventArgs).Device;
            
                /* Ignore it if it's not a NintendoSDK device. */
                if (dev.ProductId == SdkProductId || dev.VendorId == SdkVendorId)
                {
                    /* Grab device info. */
                    var (targ, isNew) = await CreateTargetAsyncUnsafe(dev);

                    /* Notify the manager. */
                    var info = targ.Info;
                    parent.NotifyDiscoveryEvent(info, true);
            
                    /* Save info ourself. */
                    discoveryInfos[dev.LocationId] = info;
            
                    /* Destroy the connection. */
                    await targ.CloseAsync();
                    await targ.DisposeAsync();
                }
                else
                {
                    dev.Dispose();
                }
            }
            else if (e is DeviceLeftEventArgs)
            {
                var devInfo =  (e as DeviceLeftEventArgs).DeviceInfo.PortInfo;
                if (!discoveryInfos.ContainsKey(devInfo))
                    return;
            
                /* Remove it. */
                parent.NotifyDiscoveryEvent(discoveryInfos[devInfo], false);
                discoveryInfos.Remove(devInfo);
            }
        }

    }

    public void NotifyClose(UsbPacketManager child)
    {
        //using var l = AsyncLockHolder.Enter(asyncLock);
        
        /* Assert this? */
        int id = child.GetUsbDeviceNumber();
        connectedTargets.Remove(id);
    }

    public void NotifyAbruptClose(UsbPacketManager child)
    {
        //using var l = AsyncLockHolder.Enter(asyncLock);

        /* Notify the parent target. */
        int id = child.GetUsbDeviceNumber();
        connectedTargets[id].NotifyAbruptClose();
    }
}