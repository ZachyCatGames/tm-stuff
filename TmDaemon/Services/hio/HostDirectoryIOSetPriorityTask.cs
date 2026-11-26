

namespace Test.Services.hio;

public class HostDirectoryIOSetPriorityTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostDirectoryIOSetPriorityTask(IService parent, HostFilesystemManager mgr, uint taskId) : base(parent, TaskType.SetPriorityForDirectory, taskId, 0)
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

        Console.WriteLine("[hio] SetPriorityForDirectory, fd={0}", fd);

        /* Try to set the dir's priority. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        try
        {
            this.mgr.SetPriorityForDirectory((int)fd, prio);
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
        reply.Write((UInt64)0); // unused

        /* Send the reply. */
        await this.SendPacketAsync(reply);
    }
}