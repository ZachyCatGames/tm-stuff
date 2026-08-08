namespace Test.htcs;

public class HtcsAcceptTask : ServiceTask
{
    HtcsSocketManager htcsManager;
    public HtcsAcceptTask(Service parent, HtcsSocketManager manager, uint taskId) : base(parent, parent.GetServiceId(), TaskType.Accept, taskId, 0)
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

        Console.WriteLine("[htcs] Accept on fd={0}", fd);

        /* Accept the socket. */
        Int32 result = 0;
        Int32 retval = 0;
        SockAddrHtcs? addr = null;
        try
        {
            var ret = await htcsManager.AcceptAsync(fd);
            addr = ret.Item2;
            retval = ret.Item1;
        }
        catch (HtcsException excpt)
        {
            result = ResultConversion.HtcsToTmipc(excpt.error);
            retval = -1;
        }

        Console.WriteLine("[htcs] Accept res={0}", retval);

        /* Allocate a reply packet. */
        var reply = this.AllocSendPacket();

        /* Address -> bytes. */
        byte[] addrBytes;
        if (retval < 0 && addr is SockAddrHtcs a)
        {
            addrBytes = a.ToBytes();
        }
        else
        {
            addrBytes = new byte[SockAddrHtcs.PackedSize];
        }
        Console.WriteLine("Fucker");

        /* Setup our reply. */
        reply.serviceId = this.parent.GetServiceId();
        reply.taskId = this.taskId;
        reply.taskType = this.type;
        reply.isInitiate = false;
        reply.Reset();
        reply.Write(fd); // I think these are the same?
        reply.Write(retval);
        reply.Write(result);
        reply.Write(addrBytes);
        reply.WriteHeader();

        /* Send it off. */
        this.SendPacket(reply);
    }
}
