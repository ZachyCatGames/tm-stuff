namespace Test.Services.dbg;

public class HostDebugService : ServiceBase
{
    public const uint ServiceId = 0xDDDE6636;
    
    private readonly HostTaskIdAllocator taskIdAlloc;

    private class DbgTask : ServiceTask
    {
        private readonly Packets.Packet packet;
        
        public Packets.Packet? Reply { get; private set; }
        
        public DbgTask(HostDebugService parent, Packets.Packet pkt, uint taskId) : base(parent, TaskType.Debug, taskId, 0)
        {
            Console.WriteLine(taskId);
            this.packet = pkt;
        }

        protected override async Task Run()
        {
            /* Send the packet. */
            await this.SendPacketAsync(packet);
            
            /* Wait for a reply. */
            Reply = await this.WaitForPacket();
        }
    }

    private struct ReplyBase
    {
        public ReplyType ReplyType { get; init; }
        public UInt64    ProcessId { get; init; }
        public UInt32    Result    { get; init; }

        public ReplyBase(Packets.Packet pkt)
        {
            pkt.Read(out UInt32 replyType);
            this.ReplyType = (ReplyType)replyType;

            pkt.Read(out UInt32 pid);
            this.ProcessId = pid;

            pkt.Read(out UInt32 res);
            this.Result = res;
        }
    }

    private struct ReplyS32
    {
        public ReplyBase Base;
        public Int32     S32 { get; init; }
        
        public ReplyS32(Packets.Packet pkt)
        {
            this.Base = new(pkt);
            pkt.Read(out Int32 s32);
            this.S32 = s32;

        }
    }

    public HostDebugService(ServiceManager mgr, HostTaskIdAllocator tidAlloc) : base(ServiceId, mgr)
    {
        this.taskIdAlloc = tidAlloc;
    }

    public override ServiceTask? ProcessNewRequest(Packets.Packet pkt)
    {
        return null;
    }

    public async Task<int> GetProcessCount()
    {
        /* Setup our base packet. */
        var pkt = await PreparePacket(DebugTaskType.GetProcessCount, 0);
        
        /* Create and submit a task. */
        DbgTask task = new(this, pkt, pkt.taskId);
        this.serviceManager.RegisterNewTask(task);
        
        /* Wait for it to finish. */
        await task.WaitDoneAsync();
        
        /* Parse the reply. */
        var reply = task.Reply;
        return new ReplyS32(reply).S32;
    }

    private async Task<Packets.Packet> PreparePacket(DebugTaskType type, UInt64 pid)
    {
        var pkt = await this.serviceManager.AllocSendPacketAsync();
        pkt.Reset();
        pkt.serviceId = serviceId;
        pkt.isInitiate = true;
        pkt.taskId = taskIdAlloc.AllocateTaskId();
        pkt.taskType = TaskType.Debug;
        pkt.Write((uint)type);
        pkt.Write(pid);
        return pkt;
    }
}