using System.Net;
using System.Net.Sockets;
using Microsoft.VisualBasic;

namespace Test.test;

public class ProxyService : Service
{
    readonly Thread workThread;
    
    readonly Socket sock;
    Socket connection;
    
    Task sendTask;
    Task recvTask;
    
    PacketQueue freePackets = new(10);
    PacketQueue sendPackets = new(10);
    
    readonly CancellationTokenSource cancelSrc = new();
    
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

        workThread.Start();
    }

    public override ServiceTask? ProcessNewRequest(Packet pkt)
    {
        sendPackets.Add(pkt);
        return new ProxyTask(this, pkt.taskType, pkt.taskId, 0);
    }

    // send to console
    async Task RecvWorker()
    {
        Packet pkt = await freePackets.TakeAsync();
        try {
            await connection.ReceiveAsync(new ArraySegment<byte>(pkt.GetBuffer(), 0, Packet.HeaderSize), cancelSrc.Token);
            pkt.ParseHeader();
            await connection.ReceiveAsync(new ArraySegment<byte>(pkt.GetBuffer(), Packet.HeaderSize, pkt.GetDataSize()), cancelSrc.Token);
        }
        catch(OperationCanceledException)
        {
            pkt.Release();
            return;
        }
        await serviceManager.SendPacketAsync(pkt);
    }
    
    // recv from console
    async Task SendWorker()
    {
        Packet pkt = await sendPackets.TakeAsync();
        try {
            await connection.SendAsync(new ArraySegment<byte>(pkt.GetBuffer(), 0, Packet.HeaderSize + pkt.GetDataSize()));
        }
        catch(OperationCanceledException)
        {
            pkt.Release();
            return;
        }
        
        pkt.Release();
    }
    
    async void WorkThreadFunc()
    {
        sock.Bind(new IPEndPoint(0x0100007F, 0));
        sock.Listen();
        Console.WriteLine("ProxyService for {0:X} listening on port {1}", serviceId, ((IPEndPoint)sock.LocalEndPoint).Port);

        connection = await sock.AcceptAsync();

        while (true)
        {
            recvTask = RecvWorker();
            sendTask = SendWorker();
            
            var newConn = await sock.AcceptAsync();
            await cancelSrc.CancelAsync();
            connection.Close();
            connection = newConn;
        }
    }
}