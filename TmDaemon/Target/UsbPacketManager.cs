using System.Diagnostics;
using LibUsbDotNet;
using LibUsbDotNet.Info;
using LibUsbDotNet.LibUsb;
using Test.Packets;
using Test.Services;
using Test.TargetManager;

namespace Test.Target;

public class UsbPacketManager : IPacketManager
{
    // connect + beacon reply
    private const UInt32 InitializeServiceId    = 0xcd5ed1b8;
    // disconnect + no reply
    private const UInt32 FinalizeQuietServiceId = 0xf97fe6b9;
    // beacon reply
    private const UInt32 FinalizeServiceId      = 0x3a8ddd94;
    // send host info + no reply
    private const UInt32 SendHostInfoServiceId  = 0x4d4a6c24;

    private class ManagerService : IService
    {
        private readonly ServiceManager manager;
        public ManagerService(ServiceManager mgr)
        {
            this.manager = mgr;
        }

        public ServiceManager GetManager() => this.manager;

        public bool AcceptsServicesId(uint serviceId)
        {
            return serviceId == InitializeServiceId ||
                   serviceId == FinalizeQuietServiceId ||
                   serviceId == FinalizeServiceId ||
                   serviceId == SendHostInfoServiceId;
        }
        
        public UInt32 GetPrimaryServiceId() => 0;

        public ServiceTask? ProcessNewRequest(Packets.Packet pkt)
        {
            throw new NotImplementedException();
        }
    }
    
    private class NormalTask : ServiceTask
    {
        public NormalTask(UInt32 serviceId, ManagerService mgr) : base(mgr, TaskType.Invalid, HostTaskIdAllocator.TaskIdForMgr, 0)
        {
            this.serviceId = serviceId;
        }

        protected override async Task Run()
        {
            /* Setup the packet. */
            Packets.Packet pkt = await AllocSendPacketAsync();
            pkt.serviceId = serviceId;
            pkt.taskId = HostTaskIdAllocator.TaskIdForMgr;
            pkt.isInitiate = false;
            pkt.taskType = TaskType.Invalid;
            pkt.Write((UInt32)4);
            
            /* Send the packet. */
            await SendPacketAsync(pkt);
        }
    }

    private class BeaconTask : ServiceTask, IDisposable
    {
        public Packets.Packet BeaconPacket { get; private set; }
        public BeaconTask(UInt32 serviceId, ManagerService mgr) : base(mgr, TaskType.Invalid, HostTaskIdAllocator.TaskIdForMgr, 0)
        {
            this.serviceId = serviceId;
        }

        protected override async Task Run()
        {
            /* Setup the packet. */
            Packets.Packet pkt = await AllocSendPacketAsync();
            pkt.serviceId = serviceId;
            pkt.taskId = HostTaskIdAllocator.TaskIdForMgr;
            pkt.isInitiate = false;
            pkt.taskType = TaskType.Invalid;
            pkt.Write((UInt32)4);
            
            /* Send the packet. */
            await this.SendPacketAsync(pkt);
            
            /* Wait for the reply. */
            this.BeaconPacket = await this.WaitForPacket();
        }
        
        public void Dispose() => this.BeaconPacket.Release();
    }
    
    private class HostInfoTask : ServiceTask
    {
        private string info;
        
        public HostInfoTask(UInt32 serviceId, ManagerService mgr, string info) : base(mgr, TaskType.Invalid, HostTaskIdAllocator.TaskIdForMgr, 0)
        {
            this.serviceId = serviceId;
            this.info      = info;
        }

        protected override async Task Run()
        {
            /* Setup the packet. */
            Packet pkt = await AllocSendPacketAsync();
            pkt.serviceId = serviceId;
            pkt.taskId = HostTaskIdAllocator.TaskIdForMgr;
            pkt.isInitiate = false;
            pkt.taskType = TaskType.Invalid;
            pkt.Write((UInt32)(info.Length + 1));
            pkt.Write(info);
            
            /* Send the packet. */
            await this.SendPacketAsync(pkt);
        }
    }
    
    private Thread sendThread;
    private Thread recvThread;

    private readonly UsbTargetProvider parent;
    
    private readonly ServiceManager serviceManager;

    private UsbInterface usbIf;

    private BeaconPacket? beaconPacket;
    
    private bool isConnected;
    
    private CancellationTokenSource threadCts = new();

    private bool          abruptDisconnectHandled = false;
    private readonly Lock abruptDisconnectLock    = new();

    private readonly ManagerService managerService;

    private readonly SemaphoreSlim cancelSem = new(0);

    public LocationId UsbLocationId { get; init; }
    
    public bool IsSuspended { get; private set; }
    
    public UsbPacketManager(UsbDevice usb, ServiceManager mgr, UsbTargetProvider parent)
    {
        serviceManager = mgr;
        usbIf          = new UsbInterface(usb);
        this.parent    = parent;
        managerService = new(serviceManager);

        UsbLocationId = usbIf.UsbLocationId;

        /* Register the manager service. */
        serviceManager.RegisterService(managerService);

        beaconPacket    = null;
        isConnected     = false;
        
        /* Start the send and receive threads. */
        this.StartWorkThreads();
    }

    public void Dispose()
    {
        /* Dispose the USB Interface. */
        usbIf.Dispose();
    }
    
    public async Task<TargetInfo?> GetTargetInfo()
    {
        /* Get the beacon packet. */
        BeaconPacket? pkt = await GetBeaconPacket();
        if (pkt == null)
            return null;

        /* Parse it. */
        string sn = pkt != null ? pkt.Value.SN : "Unknown";
        return new TargetInfo()
        {
            SerialNumber   = sn,
            Address        = UsbLocationId.ToString(),
            ConnectionType = "USB",
            Status         = isConnected ? TargetStatus.Connected : TargetStatus.Disconnected,
        };
    }

    public async Task ConnectAsync()
    {
        /* Cancel any ongoing tasks. */
        await SendBeaconTaskAsync(FinalizeServiceId);
        
        /* Actually connect. */
        await ConnectImplAsync(0);
    }
    
    public async Task DisconnectAsync()
    {
        /* Block new initiate requests. */
        serviceManager.InitiaiteLock = true;
        
        /* Stop send and recv threads. */
        await StopThreadsAsync();
        
        /* Tell our parent that we're done. */
        parent.NotifyClose(this);
    }

    public async Task NotifySuspendAsync()
    {
        /* Indicate we're suspended. */
        this.IsSuspended = true;
        
        /* Stop the send and receive thread. */
        await this.StopThreadsAsync();
        
        /* Destroy our usb interface. */
        this.usbIf.Dispose();
    }
    
    public async Task NotifyResumeAsync(object obj)
    {
        Console.WriteLine("[usb] resuming");
        
        Debug.Assert(obj is UsbDevice);
        UsbDevice dev = (UsbDevice)obj;
        
        /* Update our usb interface. */
        this.usbIf = new UsbInterface(dev);
        
        /* Start the work threads. */
        this.StartWorkThreads();
        
        /* Reconnect, indicating we had a connection earlier. */
        await this.ConnectImplAsync(1);
        
        /* Clear suspended flag. */
        this.IsSuspended = false;
        
        Console.WriteLine("[usb] resumed");
    }

    public async Task<BeaconPacket?> GetBeaconPacket()
    {
        /* Use cached packet if already cached. */
        if (this.beaconPacket != null)
            return this.beaconPacket;

        /* Send a query packet. */
        await SendBeaconTaskAsync(FinalizeServiceId);
        return this.beaconPacket;
    }
    
    private void StartWorkThreads()
    {
        /* Create a new cancelation token. */
        this.threadCts = new();
        
        /* Start 'em. */
        this.sendThread = new Thread(SendThread);
        this.recvThread = new Thread(RecvThread);
        this.sendThread.Start();
        Console.WriteLine("[usb] Started USB send thread");
        this.recvThread.Start();
        Console.WriteLine("[usb] Started USB recv thread");
    }
    
    
    private async Task ConnectImplAsync(int ps)
    {
        /* Send host info. */
        string beacon = string.Format("\nBCID: 96\nPS: {0}\n", ps);
        await SendHostInfoPacketAsync(beacon);
        Console.WriteLine(beacon);
        
        /* Send a connect packet. */
        await this.SendBeaconTaskAsync(InitializeServiceId);
        
        /* We're now connected. */
        this.isConnected = true;
    }
    
    private async Task<bool> SendNormalTaskAsync(UInt32 serviceId)
    {
        /* Query via get info cmd. */
        var task = new NormalTask(serviceId, managerService);
        this.serviceManager.RegisterNewTask(task);
        
        /* Wait for it to finish. */
        await task.WaitDoneAsync();
        
        return !task.IsCancelled;
    }

    private async Task<bool> SendBeaconTaskAsync(UInt32 serviceId)
    {
        /* Query via get info cmd. */
        using (var task = new BeaconTask(serviceId, managerService))
        {
            this.serviceManager.RegisterNewTask(task);
        
            /* Wait for it to finish. */
            await task.WaitDoneAsync();
        
            /* Check if it was cancelled (due to disconnect). */
            if (task.IsCancelled)
            {
                this.beaconPacket = null;
                return false;
            }
        
            /* Parse the beacon packet. */
            string beaconStr = task.BeaconPacket.ReadString(0x300);
            if (this.beaconPacket == null)
            {
                this.beaconPacket = new BeaconPacket(beaconStr);
            }
        }
        
        return true;
    }

    private async Task<bool> SendHostInfoPacketAsync(string info)
    {
        /* Setup task. */
        var task = new HostInfoTask(SendHostInfoServiceId, this.managerService, info);
        this.serviceManager.RegisterNewTask(task);
        
        /* Wait for it to finish. */
        await task.WaitDoneAsync();

        return !task.IsCancelled;
    }

    private void OnAbruptClose()
    {
        using var l = this.abruptDisconnectLock.EnterScope();

        /* If the other thread already took care of things, do nothing. */
        if (this.abruptDisconnectHandled)
            return;
        
        /* Indicate that handling has finished. */
        this.abruptDisconnectHandled = true;
        
        /* Notify parent so it can do cleanup. */
        this.parent.NotifyAbruptClose(this);
    }
    
    private Error RecvPacket(ref Packets.Packet pkt)
    {
        /* Receive the packet. */
        return this.usbIf.Read(pkt.GetBuffer(), 0, Packets.Packet.PacketMaxSize, out int sizeRead);
    }

    private Error SendPacketImpl(Packets.Packet pkt)
    {
        /* First, send the header. */
        byte[] buf = pkt.GetBuffer();
        Error err  = this.usbIf.Write(buf, 0, Packets.Packet.HeaderSize, out int hdrSize);
        if (err != Error.Success)
            return err;
        //Console.WriteLine("sendidx = {0}, {1} bytes", j++, pkt.dataSize);

        /* Then the data. */
        //Console.WriteLine(pkt.GetDataSize());
        return this.usbIf.Write(buf, Packets.Packet.HeaderSize, pkt.dataSize, out int dataSize);
    }

    private async Task StopThreadsAsync()
    {
        /* Cancel current transfers. */
        await this.threadCts.CancelAsync();
        this.usbIf.Cancel();
        
        /* Wait for both threads to exit. */
        await this.cancelSem.WaitAsync();
        await this.cancelSem.WaitAsync();
        
        /* Join them. */
        this.recvThread.Join();
        this.sendThread.Join();
    }
    
    int j =0;
    void SendThread()
    {
        while (true)
        {
            /* Pop a packet. */
            Packets.Packet pkt;
            try
            {
                pkt = this.serviceManager.PopSendPacketAsync(threadCts.Token).Result;
            }
            catch(AggregateException)
            {
                this.cancelSem.Release();
                return;
            }

            //File.Open(String.Format("send_{0}", j), FileMode.Create).Write(pkt.GetBuffer());
            //pkt.Print();

            /* Send it. */
            Error err = SendPacketImpl(pkt);
            if (this.threadCts.Token.IsCancellationRequested)
            {
                this.cancelSem.Release();
                return;
            }

            /* Treat other errors as an abrupt disconnect. */
            if (err != Error.Success)
            {
                this.cancelSem.Release();
                this.OnAbruptClose();
                return;
            }

            /* Release the packet. */
            pkt.Release();
        }
    }

    int i = 0;
    void RecvThread()
    {
        while (true)
        {
            /* Allocate a recv packet. */
            Packets.Packet pkt = this.serviceManager.AllocRecvPacket();

            /* Wait for a message. */
            Error err = this.RecvPacket(ref pkt);
            if (this.threadCts.Token.IsCancellationRequested)
            {
                this.cancelSem.Release();
                return;
            }
            
            /* Treat other errors as an abrupt disconnect. */
            if (err != Error.Success)
            {
                this.cancelSem.Release();
                this.OnAbruptClose();
                return;
            }

            /* Send it to the ServiceManager. */
            Console.WriteLine("Recv {0}", i);
            //File.Open(String.Format("recv_{0}", i++), FileMode.Create).Write(pkt.GetBuffer());
            this.serviceManager.OnRecvPacket(pkt);
        }
    }
}
