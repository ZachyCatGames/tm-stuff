namespace Test.htcs;

public class HtcsCloseTask : ServiceTask
{
    HtcsSocketManager htcsManager;
    public HtcsCloseTask(Service parent, HtcsSocketManager manager, uint taskId) : base(parent, TaskType.Close, taskId, 0)
    {
        this.htcsManager = manager;
    }

    protected override async Task Run()
    {
        /* Receive info packet. */
        Packet pkt = await WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int32 fd);
        pkt.Release();

        Console.WriteLine("[htcs] Close on fd={0}", fd);

        /* Close the socket. */
        Int32 result = 0;
        Int32 retval = 0;
        try
        {
            htcsManager.CloseSocket(fd);
        }
        catch (HtcsException excpt)
        {
            result = ResultConversion.HtcsToTmipc(excpt.error);
            retval = -1;
        }

        Console.WriteLine("[htcs] Close res = {0}", retval);

        /* Allocate a reply packet. */
        var reply = await AllocSendPacketAsync();

        /* Setup our reply. */
        reply.isInitiate = false;
        reply.Write(fd);
        reply.Write(retval);
        reply.Write(result);
        reply.WriteHeader();

        /* Send it off. */
        await SendPacketAsync(reply);
    }
}
