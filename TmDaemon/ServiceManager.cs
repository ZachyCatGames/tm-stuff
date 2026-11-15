using System.Threading.Channels;

namespace Test;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

public class ServiceManager
{
    public const int SendPacketsCountMax = 20;
    public const int RecvPacketsCountMax = 20;
    public const int JobCountMax         = 20;

    private bool initialized = false;
    
    private bool          isDone = false;
    private SemaphoreSlim doneEv = new(0);
    //private EventWaitHandle doneEv = new(false, EventResetMode.ManualReset);

    private readonly List<IService> services = new();
    private readonly TaskList      taskList = new();

    private readonly PacketQueue freePacketsForSend = new(SendPacketsCountMax);
    private readonly PacketQueue freePacketsForRecv = new(RecvPacketsCountMax);
    
    private readonly Channel<Packet>    sendQueue = Channel.CreateUnbounded<Packet>();
    private readonly Thread             workThread;
    private readonly Channel<WorkerCmd> cmdQueue = Channel.CreateUnbounded<WorkerCmd>();
    //private readonly PacketQueue    packetQueue = new (SendPacketsCountMax + RecvPacketsCountMax);
    
    private readonly SemaphoreSlim cancelTasksSem = new(0);

    public bool InitiaiteLock { get; set; }
    
    private enum CmdType
    {
        NewPacket,
        CancelAllTasks,
        FlushTasks,
        Close,
        Tick
    }

    private class WorkerCmd
    {
        public CmdType type;
        public Packet? pkt;

        public WorkerCmd(CmdType type, Packet pkt)
        {
            this.type = type;
            this.pkt = pkt;
        }

        public WorkerCmd(CmdType type)
        {
            this.type = type;
        }
    }
    
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
        
        /* Start the worker thread. */
        workThread = new(WorkThreadFunc);
        workThread.Start();
    }
    
    public void Finalize()
    {
        /* Notify the worker that we're done. */
        cmdQueue.Writer.TryWrite(new WorkerCmd(CmdType.Close));

        /* Wait for the thread to finish. */
        workThread.Join();
    }
    
    /* External service/task management. */
    public void RegisterService(IService srv) => services.Add(srv);

    public void RegisterNewTask(ServiceTask task)
    {
        taskList.AddTask(task);
        task.StartImpl();
    }

    public async Task CancelAllTasksAsync()
    {
        //cancelTasksSem = new(0);
        cmdQueue.Writer.TryWrite(new WorkerCmd(CmdType.CancelAllTasks));
        await cancelTasksSem.WaitAsync();
    }
    
    /* Packet allocators. */
    public Packet AllocRecvPacket() => freePacketsForRecv.Take();

    public Task<Packet> AllocRecvPacketAsync() => freePacketsForRecv.TakeAsync();

    public Packet AllocSendPacket() => freePacketsForSend.Take();

    public Task<Packet> AllocSendPacketAsync() => freePacketsForSend.TakeAsync();
    
    /* These are for tasks to send replies or w/e */
    public void SendPacket(Packet pkt)
    {
        pkt.WriteHeader();
        sendQueue.Writer.TryWrite(pkt);
    }

    public async Task SendPacketAsync(Packet pkt)
    {
        pkt.WriteHeader();
        await sendQueue.Writer.WriteAsync(pkt);
    }
    
    /* These are for the packet manager. */
    public void OnRecvPacket(Packet pkt) => cmdQueue.Writer.TryWrite(new WorkerCmd(CmdType.NewPacket, pkt));

    public async Task<Packet> PopSendPacketAsync(CancellationToken ct) => await sendQueue.Reader.ReadAsync(ct);
    
    private async void WorkThreadFunc()
    {
        bool done = false;
        while (!done)
        {
            /* Wait for a new packet. */
            WorkerCmd workerCmd;
            try
            {
                workerCmd = await cmdQueue.Reader.ReadAsync();
            }
            catch (OperationCanceledException)
            {
                Debug.Assert(isDone);
                break;
            }
            
            /* Remove any finished tasks. */
            taskList.RemoveIf((task) => task.IsDone());

            switch (workerCmd.type)
            {
                case CmdType.NewPacket:
                    Debug.Assert(workerCmd.pkt != null);
                    ProcessRecvPacketImpl(workerCmd.pkt);
                    break;
                case CmdType.CancelAllTasks:
                    CancelAllTasksImpl();
                    cancelTasksSem.Release();
                    break;
                case CmdType.Close:
                    done = true;
                    break;
                case CmdType.Tick:
                    break;
            }
        }
    }
    
    private void CancelAllTasksImpl()
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

    // called by PacketManager recv thread
    private void ProcessRecvPacketImpl(Packet pkt)
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
                if (service.AcceptsServicesId(serviceId))
                {
                    if (service.ProcessNewRequest(pkt) is ServiceTask task)
                    {
                        RegisterNewTask(task);
                        task.SignalIncomingPacket(pkt);
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
            /* If the initiate lock is active and this isn't a manager packet, drop it. */
            if (InitiaiteLock && (pkt.taskId & HostTaskIdAllocator.TaskIdOriginBitsMask) == HostTaskIdAllocator.TaskIdForMgr)
            {
                pkt.Release();
                return;
            }
            
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
