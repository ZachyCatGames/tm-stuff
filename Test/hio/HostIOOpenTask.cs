

namespace Test.hio;

public class HostIOOpenTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostIOOpenTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, TaskType.OpenFile, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packet pkt = await WaitForPacket();
        
        /* Parse the packet. */
        pkt.Read(out Int64 inFd);
        string path = pkt.ReadString(0x301);
        pkt.Read(out UInt32 mode);
        pkt.Release();
        
        Console.WriteLine("[hio] Opening file {0}", path);
        
        /* Try to open the file. */
        Int64 fd = -1;
        HioErrorCode err = HioErrorCode.SuccessEnd;
        try
        {
            fd = mgr.OpenFile(path, mode);
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
        reply.Write((UInt64)0);
        reply.WriteHeader();
        reply.Print();
        
        /* Send the reply. */
        await SendPacketAsync(reply);
    }
}