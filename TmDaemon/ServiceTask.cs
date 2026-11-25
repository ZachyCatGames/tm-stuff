namespace Test;

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
    }

    public virtual void StartImpl()
    {
        task = StartImplAsync();
        IsStarted = true;
    }

    public bool IsDone() => IsStarted && (task.IsCompleted || task.IsCanceled);

    public void WaitDone() => task.Wait();
    
    public async Task WaitDoneAsync() => await task;

    protected abstract Task Run();

    protected ServiceManager GetManager() => parent.GetManager();
    
    private void InitPacketImpl(Packet pkt)
    {
        pkt.serviceId = serviceId;
        pkt.taskId    = taskId;
        pkt.taskType  = type;
        pkt.Reset();
    }

    protected CancellationToken GetCancelToken() => cancelSrc.Token;

    protected Packet AllocSendPacket()
    {
        Packet pkt = GetManager().AllocSendPacket();
        InitPacketImpl(pkt);
        return pkt;
    }

    protected async Task<Packet> AllocSendPacketAsync()
    {
        Packet pkt = await GetManager().AllocSendPacketAsync();
        InitPacketImpl(pkt);
        return pkt;
    }

    protected Packet AllocRecvPacket()
    {
        Packet pkt = GetManager().AllocRecvPacket();
        InitPacketImpl(pkt);
        return pkt;
    }
    
    protected async Task<Packet> AllocRecvPacketAsync()
    {
        Packet pkt = await GetManager().AllocRecvPacketAsync();
        InitPacketImpl(pkt);
        return pkt;
    }

    protected void SendPacket(Packet pkt) => parent.GetManager().SendPacket(pkt);
    
    protected Task SendPacketAsync(Packet pkt) => parent.GetManager().SendPacketAsync(pkt);

    public void SignalIncomingPacket(Packet pkt) => packets.Add(pkt);

    protected async Task<Packet> WaitForPacket() => await packets.TakeAsync();
}
