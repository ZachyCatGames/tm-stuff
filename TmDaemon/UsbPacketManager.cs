using System.Diagnostics;

namespace Test;
using System.Collections.Concurrent;
using System.Threading;
using LibUsbDotNet;

public class UsbPacketManager : IPacketManager
{
    private const UInt32 InitializeServiceId        = 0xcd5ed1b8;
    private const UInt32 FinalizeServiceId          = 0xf97fe6b9;
    private const UInt32 QueryBeaconPacketServiceId = 0x3a8ddd94;
    private const UInt32 SendHostInfoServiceId      = 0x4d4a6c24;
    
    private readonly Thread sendThread;
    private readonly Thread recvThread;

    private readonly UsbTargetProvider parent;

    private readonly PacketQueue packetQueue = new(20);

    private readonly ServiceManager serviceManager;

    private readonly UsbInterface usbIf;

    private BeaconPacket? beaconPacket;
    
    private bool isConnected;
    private bool isCommsDisabled;
    private long recvDone, sendDone;
    
    private readonly CancellationTokenSource cancelSrc = new();

    private bool abruptDisconnectHandled = false;
    private readonly Lock abruptDisconnectLock = new();

    public UsbPacketManager(UsbInterface usb, ServiceManager mgr, UsbTargetProvider parent)
    {
        serviceManager = mgr;
        usbIf          = usb;
        this.parent    = parent;
        
        sendThread = new Thread(SendThread);
        recvThread = new Thread(RecvThread);

        beaconPacket    = null;
        isConnected     = false;
        isCommsDisabled = false;
        recvDone        = 0;
        sendDone        = 0;
    }

    public void Dispose()
    {
        /* Disconnect if we're connected. */
        if (isConnected)
            Disconnect();

        /* Dispose the USB Interface. */
        usbIf.Dispose();
    }

    public void SendPacket(Packet pkt)
    {
        packetQueue.Add(pkt);
    }

    public Task SendPacketAsync(Packet pkt)
    {
        return packetQueue.AddAsync(pkt);
    }
    
    public TargetInfo GetTargetInfo()
    {
        /* Get the beacon packet. */
        GetBeaconPacket(out BeaconPacket? pkt);

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

    public async Task Connect()
    {
        /* Do nothing if we're already connected. */
        if (isConnected)
            return;
        
        /* Send a connect packet. */
        GetBeaconPacketImpl(InitializeServiceId);
        
        /* Start the send and receive threads. */
        sendThread.Start();
        Console.WriteLine("Started USB send thread");
        recvThread.Start();
        Console.WriteLine("Started USB recv thread");
        
        /* We're now connected. */
        isConnected = true;
    }

    public void Disconnect()
    {
        Debug.Assert(isConnected);
        
        /* Stop send and recv threads. */
        StopThreads();
        
        /* Tell our parent that we're done. */
        parent.NotifyDisconnect(this);
    }

    public void GetBeaconPacket(out BeaconPacket? beacon)
    {
        /* Use cached packet if already cached. */
        if (beaconPacket != null)
        {
            beacon = beaconPacket;
            return;
        }

        /* Send a query packet. */
        GetBeaconPacketImpl(QueryBeaconPacketServiceId);
        beacon = beaconPacket;
    }

    public int GetUsbDeviceNumber()
    {
        return usbIf.GetUsbDeviceNumber();
    }

    public void GetBeaconPacketImpl(UInt32 serviceId)
    {
        /* Query via get info cmd. */
        Packet pkt = serviceManager.AllocRecvPacket();
        pkt.Reset();
        pkt.serviceId = serviceId;
        pkt.taskId = 0;
        pkt.isInitiate = true;
        pkt.Write((UInt32)0);
        pkt.WriteHeader();
        SendPacketImpl(pkt);
        
        /* Receive the beacon packet. */
        RecvPacketImpl(ref pkt);
        
        /* Parse the beacon packet. */
        string beaconStr = pkt.ReadString(0x1000);
        if (beaconPacket == null)
        {
            beaconPacket = new BeaconPacket(beaconStr);
        }
        
        /* Release the packet. */
        pkt.Release();
    }

    private void NotifyAbruptDisconnect()
    {
        using var l = abruptDisconnectLock.EnterScope();

        /* If the other thread already took care of things, do nothing. */
        if (abruptDisconnectHandled)
            return;
        
        /* Indicate that handling has finished. */
        abruptDisconnectHandled = true;
        
        /* Notify parent so it can do cleanup. */
        parent.NotifyAbruptDisconnect(this);
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

    void StopThreads()
    {
        /* Cancel current transfers. */
        cancelSrc.Cancel();
        usbIf.Cancel();
        
        /* Wait for both threads to exit. */
        /* TODO: This "should" probably use async semaphores or similar. */
        while (Interlocked.Read(ref recvDone) == 0) { }
        while (Interlocked.Read(ref sendDone) == 0) { }
        
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
                pkt = packetQueue.Take(cancelSrc.Token);
            }
            catch(OperationCanceledException)
            {
                Interlocked.Exchange(ref sendDone, 1);
                return;
            }

            //File.Open(String.Format("send_{0}", j), FileMode.Create).Write(pkt.GetBuffer());
            //pkt.Print();

            /* Send it. */
            Error err = SendPacketImpl(pkt);
            if (cancelSrc.Token.IsCancellationRequested)
            {
                Interlocked.Exchange(ref sendDone, 1);
                return;
            }

            /* Treat other errors as an abrupt disconnect. */
            if (err != Error.Success)
            {
                Interlocked.Exchange(ref sendDone, 1);
                NotifyAbruptDisconnect();
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
                Interlocked.Exchange(ref recvDone, 1);
                return;
            }
            
            /* Treat other errors as an abrupt disconnect. */
            if (err != Error.Success)
            {
                Interlocked.Exchange(ref recvDone, 1);
                NotifyAbruptDisconnect();
                return;
            }

            /* Send it to the ServiceManager. */
            Console.WriteLine("Recv {0}", i);
            //File.Open(String.Format("recv_{0}", i++), FileMode.Create).Write(pkt.GetBuffer());
            serviceManager.OnRecvPacket(pkt);
        }
    }
}
