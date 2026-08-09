

namespace Test.hio;

public class HostDirectoryIOCreateTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostDirectoryIOCreateTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, parent.serviceId, TaskType.CreateDirectory, taskId, 0)
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

        Console.WriteLine("[hio] CreateDirectory, fd={0}", fd);

        /* Try to create the dir. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        try
        {
            mgr.CreateDirectory(path);
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
        reply.Write((UInt64)0); // unused
        reply.WriteHeader();
        reply.Print();

        /* Send the reply. */
        this.SendPacket(reply);
    }
}