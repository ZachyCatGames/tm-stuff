

namespace Test.Services.hio;

class HostIOCloseTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostIOCloseTask(IService parent, HostFilesystemManager mgr, uint taskId) : base(parent, TaskType.CloseFile, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packets.Packet pkt = await this.WaitForPacket();
        
        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        pkt.Release();
        
        /* Try to close the file. */
        var result = HioErrorCode.SuccessEnd;
        try
        {
            this.mgr.CloseFile((int)fd);
        }
        catch (HioException excpt)
        {
            result = excpt.error;
        }
        
        /* Setup a reply. */
        Packets.Packet reply = await this.AllocSendPacketAsync();
        reply.isInitiate = false;
        reply.Write(fd);
        reply.Write((UInt32)result);
        reply.Write((UInt64)0); // unused
        
        /* Send it. */
        await this.SendPacketAsync(reply);
    }
}