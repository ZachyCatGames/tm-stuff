namespace Test;
using System.Collections.Concurrent;
using System.Data.SqlTypes;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualBasic;

public class TicsPacketManager : IPacketManager
{
    readonly Thread sendThread;
    readonly Thread recvThread;

    readonly PacketQueue packetQueue = new(20);

    readonly ServiceManager serviceManager;

    readonly Socket sock;

    /* Port is usually 8000. */
    public TicsPacketManager(IPEndPoint ip, ServiceManager mgr)
    {
        Console.WriteLine("tics: ctor");
        /* Create a socket. */
        sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        /* Connect to the console. */
        sock.Connect(ip);
        Console.WriteLine("tics: connected");

        serviceManager = mgr;

        sendThread = new Thread(SendThread);
        recvThread = new Thread(RecvThread);

        Init();
    }

    public void SendPacket(Packet pkt)
    {
        packetQueue.Add(pkt);
    }

    public Task SendPacketAsync(Packet pkt)
    {
        return packetQueue.AddAsync(pkt);
    }

    public void Start()
    {
        sendThread.Start();
        Console.WriteLine("Started TICS send thread");
        recvThread.Start();
        Console.WriteLine("Started TICS recv thread");
    }

    private struct TicsPacketHeader
    {
        public UInt32 size; // includes this hdr
        public UInt32 unknown;

        public TicsPacketHeader(UInt32 size, UInt32 unk)
        {
            this.size = size;
            this.unknown = unk;
        }

        public TicsPacketHeader(byte[] arr)
        {
            int len = Marshal.SizeOf(this);
            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.AllocHGlobal(len);
                Marshal.Copy(arr, 0, ptr, len);
                this = (TicsPacketHeader)Marshal.PtrToStructure(ptr, this.GetType());
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public void WriteTo(byte[] buf) {
            int len = Marshal.SizeOf(this);
            IntPtr ptr = Marshal.AllocHGlobal(len);
            Marshal.StructureToPtr(this, ptr, true);
            Marshal.Copy(ptr, buf, 0, len);
            Marshal.FreeHGlobal(ptr);
        }
    }

    private void Init()
    {
        byte[] buf = new byte[0x40];

        /* some shit to setup comms? */
        byte[] reply1 = {
            0x50, 0x00, 0x00, 0x00, 0x01, 0x00, 0x08, 0x00, 
        };
        byte[] reply2 = {
            0x01, 0x00, 0x00, 0x00, 0x48, 0x00, 0x00, 0x00, 0x00, 0x70, 0x80, 0x00, 0x5A, 0x61, 0x63, 0x68, 
            0x61, 0x72, 0x79, 0x40, 0x44, 0x45, 0x53, 0x4B, 0x54, 0x4F, 0x50, 0x2D, 0x34, 0x38, 0x47, 0x33, 
            0x55, 0x45, 0x37, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
        };
        byte[] reply3 = {
            0xB0, 0x00, 0x00, 0x00, 0x01, 0x00, 0x08, 0x00,
        };
        byte[] reply4 = {
            0x05, 0x00, 0x00, 0x00, 0xA8, 0x00, 0x00, 0x00, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x79, 0x00, 0x00, 0x00, 0x79, 0x00, 0x00, 0x00, 0x6F, 0x01, 0x00, 0x00, 0x5B, 0x74, 0x6D, 0x61, 
            0x5F, 0x68, 0x62, 0x5D, 0x0A, 0x54, 0x79, 0x70, 0x65, 0x3D, 0x43, 0x6F, 0x6D, 0x6D, 0x43, 0x68, 
            0x61, 0x6E, 0x6E, 0x65, 0x6C, 0x0A, 0x53, 0x63, 0x6F, 0x70, 0x65, 0x3D, 0x54, 0x61, 0x72, 0x67, 
            0x65, 0x74, 0x54, 0x6F, 0x42, 0x72, 0x69, 0x64, 0x67, 0x65, 0x54, 0x6F, 0x48, 0x6F, 0x73, 0x74, 
            0x0A, 0x50, 0x72, 0x69, 0x6F, 0x72, 0x69, 0x74, 0x79, 0x3D, 0x31, 0x0A, 0x4F, 0x70, 0x74, 0x69, 
            0x6D, 0x69, 0x7A, 0x65, 0x46, 0x6F, 0x72, 0x3D, 0x4C, 0x61, 0x74, 0x65, 0x6E, 0x63, 0x79, 0x0A, 
            0x44, 0x69, 0x72, 0x65, 0x63, 0x74, 0x69, 0x6F, 0x6E, 0x3D, 0x42, 0x69, 0x44, 0x69, 0x72, 0x65, 
            0x63, 0x74, 0x69, 0x6F, 0x6E, 0x61, 0x6C, 0x0A, 0x63, 0x66, 0x67, 0x70, 0x6F, 0x72, 0x74, 0x3D, 
            0x79, 0x65, 0x73, 0x0A, 0x00, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
        };
        byte[] reply5 = {
            0xFD, 0xFF, 0xFF, 0xFF, 0x00, 0x01, 0x08, 0x00, 
        };
        byte[] reply6 = {
            0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x08, 0x00, 
        };
        byte[] reply7 = {
            0x3A, 0x2F, 0xEB, 0x30, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x04, 0x00, 0x00, 0x00, 
        };
        Console.WriteLine("here2");
        RecvBlocking(new ArraySegment<byte>(buf, 0, 8));
        Console.WriteLine("{0}", BitConverter.ToString(buf, 0, 0x8));
        SendBlocking(buf[0..8]);
        Console.WriteLine("Here");
        SendBlocking(reply1);
        SendBlocking(reply2);
        Console.WriteLine("here3");
        RecvBlocking(buf[0..0x20]);
        Console.WriteLine("here5");
        SendBlocking(reply3);
        SendBlocking(reply4);
        Console.WriteLine("here4");
        RecvBlocking(new ArraySegment<byte>(buf, 0, 8)); // tics hdr for following
        Console.WriteLine("{0}", BitConverter.ToString(buf, 0, 8));
        RecvBlocking(new ArraySegment<byte>(buf, 0, 58));
        Console.WriteLine("{0}", BitConverter.ToString(buf, 0, 58));
        SendBlocking(reply5);


        SendBlocking(reply6); // tics hdr for following
        SendBlocking(reply7); // conn setup request

        Console.WriteLine("TICS: init done");
    }

    private void SendThread()
    {
        byte[] hdr = new byte[8];

        while (true)
        {
            /* Receive a packet from the service manager. */
            Packet pkt = packetQueue.Take();

            /* Setup tics header. */
            var hdrParsed = new TicsPacketHeader((uint)(0x8 + Packet.HeaderSize + pkt.dataSize), 0x00080100);
            hdrParsed.WriteTo(hdr);

            /* Send tics header. */
            SendBlocking(hdr);

            /* Send the packet. */
            SendBlocking(new ArraySegment<byte>(pkt.GetBuffer(), 0, Packet.HeaderSize + pkt.dataSize));

            /* Release the packet. */
            pkt.Release();
        }
    }

    private void RecvThread()
    {
        byte[] hdr = new byte[8];

        while (true)
        {
            /* Wait for a packet header. */
            RecvBlocking(new ArraySegment<byte>(hdr));

            /* Check type(?) */
            var hdrParsed = new TicsPacketHeader(hdr);
            if (hdrParsed.unknown != 0x00080100) {
                Console.WriteLine("Unknown TICS packet type: 0x{0:X08}", hdrParsed.unknown);
                continue;
            }

            /* Allocate a packet. */
            Packet pkt = serviceManager.AllocRecvPacket();

            /* Receive a header. */
            RecvBlocking(pkt.GetHeaderBuffer());

            /* Parse the header. */
            pkt.ParseHeader();

            /* Receive the rest of the packet. */
            RecvBlocking(pkt.GetDataBuffer(pkt.dataSize));

            if (pkt.serviceId == 0x30eb2f3a) {
                /* TODO: beacon packet. */
                    Console.WriteLine(System.Text.Encoding.ASCII.GetString(pkt.GetBuffer()[0x20..(0x20+pkt.dataSize)]));
            } else {
                /* Send it off the service manager. */
                serviceManager.OnRecvPacket(pkt);
            }
        }
    }

    private void RecvBlocking(ArraySegment<byte> bs)
    {
        //Console.WriteLine(bs.Count);
        int recvd = 0;
        int size = bs.Count;
        while (recvd < bs.Count) {
            int r = sock.Receive(bs[recvd..size]);
            recvd += r;
        }
    }

    private void SendBlocking(ArraySegment<byte> bs)
    {
        int sent = 0;
        int size = bs.Count;
        while (sent < bs.Count) {
            int r = sock.Send(bs[sent..size]);
            sent += r;
        }
    }

}
