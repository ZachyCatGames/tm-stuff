using System.Net;
using System.Net.Sockets;
using Microsoft.VisualBasic;

namespace Test.test;

public class ProxyService : Service
{
    readonly Thread workThread;
    Socket connection;
    readonly Socket sock;

    Task sendTask;
    Task recvTask;
    
    PacketQueue freePackets = new(10);
    PacketQueue sendPackets = new(10);
    
    CancellationTokenSource cancelSrc;
    
    class ProxyTask : ServiceTask
    {
        public ProxyTask(ProxyService par, TaskType type, uint taskId, uint prio) : base(par, type, taskId, prio)
        {}

        protected override async Task Run()
        {
            Packet pkt = await WaitForPacket();
            await ((ProxyService)parent).sendPackets.AddAsync(pkt);
        }
    }

    public ProxyService(uint serviceId, ServiceManager mgr) : base(serviceId, mgr)
    {
        workThread = new(WorkThreadFunc);
        sock = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        for (int i = 0; i < 10; i++)
        {
            freePackets.Add(new Packet(freePackets));
        }
        Console.WriteLine("cringe {0}", freePackets.GetFreeCount());

        workThread.Start();
    }

    public override ServiceTask? ProcessNewRequest(Packet pkt)
    {
        return new ProxyTask(this, pkt.taskType, pkt.taskId, 0);
    }

    // send to console
    async Task RecvWorker(Socket connection)
    {
        while (true)
        {
            Packet pkt = await freePackets.TakeAsync();
            try {
                int c = await connection.ReceiveAsync(new ArraySegment<byte>(pkt.GetBuffer(), 0, Packet.HeaderSize), cancelSrc.Token);
                if (c == 0)
                    return;
                
                pkt.Reset();
                pkt.ParseHeader();
                await connection.ReceiveAsync(new ArraySegment<byte>(pkt.GetBuffer(), Packet.HeaderSize, pkt.dataSize), cancelSrc.Token);
            }
            catch(OperationCanceledException)
            {
                pkt.Release();
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            pkt.Print();
            
            ProxyTask task = new(this, pkt.taskType, pkt.taskId, 0);
            serviceManager.RegisterTask(task);
            task.StartImpl();
            await serviceManager.SendPacketAsync(pkt);
        }
    }
    
    // recv from console
    async Task SendWorker(Socket connection)
    {
        while(true)
        {
            Packet pkt = await sendPackets.TakeAsync();
            Console.WriteLine("Sending packet");
            try {
                int c = await connection.SendAsync(new ArraySegment<byte>(pkt.GetBuffer(), 0, Packet.HeaderSize + pkt.dataSize));
                if (c == 0)
                    return;
            }
            catch(OperationCanceledException)
            {
                pkt.Release();
                return;
            }

            pkt.Release();
        }
    }
    
    async void WorkThreadFunc()
    {
        sock.Bind(new IPEndPoint(0x0100007F, 9500));
        sock.Listen();
        Console.WriteLine("ProxyService for {0:X} listening on port {1}", serviceId, ((IPEndPoint)sock.LocalEndPoint).Port);

        connection = await sock.AcceptAsync();

        while (true)
        {
            cancelSrc = new CancellationTokenSource();

            var recvTask = RecvWorker(connection);
            var sendTask = SendWorker(connection);
            
            var newConn = await sock.AcceptAsync();
            await cancelSrc.CancelAsync();
            await recvTask;
            //await sendTask;
            connection.Close();
            connection = newConn;
            Console.WriteLine("Close conn");
        }
    }
}