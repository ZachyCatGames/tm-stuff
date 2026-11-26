using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using LibUsbDotNet;
using LibUsbDotNet.Info;
using LibUsbDotNet.LibUsb;
using Test.Packets;
using Test.Services;
using Test.Target;

namespace Test.TargetManager;

public class UsbTargetProvider : ITargetProvider, IDisposable
{
    class TargetInfoReader : IDisposable
    {
        // connect + beacon reply
        private const UInt32 InitializeServiceId   = 0xcd5ed1b8;
        // disconnect + no reply
        private const UInt32 FinalizeServiceId     = 0xf97fe6b9;
        // beacon reply
        private const UInt32 QueryAgentInfo        = 0x3a8ddd94;
        // send host info + no reply
        private const UInt32 SendHostInfoServiceId = 0x4d4a6c24;
        
        private readonly UsbInterface usbIf;

        private async Task<bool> SendPacket(Packet pkt)
        {
            /* Send the header. */
            var (err, sent) = await usbIf.WriteAsync(pkt.GetBuffer(), 0, Packet.HeaderSize);
            if (err != Error.Success)
            {
                return false;
            }
            
            /* Send the body. */
            (err, sent) = await usbIf.WriteAsync(pkt.GetBuffer(), Packet.HeaderSize, pkt.dataSize);
            if (err != Error.Success)
            {
                return false;
            }

            return true;
        }

        private async Task<bool> ReceivePacket(Packet pkt)
        {
            /* Receive the whole packet. */
            var (err, received) = await usbIf.ReadAsync(pkt.GetBuffer(), 0, Packet.PacketMaxSize);
            return err == Error.Success;
        }
        
        public async Task<BeaconPacket?> GetBeaconImpl(uint serviceId)
        {
            var pkt = new Packet();
            pkt.Reset();
            pkt.serviceId = serviceId;
            pkt.Write(4);
            pkt.WriteHeader();

            if (!await this.SendPacket(pkt))
                return null;

            if (!await this.ReceivePacket(pkt))
                return null;
            
            /* Parse the reply. */
            pkt.ParseHeader();
            string beaconStr = pkt.ReadString(0x1000);
            return new BeaconPacket(beaconStr);
        }
        
        public TargetInfoReader(UsbDevice dev)
        {
            this.usbIf = new UsbInterface(dev, 500, false);
        }

        public void Dispose() => usbIf.Dispose();

        public async Task<BeaconPacket?> GetBeacon()
        {
            for (int i = 0; i < 5; i++)
            {
                var reply = await this.GetBeaconImpl(QueryAgentInfo);
                if (reply != null)
                    return reply;
            }

            return null;
        }

        public async Task<BeaconPacket?> Connect()
        {
            for (int i = 0; i < 5; i++)
            {
                var reply = await this.GetBeaconImpl(QueryAgentInfo);
                if (reply != null)
                    return reply;
            }

            return null;
        }

        public async Task SendHostBeacon(int bcid, int ps)
        {
            string beacon = string.Format("\nBCID: {0}\nPS: {1}\n", bcid, ps);
            var pkt = new Packet();
            pkt.Reset();
            pkt.serviceId = SendHostInfoServiceId;
            pkt.Write((UInt32)beacon.Length + 1);
            pkt.Write(beacon);
            pkt.WriteHeader();

            await this.SendPacket(pkt);
        }
    }

    const int SdkProductId = 0x3000;
    const int SdkVendorId = 0x57E;

    private ITargetManager parent;

    private SemaphoreSlim asyncLock = new(1);

    private Dictionary<string, Target.Target> connectedTargets = new();

    private UsbContext context;

    private Task? hotplugMonitor;
    private CancellationTokenSource? hotplugCts;
    private readonly Channel<DeviceEventArgs> hotplugEvents = Channel.CreateUnbounded<DeviceEventArgs>();
    private readonly Dictionary<string, TargetInfo> attachedTargets = new();
    private readonly Dictionary<string, (TargetInfo, string)> attachedBySerial = new();
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
    
    private async Task<BeaconPacket?> GetBeaconPacket(UsbDevice device)
    {
        using (var reader = new TargetInfoReader((UsbDevice)device))
        {
            return await reader.GetBeacon();
        }
    }
    
    public async Task<int> QueryDisconnectedCountAsync()
    {
        using var l = await AsyncLockHolder.EnterAsync(asyncLock);
        return this.attachedTargets.Count;
    }

    public async Task<List<TargetInfo>> QueryDisconnectedInfosAsync()
    {
        using var l = await AsyncLockHolder.EnterAsync(asyncLock);
        return this.attachedTargets.Values.ToList();
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

        if (connectedTargets.TryGetValue(serialNumber, out var target))
            return target;

        if (!attachedBySerial.TryGetValue(serialNumber, out var info))
            return null;

        var (targInfo, id) = info;
        targInfo.Status = TargetStatus.Connected;

        using (var dev = (UsbDevice)context.Find(x => x.LocationId.ToString() == id))
        {
            var srvMgr = new ServiceManager();

            using (var reader = new TargetInfoReader((UsbDevice)dev.Clone()))
            {
                /* Send host info. */
                await reader.SendHostBeacon(96, 0);
                
                /* Connect. */
                await reader.Connect();
            }
        
            /* Setup packet manager. */
            UsbPacketManager pktMgr;
            try
            {
                pktMgr = new((UsbDevice)dev.Clone(), srvMgr, this);
            }
            catch (Exception e)
            {
                return null;
            }

            /* Setup Target. */
            Target.Target targ;
            try
            {
                targ = new(this.parent, srvMgr, pktMgr, targInfo);
            }
            catch (Exception e)
            {
                pktMgr.Dispose();
                return null;
            }

            /* Initialize target. */
            try
            {
                await targ.InitializeAsync();
            }
            catch (Exception e)
            {
                targ.Dispose();
                return null;
            }

            return targ;
        }
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
        this.connectedTargets.Clear();
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
        LocationId id = dev.LocationId;
            
        /* Ignore it if it's not a NintendoSDK device. */
        if (dev.ProductId == SdkProductId || dev.VendorId == SdkVendorId)
        {
            dev.Open();
            
            using (var reader = new TargetInfoReader((UsbDevice)dev.Clone()))
            {
                var beacon = await reader.GetBeacon();
                if (beacon == null)
                    return;
                
                /* Parse the beacon packet. */
                var info = new TargetInfo()
                {
                    Address = id.ToString(),
                    ConnectionType = "USB",
                    SerialNumber = beacon?.SN,
                    Status = TargetStatus.Disconnected
                };
                
                /* Check if this is a connected target. */
                if (this.connectedTargets.TryGetValue(info.SerialNumber, out var target))
                {
                    target.NotifyResume(dev);
                    return;
                }
                
                this.parent.NotifyDiscoveryEvent(info, true);
                this.attachedTargets[id.ToString()] = info;
                this.attachedBySerial[info.SerialNumber] = (info, id.ToString());
            }
        }

        dev.Dispose();

    }

    private async Task HandleDeviceUnplugged(DeviceLeftEventArgs e)
    {
        var devInfo = e.DeviceInfo.PortInfo.ToString();
        if (!this.attachedTargets.TryGetValue(devInfo, out var info))
            return;
            
        /* Remove it. */
        parent.NotifyDiscoveryEvent(this.attachedTargets[devInfo], false);
        this.attachedBySerial.Remove(info.SerialNumber);
        this.attachedTargets.Remove(devInfo);
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