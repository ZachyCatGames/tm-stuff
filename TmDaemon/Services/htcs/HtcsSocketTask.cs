namespace Test.Services.htcs;

public class HtcsSocketTask : ServiceTask
{
    readonly HtcsSocketManager htcsManager;
    public HtcsSocketTask(IService parent, HtcsSocketManager manager, uint taskId) : base(parent, TaskType.Socket, taskId, 0)
    {
        this.htcsManager = manager;
    }

    protected override async Task Run()
    {
        /* Receive info packet. */
        Packets.Packet pkt = await this.WaitForPacket();

        /* This should just contain -1..? */
        pkt.Read(out Int32 fd);
        pkt.Release();

#if HTCS_TASK_DEBUG
        Console.WriteLine("[htcs] CreateSocket");
#endif

        /* Create a new socket. */
        Int32 result = 0;
        Int32 newFd = -1;
        try
        {
            newFd = this.htcsManager.CreateSocket();
        }
        catch (HtcsException excpt)
        {
            result = ResultConversion.HtcsToTmipc(excpt.error);
        }
#if HTCS_TASK_DEBUG
        Console.WriteLine("[htcs] CreateSocket fd={0}", newFd);
#endif

        /* Allocate a reply packet. */
        var reply = await this.AllocSendPacketAsync();

        /* Setup our reply. */
        reply.isInitiate = false;
        reply.Write(newFd); // I think these are the same?
        reply.Write(newFd);
        reply.Write(result);

        //var f = File.Open(String.Format("{0}.bin", newFd), FileMode.Create);
        //f.Write(reply.GetBuffer(), 0, 0xe020);

        /* Send it off. */
        await this.SendPacketAsync(reply);
    }
}
