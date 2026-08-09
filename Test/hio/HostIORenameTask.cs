

namespace Test.hio;

public class HostIORenameTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostIORenameTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, parent.serviceId, TaskType.RenameFile, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packet pkt = await this.WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        string src = pkt.ReadString(0x301);
        string dst = pkt.ReadString(0x301);
        pkt.Release();

        Console.WriteLine("[hio] RenameFile, fd={0}", fd);

        /* Try to rename the file. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        try
        {
            mgr.RenameFile(src, dst);
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