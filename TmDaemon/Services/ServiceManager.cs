using System.Diagnostics;
using System.Threading.Channels;
using Test.Packets;

namespace Test.Services;

public class ServiceManager
{
    public const int SendPacketsCountMax = 20;
    public const int RecvPacketsCountMax = 20;
    public const int JobCountMax         = 20;
    
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

    private readonly SemaphoreSlim sendLockLock = new(1); // society
    private readonly SemaphoreSlim sendWaitLock = new(0);
    private int sendLockWaiterCount = 0;
    private bool sendLock = false;
    public bool SendLock
    {
        get => sendLock;
        set
        {
            using var s = AsyncLockHolder.Enter(sendLockLock);
            if (value != sendLock)
            {
                if (value)
                {
                    sendLock = true;
                }
                else
                {
                    sendLock = false;
                    for (int i = 0; i < sendLockWaiterCount; i++)
                    {
                        sendWaitLock.Release();
                    }
                }
            }
        }
    }

    private async Task WaitForSendLock()
    {
        bool lockEn;
        using (var s = await AsyncLockHolder.EnterAsync(sendLockLock))
        {
            lockEn = sendLock;
            if (sendLock)
            {
                sendLockWaiterCount++;
            }
        }

        if (lockEn)
        {
            await sendWaitLock.WaitAsync();
        }

    }

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
        public CmdType Type;
        public Packet? Pkt;

        public WorkerCmd(CmdType type, Packet pkt)
        {
            this.Type = type;
            this.Pkt = pkt;
        }

        public WorkerCmd(CmdType type) => this.Type = type;
    }
    
    public ServiceManager()
    {
        for (int i = 0; i < SendPacketsCountMax; i++)
        {
            this.freePacketsForSend.Add(new Packet(this.freePacketsForSend));
        }

        for (int i = 0; i < RecvPacketsCountMax; i++)
        {
            this.freePacketsForRecv.Add(new Packet(this.freePacketsForRecv));
        }
        
        /* Start the worker thread. */
        this.workThread = new(this.WorkThreadFunc);
        this.workThread.Start();
    }
    
    public void Finalize()
    {
        /* Notify the worker that we're done. */
        this.cmdQueue.Writer.TryWrite(new WorkerCmd(CmdType.Close));

        /* Wait for the thread to finish. */
        this.workThread.Join();
    }
    
    /* External service/task management. */
    public void RegisterService(IService srv) => this.services.Add(srv);

    public void RegisterNewTask(ServiceTask task)
    {
        this.taskList.AddTask(task);
        task.StartImpl();
    }

    public async Task CancelAllTasksAsync()
    {
        //cancelTasksSem = new(0);
        this.cmdQueue.Writer.TryWrite(new WorkerCmd(CmdType.CancelAllTasks));
        await this.cancelTasksSem.WaitAsync();
    }
    
    /* Packet allocators. */
    public Packet AllocRecvPacket()
    {
        Packet pkt = this.freePacketsForRecv.Take();
        pkt.Reset();
        return pkt;
    }

    public async Task<Packet> AllocRecvPacketAsync()
    {
        Packet pkt = await this.freePacketsForRecv.TakeAsync();
        pkt.Reset();
        return pkt;
    }

    public Packet AllocSendPacket()
    {
        Packet pkt = this.freePacketsForSend.Take();
        pkt.Reset();
        return pkt;
    }

    public async Task<Packet> AllocSendPacketAsync()
    {
        Packet pkt = await this.freePacketsForSend.TakeAsync();
        pkt.Reset();
        return pkt;
    }
    
    /* These are for tasks to send replies or w/e */
    public void SendPacket(Packet pkt)
    {
        this.WaitForSendLock().Wait();
        pkt.WriteHeader();
        this.sendQueue.Writer.TryWrite(pkt);
    }

    public async Task SendPacketAsync(Packet pkt)
    {
        await this.WaitForSendLock();
        pkt.WriteHeader();
        await this.sendQueue.Writer.WriteAsync(pkt);
    }

    public async Task SendPacketByForceAsync(Packet pkt)
    {
        pkt.WriteHeader();
        await this.sendQueue.Writer.WriteAsync(pkt);
    }
    
    /* These are for the packet manager. */
    public void OnRecvPacket(Packet pkt) => this.cmdQueue.Writer.TryWrite(new WorkerCmd(CmdType.NewPacket, pkt));

    public async Task<Packet> PopSendPacketAsync(CancellationToken ct) => await this.sendQueue.Reader.ReadAsync(ct);
    
    private async void WorkThreadFunc()
    {
        bool done = false;
        while (!done)
        {
            /* Wait for a new packet. */
            WorkerCmd workerCmd;
            try
            {
                workerCmd = await this.cmdQueue.Reader.ReadAsync();
            }
            catch (OperationCanceledException)
            {
                Debug.Assert(this.isDone);
                break;
            }
            
            /* Remove any finished tasks. */
            taskList.RemoveIf((task) => task.IsDone());

            switch (workerCmd.Type)
            {
                case CmdType.NewPacket:
                    Debug.Assert(workerCmd.Pkt != null);
                    ProcessRecvPacketImpl(workerCmd.Pkt);
                    break;
                case CmdType.CancelAllTasks:
                    CancelAllTasksImpl();
                    this.cancelTasksSem.Release();
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
            foreach (var service in this.services)
            {
                if (service.AcceptsServicesId(serviceId))
                {
                    if (service.ProcessNewRequest(pkt) is ServiceTask task)
                    {
                        this.RegisterNewTask(task);
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
