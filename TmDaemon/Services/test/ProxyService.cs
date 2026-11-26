using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Test.Packets;

namespace Test.Services.test;

public class ProxyService : ServiceBase
{
    private readonly Thread workThread;
    private Socket? connection;
    private readonly Socket sock;
    
    private PacketQueue freePackets = new(10);
    private PacketQueue sendPackets = new(10);
    
    private CancellationTokenSource? cancelSrc;
    
    class ProxyTask : ServiceTask
    {
        public ProxyTask(ProxyService par, TaskType type, uint taskId, uint prio) : base(par, type, taskId, prio)
        {}

        protected override async Task Run()
        {
            Packets.Packet pkt = await WaitForPacket();
            await ((ProxyService)this.parent).sendPackets.AddAsync(pkt);
        }
    }

    public ProxyService(uint serviceId, ServiceManager mgr) : base(serviceId, mgr)
    {
        workThread = new(this.WorkThreadFunc);
        sock = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        for (int i = 0; i < 10; i++)
        {
            this.freePackets.Add(new Packets.Packet(freePackets));
        }
        Console.WriteLine("cringe {0}", this.freePackets.GetFreeCount());

        this.workThread.Start();
    }

    public override ServiceTask? ProcessNewRequest(Packets.Packet pkt)
    {
        return new ProxyTask(this, pkt.taskType, pkt.taskId, 0);
    }

    // send to console
    async Task RecvWorker()
    {
        Debug.Assert(this.connection != null);
        Debug.Assert(this.cancelSrc != null);
        
        while (true)
        {
            Packets.Packet pkt;
            try
            {
                pkt = await this.freePackets.TakeAsync(this.cancelSrc.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            
            try {
                int recvd = 0;
                while (recvd < Packets.Packet.HeaderSize)
                {
                    int c = await connection.ReceiveAsync(new ArraySegment<byte>(pkt.GetBuffer(), recvd, Packets.Packet.HeaderSize + recvd), cancelSrc.Token);
                    if (c == 0)
                    {
                        return;
                    }
                    recvd += c;
                }
  
                pkt.Reset();
                pkt.ParseHeader();
                Console.WriteLine("recv pkt size {0}, {1}", pkt.dataSize, new ArraySegment<byte>(pkt.GetBuffer(), Packets.Packet.HeaderSize, pkt.dataSize).Count);
                
                recvd = 0;
                while (recvd < pkt.dataSize)
                {
                    int c = await connection.ReceiveAsync(new ArraySegment<byte>(pkt.GetBuffer(), Packets.Packet.HeaderSize + recvd, pkt.dataSize - recvd), cancelSrc.Token);
                    if (c == 0)
                    {
                        return;
                    }
                    recvd += c;
                    Console.WriteLine("recvd {0} {1}", recvd, c);
                }
            }
            catch(OperationCanceledException)
            {
                pkt.Release();
                return;
            }
            catch (Exception e)
            {
                pkt.Release();
                Console.WriteLine(e);
                return;
            }
            pkt.Print();
            
            ProxyTask task = new(this, pkt.taskType, pkt.taskId, 0);
            this.serviceManager.RegisterNewTask(task);
            await this.serviceManager.SendPacketAsync(pkt);
        }
    }

    // recv from console
    async Task SendWorker()
    {
        Debug.Assert(this.cancelSrc != null);
        Debug.Assert(this.connection != null);
        
        while(true)
        {
            Packets.Packet pkt;
            try
            {
                pkt = await this.sendPackets.TakeAsync(this.cancelSrc.Token);
            }
            catch(OperationCanceledException)
            {
                return;
            }

            Console.WriteLine("Sending packet");
            try {
                int c = await connection.SendAsync(new ArraySegment<byte>(pkt.GetBuffer(), 0, Packets.Packet.HeaderSize + pkt.dataSize));
                if (c == 0)
                    return;
            }
            catch(OperationCanceledException)
            {
                pkt.Release();
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                pkt.Release();
                return;
            }

            pkt.Release();
        }
    }
    
    async void WorkThreadFunc()
    {
        this.sock.Bind(new IPEndPoint(0x0100007F, 9500));
        this.sock.Listen();
        Console.WriteLine("ProxyService for {0:X} listening on port {1}", this.serviceId, ((IPEndPoint)sock.LocalEndPoint).Port);

        this.connection = await sock.AcceptAsync();

        while (true)
        {
            cancelSrc = new CancellationTokenSource();

            var recvTask = this.RecvWorker();
            var sendTask = this.SendWorker();
            
            var newConn = await this.sock.AcceptAsync();
            await this.cancelSrc.CancelAsync();
            await recvTask;
            await sendTask;
            //await sendTask;
            this.connection.Close();
            this.connection = newConn;
            Console.WriteLine("Close conn");
        }
    }
}