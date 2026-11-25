

namespace Test.Services.hio;

public class HostIOGetPriorityTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostIOGetPriorityTask(IService parent, HostFilesystemManager mgr, uint taskId) : base(parent, TaskType.GetPriorityForFile, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packets.Packet pkt = await WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        pkt.Release();

        Console.WriteLine("[hio] GetPriorityForFile, fd={0}", fd);

        /* Try to set the file's priority. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        Int32 prio = -1;
        try
        {
            prio = mgr.GetPriorityForFile((int)fd);
        }
        catch (HioException excpt)
        {
            err = excpt.error;
        }

        /* Setup a reply. */
        Packets.Packet reply = await AllocSendPacketAsync();
        reply.isInitiate = false;
        reply.Write(fd);
        reply.Write((UInt32)err);
        reply.Write((Int64)prio); // target casts to s32 again
        reply.WriteHeader();
        reply.Print();

        /* Send the reply. */
        await SendPacketAsync(reply);
    }
}