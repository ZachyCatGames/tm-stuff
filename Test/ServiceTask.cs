namespace Test;

public abstract class ServiceTask
{
    public readonly uint serviceId;
    public readonly TaskType type;
    public readonly TaskState state;
    public readonly uint taskId;
    public readonly uint priority;

    public bool hasFollowup;

    protected readonly Service parent;

    PacketQueue packets;

    public Task task;

    public ServiceTask(Service par, TaskType type, uint taskId, uint prio)
    {
        this.parent = par;
        this.serviceId = par.serviceId;
        this.type = type;
        this.state = TaskState.InProgress;
        this.taskId = taskId;
        this.priority = prio;
        this.packets = new(20);
    }

    private async Task StartImplAsync()
    {
        /* Start the task. */
        await this.Run();

        /* We're done. */
        this.NotifyDone();
    }

    public virtual void StartImpl()
    {
        task = this.StartImplAsync();
    }

    protected abstract Task Run();

    protected ServiceManager GetManager()
    {
        return parent.serviceManager;
    }
    
    private void InitPacketImpl(Packet pkt)
    {
        pkt.serviceId = this.parent.serviceId;
        pkt.taskId = this.taskId;
        pkt.taskType = this.type;
        pkt.Reset();
    }

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

    protected void NotifyDone()
    {
        parent.NotifyDone(this);
    }

    protected void SendPacket(Packet pkt)
    {
        parent.serviceManager.SendPacket(pkt);
    }
    
    protected Task SendPacketAsync(Packet pkt)
    {
        return parent.serviceManager.SendPacketAsync(pkt);
    }

    public async Task SignalIncomingPacket(Packet pkt)
    {
        await packets.AddAsync(pkt);
    }

    protected async Task<Packet> WaitForPacket()
    {
        return await packets.TakeAsync();
    }

}
