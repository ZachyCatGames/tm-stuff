

namespace Test.Services.hio;

public class HostIOSetPriorityTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostIOSetPriorityTask(IService parent, HostFilesystemManager mgr, uint taskId) : base(parent, TaskType.SetPriorityForFile, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packets.Packet pkt = await this.WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        pkt.Read(out Int32 prio);
        pkt.Release();

        Console.WriteLine("[hio] SetPriorityForFile, fd={0}", fd);

        /* Try to set the file's priority. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        try
        {
            this.mgr.SetPriorityForFile((int)fd, prio);
        }
        catch (HioException excpt)
        {
            err = excpt.error;
        }

        /* Setup a reply. */
        Packets.Packet reply = await this.AllocSendPacketAsync();
        reply.isInitiate = false;
        reply.Write(fd);
        reply.Write((UInt32)err);
        reply.Write((UInt64)0);

        /* Send the reply. */
        await this.SendPacketAsync(reply);
    }
}