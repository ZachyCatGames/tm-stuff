

namespace Test.Services.hio;

public class HostDirectoryIOCloseTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostDirectoryIOCloseTask(IService parent, HostFilesystemManager hostfsMgr, uint taskId) : base(parent, TaskType.CloseDirectory, taskId, 0)
    {
        mgr = hostfsMgr;
    }
    
    protected override async Task Run()
    {
        /* Get packet. */
        Packets.Packet pkt = await this.WaitForPacket();
        
        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        
        /* Release the packet. */
        pkt.Release();
        
        /* Close the directory. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        try {
            this.mgr.CloseDirectory((int)fd);
        }
        catch (HioException excpt)
        {
            err = excpt.error;
        }
        
        /* Allocate a reply packet. */
        Packets.Packet reply = await this.AllocSendPacketAsync();
        
        /* Setup the reply. */
        reply.isInitiate = false;
        reply.Write(fd);
        reply.Write((Int32)err);
        reply.Write((Int64)0);
        
        /* Send it. */
        await this.SendPacketAsync(reply);
    }
}