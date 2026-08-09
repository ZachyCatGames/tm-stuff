

namespace Test.hio;

public class HostIOExistsTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostIOExistsTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, TaskType.FileExists, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packet pkt = await WaitForPacket();
        
        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        string path = pkt.ReadString(0x301);
        pkt.Release();
        
        Console.WriteLine("[hio] FileExists, fd={0}", fd);
        
        /* Check if the file exists. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        bool exists = false;
        try
        {
            exists = mgr.FileExists(path);
        }
        catch (HioException excpt)
        {
            err = excpt.error;
        }
        
        /* Setup a reply. */
        Packet reply = await AllocSendPacketAsync();
        reply.isInitiate = false;
        reply.Write(fd);
        reply.Write((UInt32)err);
        reply.Write(exists ? 1 : 0);
        reply.WriteHeader();
        reply.Print();
        
        /* Send the reply. */
        await SendPacketAsync(reply);
    }
}