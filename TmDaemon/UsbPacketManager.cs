using System.Diagnostics;

namespace Test;
using System.Collections.Concurrent;
using System.Threading;
using LibUsbDotNet;

public class UsbPacketManager : IPacketManager
{
    // connect + beacon reply
    private const UInt32 InitializeServiceId        = 0xcd5ed1b8;
    // disconnect + no reply
    private const UInt32 FinalizeServiceId          = 0xf97fe6b9;
    // beacon reply
    private const UInt32 QueryBeaconPacketServiceId = 0x3a8ddd94;
    // send host info + no reply
    private const UInt32 SendHostInfoServiceId      = 0x4d4a6c24;

    private class ManagerService : IService
    {
        private readonly ServiceManager manager;
        public ManagerService(ServiceManager mgr)
        {
            manager = mgr;
        }

        public ServiceManager GetManager() => manager;

        public bool AcceptsServicesId(uint serviceId)
        {
            return serviceId == InitializeServiceId ||
                   serviceId == FinalizeServiceId ||
                   serviceId == QueryBeaconPacketServiceId ||
                   serviceId == SendHostInfoServiceId;
        }
        
        public UInt32 GetPrimaryServiceId() => 0;

        public ServiceTask? ProcessNewRequest(Packet pkt)
        {
            throw new NotImplementedException();
        }
    }
    
    private class NormalTask : ServiceTask
    {
        private UInt32 serviceId;
        
        public NormalTask(UInt32 serviceId, ManagerService mgr) : base(mgr, TaskType.Invalid, Packet.TaskIdForMgr, 0)
        {
            this.serviceId = serviceId;
        }

        protected override async Task Run()
        {
            /* Setup the packet. */
            Packet pkt = await AllocSendPacketAsync();
            pkt.Reset();
            pkt.serviceId = serviceId;
            pkt.taskId = Packet.TaskIdForMgr;
            pkt.isInitiate = false;
            pkt.taskType = TaskType.Invalid;
            pkt.Write((UInt32)0);
            pkt.WriteHeader();
            
            /* Send the packet. */
            await SendPacketAsync(pkt);
        }
    }

    private class BeaconTask : ServiceTask
    {
        private UInt32 serviceId;
        
        public Packet BeaconPacket { get; private set; }
        public BeaconTask(UInt32 serviceId, ManagerService mgr) : base(mgr, TaskType.Invalid, Packet.TaskIdForMgr, 0)
        {
            this.serviceId = serviceId;
        }

        protected override async Task Run()
        {
            /* Setup the packet. */
            Packet pkt = await AllocSendPacketAsync();
            pkt.Reset();
            pkt.serviceId = serviceId;
            pkt.taskId = Packet.TaskIdForMgr;
            pkt.isInitiate = false;
            pkt.taskType = TaskType.Invalid;
            pkt.Write((UInt32)0);
            pkt.WriteHeader();
            
            /* Send the packet. */
            await SendPacketAsync(pkt);
            
            /* Wait for the reply. */
            BeaconPacket = await WaitForPacket();
            
            Console.WriteLine("test");
        }
        
        public void Dispose() => BeaconPacket.Release();
    }
    
    private readonly Thread sendThread;
    private readonly Thread recvThread;

    private readonly UsbTargetProvider parent;
    
    private readonly ServiceManager serviceManager;

    private readonly UsbInterface usbIf;

    private BeaconPacket? beaconPacket;
    
    private bool isConnected;
    private bool isCommsDisabled;
    private long recvDone, sendDone;
    
    private readonly CancellationTokenSource cancelSrc = new();

    private bool abruptDisconnectHandled = false;
    private readonly Lock abruptDisconnectLock = new();

    private readonly ManagerService managerService;

    private SemaphoreSlim cancelSem;

    public UsbPacketManager(UsbInterface usb, ServiceManager mgr, UsbTargetProvider parent)
    {
        serviceManager = mgr;
        usbIf          = usb;
        this.parent    = parent;
        managerService = new(serviceManager);
        
        /* Register the manager service. */
        serviceManager.RegisterService(managerService);
        
        sendThread = new Thread(SendThread);
        recvThread = new Thread(RecvThread);

        beaconPacket    = null;
        isConnected     = false;
        isCommsDisabled = false;
        recvDone        = 0;
        sendDone        = 0;
        
        /* Start the send and receive threads. */
        sendThread.Start();
        Console.WriteLine("Started USB send thread");
        recvThread.Start();
        Console.WriteLine("Started USB recv thread");
    }

    public void Dispose()
    {
        /* Dispose the USB Interface. */
        usbIf.Dispose();
    }
    
    public async Task<TargetInfo> GetTargetInfo()
    {
        /* Get the beacon packet. */
        BeaconPacket? pkt = await GetBeaconPacket();

        /* Parse it. */
        string sn = pkt != null ? pkt.Value.SN : "Unknown";
        return new TargetInfo()
        {
            SerialNumber   = sn,
            Address        = GetUsbDeviceNumber().ToString(),
            ConnectionType = "USB",
            Status         = isConnected ? TargetStatus.Connected : TargetStatus.Disconnected,
        };
    }

    public async Task ConnectAsync()
    {
        /* Send a finalize packet to clear the console's task list. */
        await SendNormalTaskImplAsync(FinalizeServiceId);
        
        /* Actually connect. */
        await ReconnectAsync();
    }

    public async Task ReconnectAsync()
    {
        /* Do nothing if we're already connected. */
        if (isConnected)
            return;
        
        /* Send a connect packet. */
        await SendBeaconTaskImplAsync(InitializeServiceId);
        
        /* We're now connected. */
        isConnected = true;
    }

    public async Task DisconnectImplAsync(bool sendDcPkt)
    {
        /* Block new initiate requests. */
        serviceManager.InitiaiteLock = true;
        
        /* Cancel all tasks. */
        await serviceManager.CancelAllTasksAsync();
        
        /* Send the disconnect packet. */
        if (sendDcPkt)
        {
            NormalTask task = new(FinalizeServiceId, managerService);
            serviceManager.RegisterNewTask(task);
            await task.WaitDoneAsync();
        }

        /* TODO: wait */

        /* Stop send and recv threads. */
        await StopThreadsAsync();
        
        /* Tell our parent that we're done. */
        parent.NotifyClose(this);
    }

    public async Task DisconnectAsync()
    {
        await DisconnectImplAsync(true);
    }

    public async Task CloseAsync()
    {
        await DisconnectImplAsync(false);
    }

    public async Task<BeaconPacket?> GetBeaconPacket()
    {
        /* Use cached packet if already cached. */
        if (beaconPacket != null)
        {
            return beaconPacket;
        }

        /* Send a query packet. */
        await SendBeaconTaskImplAsync(QueryBeaconPacketServiceId);
        return beaconPacket;
    }

    public int GetUsbDeviceNumber()
    {
        return usbIf.GetUsbDeviceNumber();
    }

    public async Task SendNormalTaskImplAsync(UInt32 serviceId)
    {
        /* Query via get info cmd. */
        var task = new NormalTask(serviceId, managerService);
        serviceManager.RegisterNewTask(task);
        
        /* Wait for it to finish. */
        await task.WaitDoneAsync();
    }

    public async Task SendBeaconTaskImplAsync(UInt32 serviceId)
    {
        /* Query via get info cmd. */
        var task = new BeaconTask(serviceId, managerService);
        serviceManager.RegisterNewTask(task);
        
        /* Wait for it to finish. */
        await task.WaitDoneAsync();

        /* Parse the beacon packet. */
        string beaconStr = task.BeaconPacket.ReadString(0x300);
        if (beaconPacket == null)
        {
            beaconPacket = new BeaconPacket(beaconStr);
        }
        
        /* Release the packet. */
        task.Dispose();
    }

    private void NotifyAbruptClose()
    {
        using var l = abruptDisconnectLock.EnterScope();

        /* If the other thread already took care of things, do nothing. */
        if (abruptDisconnectHandled)
            return;
        
        /* Indicate that handling has finished. */
        abruptDisconnectHandled = true;
        
        /* Notify parent so it can do cleanup. */
        parent.NotifyAbruptClose(this);
    }
    
    private Error RecvPacketImpl(ref Packet pkt)
    {
        /* Receive the packet. */
        return usbIf.Read(pkt.GetBuffer(), 0, Packet.PacketMaxSize, out int sizeRead);
    }

    private Error SendPacketImpl(Packet pkt)
    {
        /* First, send the header. */
        byte[] buf = pkt.GetBuffer();
        Error err  = usbIf.Write(buf, 0, Packet.HeaderSize, out int hdrSize);
        if (err != Error.Success)
            return err;
        //Console.WriteLine("sendidx = {0}, {1} bytes", j++, pkt.dataSize);

        /* Then the data. */
        //Console.WriteLine(pkt.GetDataSize());
        return usbIf.Write(buf, Packet.HeaderSize, pkt.dataSize, out int dataSize);
    }

    private async Task StopThreadsAsync()
    {
        /* Create the cancellation semaphore. */
        cancelSem = new(0);
        
        /* Cancel current transfers. */
        cancelSrc.Cancel();
        usbIf.Cancel();
        
        /* Wait for both threads to exit. */
        await cancelSem.WaitAsync();
        await cancelSem.WaitAsync();
        
        /* Join them. */
        recvThread.Join();
        sendThread.Join();
    }
    
    int j =0;
    void SendThread()
    {
        while (true)
        {
            /* Pop a packet. */
            Packet pkt;
            try
            {
                pkt = serviceManager.PopSendPacket();
            }
            catch(OperationCanceledException)
            {
                cancelSem.Release();
                return;
            }

            //File.Open(String.Format("send_{0}", j), FileMode.Create).Write(pkt.GetBuffer());
            //pkt.Print();

            /* Send it. */
            Error err = SendPacketImpl(pkt);
            if (cancelSrc.Token.IsCancellationRequested)
            {
                cancelSem.Release();
                return;
            }

            /* Treat other errors as an abrupt disconnect. */
            if (err != Error.Success)
            {
                cancelSem.Release();
                NotifyAbruptClose();
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
            Packet pkt = serviceManager.AllocRecvPacket();

            /* Wait for a message. */
            Error err = RecvPacketImpl(ref pkt);
            if (cancelSrc.Token.IsCancellationRequested)
            {
                cancelSem.Release();
                return;
            }
            
            /* Treat other errors as an abrupt disconnect. */
            if (err != Error.Success)
            {
                cancelSem.Release();
                NotifyAbruptClose();
                return;
            }

            /* Send it to the ServiceManager. */
            Console.WriteLine("Recv {0}", i);
            //File.Open(String.Format("recv_{0}", i++), FileMode.Create).Write(pkt.GetBuffer());
            serviceManager.OnRecvPacket(pkt);
        }
    }
}
