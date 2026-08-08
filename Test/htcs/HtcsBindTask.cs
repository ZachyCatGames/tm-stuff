namespace Test.htcs;

public class HtcsBindTask : ServiceTask
{
    HtcsSocketManager htcsManager;
    public HtcsBindTask(Service parent, HtcsSocketManager manager, uint taskId) : base(parent, parent.GetServiceId(), TaskType.Bind, taskId, 0)
    {
        this.htcsManager = manager;
    }

    protected override async Task Run()
    {
        /* Receive info packet. */
        Packet pkt = await this.WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int32 fd);
        var bs = pkt.Read(SockAddrHtcs.PackedSize);
        Console.WriteLine(BitConverter.ToString(bs.Array));
        var addr = SockAddrHtcs.FromBytes(bs);
        pkt.Release();

        Console.WriteLine("[htcs] Bind on fd={0}\n" +
            "\tPort Name: {1}\n" +
            "\tPeer Name: {2}", fd, addr.portName, addr.peerName);

        /* Bind the socket. */
        Int32 result = 0;
        Int32 retval = 0;
        try
        {
            htcsManager.Bind(fd, addr);
        }
        catch (HtcsException excpt)
        {
            result = ResultConversion.HtcsToTmipc(excpt.error);
            retval = -1;
        }

        Console.WriteLine("[htcs] Bind res = {0}", retval);

        /* Allocate a reply packet. */
        var reply = this.AllocSendPacket();

        /* Setup our reply. */
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
