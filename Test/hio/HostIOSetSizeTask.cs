

namespace Test.hio;

public class HostIOSetSizeTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostIOSetSizeTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, parent.serviceId, TaskType.SetFileSize, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packet pkt = await this.WaitForPacket();
        
        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        pkt.Read(out Int64 size);
        pkt.Release();
        
        Console.WriteLine("[hio] Set file size, fd={0}", fd);
        
        /* Try to set the file's size. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        try
        {
            mgr.SetFileSize((int)fd, size);
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