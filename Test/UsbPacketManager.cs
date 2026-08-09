namespace Test;
using System.Collections.Concurrent;
using System.Threading;
using LibUsbDotNet;

public class UsbPacketManager : IPacketManager
{
    readonly Thread sendThread;
    readonly Thread recvThread;

    readonly BlockingCollection<Packet> packetQueue = new(20);

    readonly ServiceManager serviceManager;

    readonly UsbInterface usbIf;

    public UsbPacketManager(UsbInterface usb, ServiceManager mgr)
    {
        serviceManager = mgr;
        usbIf = usb;
        sendThread = new Thread(SendThread);
        recvThread = new Thread(RecvThread);
    }

    public void StartThreads()
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

    int j =0;
    void SendThread()
    {
        while (true)
        {
            /* Pop a packet. */
            Packet pkt = packetQueue.Take();
            //File.Open(String.Format("send_{0}", j++), FileMode.Create).Write(pkt.GetBuffer());

            /* First, send the header. */
            byte[] buf = pkt.GetBuffer();
            Error err = usbIf.Write(buf, 0, Packet.HeaderSize, 0, out int hdrSize);
            if (err != Error.Success)
            {
                Console.WriteLine("USB Write Error: {0}", err);
                return;
            }

            /* Then the data. */
            //Console.WriteLine(pkt.GetDataSize());
            err = usbIf.Write(buf, Packet.HeaderSize, pkt.GetDataSize(), 0, out int dataSize);
            if (err != Error.Success)
            {
                Console.WriteLine("USB Write Error: {0}", err);
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
            Error err = usbIf.Read(pkt.GetBuffer(), 0, Packet.PacketMaxSize, 0, out int sizeRead);
            if (err != Error.Success)
            {
                Console.WriteLine("USB Read Error: {0}", err);
                return;
            }

            /* Send it to the ServiceManager. */
            //Console.WriteLine("Recv {0}", i);
            //File.Open(String.Format("recv_{0}", i++), FileMode.Create).Write(pkt.GetBuffer());
            serviceManager.OnNewPacket(pkt);
        }
    }
}
