using LibUsbDotNet.LibUsb;

namespace Test;

public class UsbTargetProvider : ITargetProvider, IDisposable
{
    const int SdkProductId = 0x3000;
    const int SdkVendorId = 0x57E;

    private ITargetManager parent;

    private SemaphoreSlim asyncLock = new(1);

    private Dictionary<int, Target> connectedTargets = new();

    private UsbContext context;


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
    
    Target? CreateTargetUnsafe(UsbDevice device, out bool isNew)
    {
        int id = device.Address | (device.BusNumber << 8);
        
        /* Check if we already have a target for this device. */
        if (connectedTargets.ContainsKey(id))
        {
            isNew = false;
            return connectedTargets[id];
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
        var targ       = new Target(parent, serviceMgr, pktMgr);
        
        isNew = true;
        return targ;
    }
    
    public async Task<TargetList> QueryTargetsImplUnsafeAsync(Func<Target, bool> pred)
    {
        var list = new TargetList();
        
        using (var sdkDevices = context.FindAll(d => d.VendorId == SdkVendorId && d.ProductId == SdkProductId))
        {
            foreach (var dev in sdkDevices)
            {
                var targ = CreateTargetUnsafe((UsbDevice)(dev.Clone()), out bool isNew);
                if (isNew)
                {
                    if (pred(targ))
                        list.Add(targ);
                    else
                        targ.Dispose();
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
        
        using (var targets = await QueryTargetsImplUnsafeAsync())
        {
            return targets.Count;
        }
    }

    public async Task<List<TargetInfo>> QueryDisconnectedInfosAsync()
    {
        using var l = await AsyncLockHolder.EnterAsync(asyncLock);
        
        var list = new List<TargetInfo>();
        using (var targets = await QueryTargetsImplUnsafeAsync())
        {
            targets.ForEach(targ => list.Add(targ.Info));
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
    
    public async Task<Target?> ConnectAsync(string serialNumber)
    {
        using var l = await AsyncLockHolder.EnterAsync(asyncLock);
        
        var list = await QueryTargetsImplUnsafeAsync();
        Target? selected = null;
        foreach (var targ in list)
        {
            TargetInfo info = targ.Info;
            if (info.SerialNumber == serialNumber)
            {
                /* Connect to it. */
                targ.Connect();
            
                /* Add it to the connected list. */
                int id = ((UsbPacketManager)targ.GetPacketManager()).GetUsbDeviceNumber();
                connectedTargets[id] = targ;

                /* Dispose of the last selected target. */
                if (selected != null)
                    selected.Dispose();
                selected = targ;
            }
            else
            {
                targ.Dispose();
            }
        }
        return selected;
    }

    public void NotifyDisconnect(UsbPacketManager child)
    {
        using var l = AsyncLockHolder.Enter(asyncLock);
        
        /* Assert this? */
        int id = child.GetUsbDeviceNumber();
        connectedTargets.Remove(id);
    }
}