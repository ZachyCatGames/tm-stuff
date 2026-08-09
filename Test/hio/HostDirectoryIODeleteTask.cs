

namespace Test.hio;

public class HostDirectoryIODeleteTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostDirectoryIODeleteTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, TaskType.DeleteDirectory, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packet pkt = await WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        string path = pkt.ReadString(0x301);
        pkt.Read(out bool recursive);
        pkt.Release();

        Console.WriteLine("[hio] DeleteDirectory, fd={0}", fd);

        /* Try to delete the dir. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        try
        {
            mgr.DeleteDirectory(path, recursive);
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