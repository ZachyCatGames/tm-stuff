

namespace Test.hio;

class HostIOCloseTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostIOCloseTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, parent.serviceId, TaskType.CloseFile, taskId, 0)
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
        
        /* Try to close the file. */
        var result = HioErrorCode.SuccessEnd;
        try
        {
            mgr.CloseFile((int)fd);
        }
        catch (HioException excpt)
        {
            result = excpt.error;
        }
        
        /* Setup a reply. */
        Packet reply = this.AllocSendPacket();
        reply.isInitiate = false;
        reply.Write(fd);
        reply.Write((UInt32)result);
        reply.Write((UInt64)0); // unused
        reply.WriteHeader();
        reply.Print();
        
        /* Send it. */
        this.SendPacket(reply);
    }
}