namespace Test.htcs;

public class HtcsCloseTask : ServiceTask
{
    HtcsSocketManager htcsManager;
    public HtcsCloseTask(Service parent, HtcsSocketManager manager, uint taskId) : base(parent, parent.GetServiceId(), TaskType.HtcsClose, taskId, 0)
    {
        this.htcsManager = manager;
    }

    protected override async Task Run()
    {
        /* Receive info packet. */
        Packet pkt = await this.WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int32 fd);
        pkt.Release();

        Console.WriteLine("[htcs] Close on fd={0}", fd);

        /* Bind the socket. */
        int retval = htcsManager.CloseSocket(fd);

        Console.WriteLine("[htcs] Close res = {0}", retval);

        /* Allocate a reply packet. */
        var reply = this.AllocSendPacket();

        /* Setup our reply. */
        Int32 result = retval < 0 ? 0 : 1; // TODO
        reply.serviceId = this.parent.GetServiceId();
        reply.taskId = this.taskId;
        reply.taskType = this.type;
        reply.isInitiate = false;
        reply.Reset();
        reply.Write(fd);
        reply.Write(retval);
        reply.Write(result);
        reply.WriteHeader();

        /* Send it off. */
        this.SendPacket(reply);
    }
}
