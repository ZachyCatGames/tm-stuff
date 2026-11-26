using Test.Packets;

namespace Test.Services;

public abstract class ServiceTask
{
    public uint               serviceId;
    public readonly TaskType  type;
    public readonly TaskState state;
    public readonly uint      taskId;
    public readonly uint      priority;

    private CancellationTokenSource cancelSrc = new();

    private readonly ServiceManager manager;
    
    
    private PacketQueue packets = new(20);
    
    public bool hasFollowup;

    public Task task;
    
    protected readonly IService parent;
    
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

    public virtual void StartImpl()
    {
        this.task = StartImplAsync();
        this.IsStarted = true;
    }

    public bool IsDone() => this.IsStarted && (this.task.IsCompleted || this.task.IsCanceled);
    
    public bool IsCancelled => this.IsStarted && this.task.IsCanceled;

    public void WaitDone() => this.task.Wait();
    
    public async Task WaitDoneAsync() => await this.task;

    protected abstract Task Run();

    protected ServiceManager GetManager() => this.parent.GetManager();
    
    private void InitPacketImpl(Packet pkt)
    {
        pkt.serviceId = this.serviceId;
        pkt.taskId    = this.taskId;
        pkt.taskType  = this.type;
        pkt.Reset();
    }

    protected CancellationToken GetCancelToken() => this.cancelSrc.Token;
    
    protected async Task<Packet> AllocSendPacketAsync()
    {
        Packets.Packet pkt = await this.GetManager().AllocSendPacketAsync();
        this.InitPacketImpl(pkt);
        return pkt;
    }

    protected async Task<Packet> AllocRecvPacketAsync() => await this.GetManager().AllocRecvPacketAsync();

    protected Task SendPacketAsync(Packet pkt) => this.parent.GetManager().SendPacketAsync(pkt);

    public void SignalIncomingPacket(Packet pkt) => this.packets.Add(pkt);

    protected bool HasPendingRecvPacket() => this.packets.Count != 0;
    protected async Task<Packet> WaitForPacket() => await this.packets.TakeAsync();
    
    private async Task StartImplAsync()
    {
        /* Start the task. */
        await this.Run();
        
        /* Clear any pending packets. */
        while (this.HasPendingRecvPacket())
        {
            Packet pkt = await this.WaitForPacket();
            pkt.Release();
        }
    }
}
