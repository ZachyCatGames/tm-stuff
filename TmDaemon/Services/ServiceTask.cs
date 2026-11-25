using Test.Packets;

namespace Test.Services;

public abstract class ServiceTask
{
    public readonly uint      serviceId;
    public readonly TaskType  type;
    public readonly TaskState state;
    public readonly uint      taskId;
    public readonly uint      priority;

    public bool hasFollowup;

    protected readonly IService parent;

    private PacketQueue packets = new(20);

    public Task task;

    private CancellationTokenSource cancelSrc = new();

    private readonly ServiceManager manager;
    
    public bool IsStarted { get; private set; }

    public ServiceTask(IService par, TaskType type, uint taskId, uint prio)
    {
        this.parent    = par;
        this.serviceId = par.GetPrimaryServiceId();
        this.type      = type;
        this.state     = TaskState.InProgress;
        this.taskId    = taskId;
        this.priority  = prio;
        this.manager   = par.GetManager();

        this.IsStarted = false;
    }
    
    public void Cancel() => cancelSrc.Cancel();

    private async Task StartImplAsync()
    {
        /* Start the task. */
        await Run();
        
        /* Clear any pending packets. */
        while (HasPendingRecvPacket())
        {
            Packet pkt = await WaitForPacket();
            pkt.Release();
        }
    }

    public virtual void StartImpl()
    {
        task = StartImplAsync();
        IsStarted = true;
    }

    public bool IsDone() => IsStarted && (task.IsCompleted || task.IsCanceled);
    
    public bool IsCancelled => IsStarted && task.IsCanceled;

    public void WaitDone() => task.Wait();
    
    public async Task WaitDoneAsync() => await task;

    protected abstract Task Run();

    protected ServiceManager GetManager() => parent.GetManager();
    
    private void InitPacketImpl(Packets.Packet pkt)
    {
        pkt.serviceId = serviceId;
        pkt.taskId    = taskId;
        pkt.taskType  = type;
        pkt.Reset();
    }

    protected CancellationToken GetCancelToken() => cancelSrc.Token;

    protected Packets.Packet AllocSendPacket()
    {
        Packets.Packet pkt = GetManager().AllocSendPacket();
        InitPacketImpl(pkt);
        return pkt;
    }

    protected async Task<Packets.Packet> AllocSendPacketAsync()
    {
        Packets.Packet pkt = await GetManager().AllocSendPacketAsync();
        InitPacketImpl(pkt);
        return pkt;
    }

    protected Packets.Packet AllocRecvPacket()
    {
        Packets.Packet pkt = GetManager().AllocRecvPacket();
        InitPacketImpl(pkt);
        return pkt;
    }
    
    protected async Task<Packets.Packet> AllocRecvPacketAsync()
    {
        Packets.Packet pkt = await GetManager().AllocRecvPacketAsync();
        InitPacketImpl(pkt);
        return pkt;
    }

    protected void SendPacket(Packets.Packet pkt) => parent.GetManager().SendPacket(pkt);
    
    protected Task SendPacketAsync(Packets.Packet pkt) => parent.GetManager().SendPacketAsync(pkt);

    public void SignalIncomingPacket(Packets.Packet pkt) => packets.Add(pkt);

    protected bool HasPendingRecvPacket() => packets.Count != 0;
    protected async Task<Packets.Packet> WaitForPacket() => await packets.TakeAsync();
}
