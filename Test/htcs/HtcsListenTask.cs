namespace Test.htcs;

public class HtcsListenTask : ServiceTask
{
    HtcsSocketManager htcsManager;
    public HtcsListenTask(Service parent, HtcsSocketManager manager, uint taskId) : base(parent, parent.GetServiceId(), TaskType.HtcsListen, taskId, 0)
    {
        this.htcsManager = manager;
    }

    protected override async Task Run()
    {
        /* Receive info packet. */
        Packet pkt = await this.WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int32 fd);
        pkt.Read(out Int32 backlogCount);
        pkt.Release();

        Console.WriteLine("[htcs] Listen on fd={0}", fd);

        /* Bind the socket. */
        int retval = htcsManager.Listen(fd, backlogCount);

        /* Allocate a reply packet. */
        var reply = this.AllocSendPacket();

        /* Setup our reply. */
        Int32 result = retval < 0 ? 0 : 1; // TODO
        reply.serviceId = this.parent.GetServiceId();
        reply.taskId = this.taskId;
        reply.taskType = this.type;
        reply.isInitiate = false;
        reply.Reset();
        reply.Write(fd); // I think these are the same?
        reply.Write(retval);
        reply.Write(result);
        reply.WriteHeader();

        /* Send it off. */
        this.SendPacket(reply);
    }

}
