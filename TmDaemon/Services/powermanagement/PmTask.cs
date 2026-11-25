using Test.Packets;

namespace Test.Services.powermanagement;

public class PmTask : ServiceTask
{
    private readonly SemaphoreSlim prepDoneEv; 
    
    public PmTask(HostPmService parent, TaskType type, uint taskId, SemaphoreSlim prepDoneEv) : base(parent, type, taskId, 0)
    {
        this.prepDoneEv = prepDoneEv;
    }
    
    protected override async Task Run()
    {
        Packet pkt = await this.WaitForPacket();
        pkt.Release();
        
        /* Wait for our parent to signal us. */
        await this.prepDoneEv.WaitAsync();
        
        /* Send a reply. */
        pkt.Write(-1);
        await this.SendPacketAsync(pkt);
    }
}