namespace Test;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

public class ServiceManager
{
    public const int SendPacketsCountMax = 20;
    public const int RecvPacketsCountMax = 20;
    public const int JobCountMax = 20;

    private bool initialized = false;
    private bool isDone = false;
    private EventWaitHandle doneEv = new(false, EventResetMode.ManualReset);

    private readonly List<Service> services = new();
    private readonly TaskList taskList = new();

    private readonly PacketQueue freePacketsForSend = new(SendPacketsCountMax);
    private readonly PacketQueue freePacketsForRecv = new(RecvPacketsCountMax);

    private IPacketManager packetMgr;
    private readonly Thread workThread;
    private readonly PacketQueue packetQueue = new (SendPacketsCountMax + RecvPacketsCountMax);

    private readonly Lock packetProcessMutex = new();

    private readonly CancellationTokenSource exitTokenSource = new();
    
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

    public void Finalize()
    {
        /* Set done flag. */
        isDone = true;
        
        /* Cancel pending wait in worker. */
        exitTokenSource.Cancel();
        
        /* Wait for the worker to finish. */
        doneEv.WaitOne();
        
        /* Cancel all current taskList. */
        CancelAllTasks();
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

    public void RegisterTask(ServiceTask task)
    {
        taskList.AddTask(task);
    }

    public void CancelAllTasks()
    {
        taskList.ForEach((task) =>
        {
            /* Notify the task that it's canceled. */
            task.Cancel();
            
            /* Wait for it to finish, we don't care about the result. */
            try
            {
                task.WaitDone();
            }
            catch (Exception)
            {
                /* ... */
            }
        });
    }
    
    async void WorkThreadFunc()
    {
        while (true)
        {
            /* Wait for a new packet. */
            Packet pkt;
            try
            {
                pkt = await packetQueue.TakeAsync(exitTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Debug.Assert(isDone);
                
                /* Signal that we're done. */
                doneEv.Set();
                return;
            }

            /* Call impl on it. */
            using (var scope = packetProcessMutex.EnterScope())
            {
                ProcessRecvPacketImpl(pkt);
            }
            
            /* Remove any finished taskList. */
            taskList.RemoveIf((task) => task.IsDone());
        }
    }

    // called by PacketManager recv thread
    void ProcessRecvPacketImpl(Packet pkt)
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
                        taskList.AddTask(task);
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
