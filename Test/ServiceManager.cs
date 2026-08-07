namespace Test;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

public class ServiceManager
{
    enum JobType
    {
        NewTask = 0,
        CancelTask,
        RecvPacket,
        Tick,
        End
    }

    struct Job
    {
        public Task? task;
        public Packet? packet;
        public JobType type;

        public Job(Task t, Packet p, JobType j)
        {
            task = t;
            packet = p;
            type = j;
        }

        public Job(Task t, JobType j)
        {
            task = t;
            type = j;
        }

        public Job(JobType j)
        {
            type = j;
        }
    }

    public const int SendPacketsCountMax = 20;
    public const int RecvPacketsCountMax = 20;
    public const int JobCountMax = 20;

    bool initialized = false;

    readonly List<Service> services = [];

    readonly BlockingCollection<Packet> freePacketsForSend = new(SendPacketsCountMax);
    readonly BlockingCollection<Packet> freePacketsForRecv = new(RecvPacketsCountMax);
    readonly TaskList taskList = new();
    IPacketManager packetMgr;

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
    }

    public void Init()
    {
        initialized = true;
    }

    public void SetPacketManager(IPacketManager mgr)
    {
        packetMgr = mgr;
    }

    public IPacketManager GetPacketManager()
    {
        return packetMgr;
    }

    public void SendPacket(Packet pkt)
    {
        packetMgr.SendPacket(pkt);
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

    public Packet AllocSendPacket()
    {
        Console.WriteLine("Fuck {0}", freePacketsForSend.Count);
        return freePacketsForSend.Take();
    }

    // called by PacketManager recv thread
    public void OnNewPacket(Packet pkt)
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
                if (service.GetServiceId() == serviceId)
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
