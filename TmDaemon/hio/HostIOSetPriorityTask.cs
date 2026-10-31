

namespace Test.hio;

public class HostIOSetPriorityTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostIOSetPriorityTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, TaskType.SetPriorityForFile, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packet pkt = await WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        pkt.Read(out Int32 prio);
        pkt.Release();

        Console.WriteLine("[hio] SetPriorityForFile, fd={0}", fd);

        /* Try to set the file's priority. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        try
        {
            mgr.SetPriorityForFile((int)fd, prio);
        }
        catch (HioException excpt)
        {
            err = excpt.error;
        }

        /* Setup a reply. */
        Packet reply = await AllocSendPacketAsync();
        reply.isInitiate = false;
        reply.Write(fd);
        reply.Write((UInt32)err);
        reply.Write((UInt64)0);
        reply.WriteHeader();
        reply.Print();

        /* Send the reply. */
        await SendPacketAsync(reply);
    }
}