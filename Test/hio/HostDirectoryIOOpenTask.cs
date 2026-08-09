

namespace Test.hio;

public class HostDirectoryIOOpenTask : ServiceTask
{
    readonly HostFilesystemManager mgr;
    
    public HostDirectoryIOOpenTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, parent.GetServiceId(), TaskType.OpenDirectory, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    protected override async Task Run()
    {
        /* Receive the packet. */
        Packet pkt = await this.WaitForPacket();
        
        /* Parse the packet. */
        pkt.Read(out Int64 fdIn);
        string path = pkt.ReadString(0x301);
        pkt.Read(out UInt64 id);
        pkt.Read(out UInt32 flags);
        
        /* Release the packet. */
        pkt.Release();
        
        Console.WriteLine("[hio] opening {0}", path);
        
        /* Try to open the dir. */
        HioErrorCode result = HioErrorCode.SuccessEnd;
        int fd = -1;
        try
        {
            fd = mgr.OpenDirectory(path, flags);
        }
        catch (HioException excpt)
        {
            result = excpt.error;
        }
        
        /* Allocate a packet. */
        Packet reply = this.AllocSendPacket();
        
        /* Setup our reply. */
        reply.isInitiate = false;
        reply.Write((Int64)fd);
        reply.Write((Int32)result);
        reply.Write((Int64)0); // unused?
        reply.Write(id);
        reply.WriteHeader();

        reply.Print();

        /* Send reply. */
        this.SendPacket(reply);
    }
}
