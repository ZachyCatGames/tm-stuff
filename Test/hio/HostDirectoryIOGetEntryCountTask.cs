

namespace Test.hio;

public class HostDirectoryIOGetEntryCountTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostDirectoryIOGetEntryCountTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, TaskType.GetDirectoryEntryCount, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packet pkt = await WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        pkt.Release();

        Console.WriteLine("[hio] GetDirectoryEntryCount, fd={0}", fd);

        /* Try to get the dir's entry count. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        Int64 count = -1;
        try
        {
            count = mgr.GetDirectoryEntryCount((int)fd);
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
        reply.Write(count);
        reply.WriteHeader();
        reply.Print();

        /* Send the reply. */
        await SendPacketAsync(reply);
    }
}