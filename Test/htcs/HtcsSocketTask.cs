namespace Test.htcs;

public class HtcsSocketTask : ServiceTask
{
    readonly HtcsSocketManager htcsManager;
    public HtcsSocketTask(Service parent, HtcsSocketManager manager, uint taskId) : base(parent, parent.GetServiceId(), TaskType.Socket, taskId, 0)
    {
        this.htcsManager = manager;
    }

    protected override async Task Run()
    {
        /* Receive info packet. */
        Packet pkt = await this.WaitForPacket();

        /* This should just contain -1..? */
        pkt.Read(out Int32 fd);
        pkt.Release();

        Console.WriteLine("[htcs] CreateSocket");

        /* Create a new socket. */
        Int32 result = 0;
        Int32 newFd = -1;
        try
        {
            newFd = htcsManager.CreateSocket();
        }
        catch (HtcsException excpt)
        {
            result = ResultConversion.HtcsToTmipc(excpt.error);
        }
        Console.WriteLine("[htcs] CreateSocket fd={0}", newFd);

        /* Allocate a reply packet. */
        var reply = this.AllocSendPacket();

        /* Setup our reply. */
        reply.serviceId = this.parent.GetServiceId();
        reply.taskId = this.taskId;
        reply.taskType = this.type;
        reply.isInitiate = false;
        reply.Reset();
        reply.Write(newFd); // I think these are the same?
        reply.Write(newFd);
        reply.Write(result);
        reply.WriteHeader();

        //var f = File.Open(String.Format("{0}.bin", newFd), FileMode.Create);
        //f.Write(reply.GetBuffer(), 0, 0xe020);

        /* Send it off. */
        this.SendPacket(reply);
    }
}
