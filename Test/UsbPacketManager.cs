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
    
    readonly Thread sendThread;
    readonly Thread recvThread;

    readonly PacketQueue packetQueue = new(20);

    readonly ServiceManager serviceManager;

    readonly UsbInterface usbIf;

    private BeaconPacket? beaconPacket;
    
    private bool isConnected;

    public UsbPacketManager(UsbInterface usb, ServiceManager mgr)
    {
        serviceManager = mgr;
        usbIf = usb;
        sendThread = new Thread(SendThread);
        recvThread = new Thread(RecvThread);

        beaconPacket = null;
        isConnected = false;
    }

    public void Dispose()
    {
        usbIf.Dispose();
    }

    public void Start()
    {
        sendThread.Start();
        Console.WriteLine("Started USB send thread");
        recvThread.Start();
        Console.WriteLine("Started USB recv thread");
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
            serialNumber   = sn,
            address        = GetUsbDeviceNumber().ToString(),
            connectionType = "USB"
        };
    }

    public void Connect()
    {
        /* Do nothing if we're already connected. */
        if (isConnected)
        {
            return;
        }
        
        /* Send a connect packet. */
        GetBeaconPacketImpl(InitializeServiceId);
        
        /* We're now connected. */
        isConnected = true;
    }

    public void GetBeaconPacket(out BeaconPacket? beacon)
    {
        /* Use cached packet if already cached. */
        if (beaconPacket != null)
        {
            beacon = beaconPacket;
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
    
    private void RecvPacketImpl(ref Packet pkt)
    {
        Error err = usbIf.Read(pkt.GetBuffer(), 0, Packet.PacketMaxSize, 0, out int sizeRead);
    }

    private void SendPacketImpl(Packet pkt)
    {
        /* First, send the header. */
        byte[] buf = pkt.GetBuffer();
        Error err = usbIf.Write(buf, 0, Packet.HeaderSize, 0, out int hdrSize);
        if (err != Error.Success)
        {
            Console.WriteLine("USB Write Error: {0}", err);
            return;
        }
        //Console.WriteLine("sendidx = {0}, {1} bytes", j++, pkt.dataSize);

        /* Then the data. */
        //Console.WriteLine(pkt.GetDataSize());
        err = usbIf.Write(buf, Packet.HeaderSize, pkt.dataSize, 0, out int dataSize);
        if (err != Error.Success)
        {
            Console.WriteLine("USB Write Error: {0}", err);
            return;
        }
    }

    int j =0;
    void SendThread()
    {
        while (true)
        {
            /* Pop a packet. */
            Packet pkt = packetQueue.Take();
            //File.Open(String.Format("send_{0}", j), FileMode.Create).Write(pkt.GetBuffer());
            //pkt.Print();

            /* Send it. */
            SendPacketImpl(pkt);

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
            RecvPacketImpl(ref pkt);

            /* Send it to the ServiceManager. */
            Console.WriteLine("Recv {0}", i);
            //File.Open(String.Format("recv_{0}", i++), FileMode.Create).Write(pkt.GetBuffer());
            serviceManager.OnRecvPacket(pkt);
        }
    }
}
