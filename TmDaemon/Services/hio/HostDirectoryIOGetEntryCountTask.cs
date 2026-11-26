

namespace Test.Services.hio;

public class HostDirectoryIOGetEntryCountTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostDirectoryIOGetEntryCountTask(IService parent, HostFilesystemManager mgr, uint taskId) : base(parent, TaskType.GetDirectoryEntryCount, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packets.Packet pkt = await this.WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        pkt.Release();

        Console.WriteLine("[hio] GetDirectoryEntryCount, fd={0}", fd);

        /* Try to get the dir's entry count. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        Int64 count = -1;
        try
        {
            count = this.mgr.GetDirectoryEntryCount((int)fd);
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
        reply.Write(count);

        /* Send the reply. */
        await this.SendPacketAsync(reply);
    }
}