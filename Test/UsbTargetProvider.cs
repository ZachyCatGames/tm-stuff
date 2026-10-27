using LibUsbDotNet.LibUsb;

namespace Test;

public class UsbTargetProvider : ITargetProvider, IDisposable
{
    const int SdkProductId = 0x3000;
    const int SdkVendorId = 0x57E;

    private Dictionary<int, Target> connectedTargets = new();

    private UsbContext context;


    public UsbTargetProvider()
    {
        context = new UsbContext();
    }

    public void Dispose()
    {
        context.Dispose();
    }
    
    Target? CreateTarget(UsbDevice device, out bool isNew)
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
        var pktMgr     = new UsbPacketManager(usbif, serviceMgr);
        var targ       = new Target(serviceMgr, pktMgr);
        
        isNew = true;
        return targ;
    }
    
    public async Task<TargetList> QueryTargetsImplAsync(Func<Target, bool> pred)
    {
        var list = new TargetList();
        
        using (var sdkDevices = context.FindAll(d => d.VendorId == SdkVendorId && d.ProductId == SdkProductId))
        {
            foreach (var dev in sdkDevices)
            {
                var targ = CreateTarget((UsbDevice)(dev.Clone()), out bool isNew);
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

    public Task<TargetList> QueryTargetsImplAsync()
    {
        return QueryTargetsImplAsync((_) => true);
    } 
    
    public async Task<int> QueryDisconnectedCountAsync()
    {
        using (var targets = await QueryTargetsImplAsync())
        {
            return targets.Count;
        }
    }

    public async Task<List<TargetInfo>> QueryDisconnectedInfosAsync()
    {
        var list = new List<TargetInfo>();
        using (var targets = await QueryTargetsImplAsync())
        {
            targets.ForEach(targ => list.Add(targ.Info));
        }

        return list;
    }

    public async Task<int> QueryConnectedCountAsync()
    {

        return connectedTargets.Count;
    }

    public async Task<List<TargetInfo>> QueryConnectedInfosAsync()
    {
        var list = new List<TargetInfo>();
        foreach (var target in connectedTargets.Values)
        {
            list.Add(target.Info);
        }

        return list;
    }
    
    public async Task<Target?> ConnectAsync(string serialNumber)
    {
        var list = await QueryTargetsImplAsync();
        Target? selected = null;
        foreach (var targ in list)
        {
            TargetInfo info = targ.Info;
            if (info.serialNumber == serialNumber)
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
}