

namespace Test.hio;

public class HostDirectoryIOExistsTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostDirectoryIOExistsTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, parent.serviceId, TaskType.DirectoryExists, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packet pkt = await this.WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        string path = pkt.ReadString(0x301);
        pkt.Release();

        Console.WriteLine("[hio] DirectoryExists, fd={0}", fd);

        /* Check if the dir exists. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        bool exists = false;
        try
        {
            exists = mgr.DirectoryExists(path);
        }
        catch (HioException excpt)
        {
            err = excpt.error;
        }

        /* Setup a reply. */
        Packet reply = this.AllocSendPacket();
        reply.isInitiate = false;
        reply.Write(fd);
        reply.Write((UInt32)err);
        reply.Write((UInt64)(exists ? 1 : 0)); // unused
        reply.WriteHeader();
        reply.Print();

        /* Send the reply. */
        this.SendPacket(reply);
    }
}