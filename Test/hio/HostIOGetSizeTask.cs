

namespace Test.hio;

public class HostIOGetSizeTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostIOGetSizeTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, parent.serviceId, TaskType.GetFileSize, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packet pkt = await this.WaitForPacket();
        
        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        pkt.Release();
        
        Console.WriteLine("[hio] Get file size, fd={0}", fd);
        
        /* Try to get the file's size. */
        Int64 size = 0;
        HioErrorCode err = HioErrorCode.SuccessEnd;
        try
        {
            size = mgr.GetFileSize((int)fd);
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
        reply.Write(size);
        reply.WriteHeader();
        reply.Print();
        
        /* Send the reply. */
        this.SendPacket(reply);
    }
}