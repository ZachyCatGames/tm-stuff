namespace Test;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

public class ServiceManager
{
    public const int SendPacketsCountMax = 20;
    public const int RecvPacketsCountMax = 20;
    public const int JobCountMax = 20;

    bool initialized = false;

    readonly List<Service> services = [];

    readonly PacketQueue freePacketsForSend = new(SendPacketsCountMax);
    readonly PacketQueue freePacketsForRecv = new(RecvPacketsCountMax);
    readonly TaskList taskList = new();

    IPacketManager packetMgr;
    readonly Thread workThread;
    readonly PacketQueue packetQueue = new (SendPacketsCountMax + RecvPacketsCountMax);

    public ServiceManager()
    {
        for (int i = 0; i < SendPacketsCountMax; i++)
        {
            freePacketsForSend.Add(new Packet(freePacketsForSend));
        }

        for (int i = 0; i < RecvPacketsCountMax; i++)
        {
            freePacketsForRecv.Add(new Packet(freePacketsForRecv));
        }
        
        workThread = new(WorkThreadFunc);
    }

    public void Init(IPacketManager mgr)
    {
        initialized = true;
        packetMgr = mgr;
        workThread.Start();
    }
    
    // for use by packet mgr.
    public void OnRecvPacket(Packet pkt)
    {
        packetQueue.Add(pkt);
    }

    public IPacketManager GetPacketManager()
    {
        return packetMgr;
    }

    public void SendPacket(Packet pkt)
    {
        packetMgr.SendPacket(pkt);
    }
    
    public Task SendPacketAsync(Packet pkt)
    {
        return packetMgr.SendPacketAsync(pkt);
    }

    public void RegisterService(Service srv)
    {
        services.Add(srv);
    }

    public void RegisterTask(ServiceTask task)
    {
        taskList.AddTask(task);
    }

    public void NotifyTaskDone(ServiceTask task)
    {
        taskList.RemoveTask(task);
    }

    public Packet AllocRecvPacket()
    {
        return freePacketsForRecv.Take();
    }

    public Task<Packet> AllocRecvPacketAsync()
    {
        return freePacketsForRecv.TakeAsync();
    }

    public Packet AllocSendPacket()
    {
        //Console.WriteLine("Fuck {0}", freePacketsForSend.Count);
        return freePacketsForSend.Take();
    }

    public Task<Packet> AllocSendPacketAsync()
    {
        return freePacketsForSend.TakeAsync();
    }
    
    public async void WorkThreadFunc()
    {
        while (true)
        {
            /* Wait for a new packet. */
            Packet pkt = await packetQueue.TakeAsync();
            
            /* Call impl on it. */
            ProcessRecvPacketImpl(pkt);
        }
    }

    // called by PacketManager recv thread
    public void ProcessRecvPacketImpl(Packet pkt)
    {
        /* Parse the header. */
        pkt.ParseHeader();
        pkt.Reset();

        /* Is this an initiate message? */
        if (pkt.isInitiate)
        {
            /* Dispatch the packet to its target Service. */
            var serviceId = pkt.serviceId;
            foreach (var service in services)
            {
                if (service.serviceId == serviceId)
                {
                    if (service.ProcessNewRequest(pkt) is ServiceTask task)
                    {
                        this.RegisterTask(task);
                        task.SignalIncomingPacket(pkt);
                        task.StartImpl();
                    }
                    else
                    {
                        pkt.Release();
                    }

                    return;
                }
            }
            Console.WriteLine("[ServiceManager] Received packet for unknown serviceId: {0:X}", serviceId);
            pkt.Print();
        }
        else
        {
            /* Find the target service. */
            if (taskList.FindByTaskById(pkt.taskId) is ServiceTask task)
            {
                task.SignalIncomingPacket(pkt);
                return;
            }

            Console.WriteLine("[ServiceManager] Received packet for unknown taskId: {0}", pkt.taskId);
            pkt.Print();
        }
        pkt.Release();
    }
}
