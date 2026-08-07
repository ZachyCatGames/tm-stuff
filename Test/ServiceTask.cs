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

    readonly SemaphoreSlim incomingPacketSignal;
    Packet packet;

    Task task;

    public ServiceTask(Service par, uint sid, TaskType type, uint taskId, uint prio)
    {
        this.parent = par;
        this.serviceId = sid;
        this.type = type;
        this.state = TaskState.InProgress;
        this.taskId = taskId;
        this.priority = prio;
        this.incomingPacketSignal = new(0, 1);
    }

    private async Task StartImplAsync()
    {
        /* Start the task. */
        await this.Run();

        /* We're done. */
        this.NotifyDone();
    }

    public void StartImpl()
    {
        task = this.StartImplAsync();
    }

    protected abstract Task Run();

    protected ServiceManager GetManager()
    {
        return parent.serviceManager;
    }

    protected Packet AllocSendPacket()
    {
        return this.GetManager().AllocSendPacket();
    }

    protected Packet AllocRecvPacket()
    {
        return this.GetManager().AllocRecvPacket();
    }

    protected void NotifyDone()
    {
        parent.NotifyDone(this);
    }

    protected void SendPacket(Packet pkt)
    {
        parent.serviceManager.SendPacket(pkt);
    }

    public void SignalIncomingPacket(Packet pkt)
    {
        packet = pkt;
        incomingPacketSignal.Release();
    }

    protected async Task<Packet> WaitForPacket()
    {
        await incomingPacketSignal.WaitAsync();
        return packet;
    }

}
