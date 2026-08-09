

namespace Test.hio;

public class HostDirectoryIORenameTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostDirectoryIORenameTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, TaskType.RenameDirectory, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packet pkt = await WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        string src = pkt.ReadString(0x301);
        string dst = pkt.ReadString(0x301);
        pkt.Release();

        Console.WriteLine("[hio] RenameDirectory, fd={0}", fd);

        /* Try to rename the dir. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        try
        {
            mgr.RenameDirectory(src, dst);
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
        reply.Write((UInt64)0); // unused
        reply.WriteHeader();
        reply.Print();

        /* Send the reply. */
        await SendPacketAsync(reply);
    }
}