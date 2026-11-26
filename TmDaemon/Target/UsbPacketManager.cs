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

    private readonly SemaphoreSlim cancelSem = new(0);

    public LocationId UsbLocationId { get; init; }
    
    public bool IsSuspended { get; private set; }
    
    public UsbPacketManager(UsbDevice usb, ServiceManager mgr, UsbTargetProvider parent)
    {
        serviceManager = mgr;
        usbIf          = new UsbInterface(usb);
        this.parent    = parent;

        UsbLocationId = usbIf.UsbLocationId;

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
        //await this.ConnectImplAsync(1);
        
        /* Clear suspended flag. */
        this.IsSuspended = false;
        
        Console.WriteLine("[usb] resumed");
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
