

namespace Test.hio;

// IO type == directory entry type
public class HostIOGetIOTypeTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostIOGetIOTypeTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, TaskType.GetIOType, taskId, 0)
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

        Console.WriteLine("[hio] GetIOType, fd={0}", fd);

        /* Try to get the path's entry type. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        Int32 ioType = 0;
        try
        {
            ioType = mgr.GetIOType(path);
        }
        catch (HioException excpt)
        {
            err = excpt.error;
        }
        catch (Exception excpt)
        {
            Console.WriteLine(excpt);
        }
        
        if (ioType == -1)
        {
            ioType = 0;
            err = HioErrorCode.PathNotFound;
        }

        /* Setup a reply. */
        Packet reply = await AllocSendPacketAsync();
        reply.isInitiate = false;
        reply.Write(fd);
        reply.Write((UInt32)err);
        reply.Write((UInt64)ioType);
        reply.WriteHeader();
        reply.Print();

        /* Send the reply. */
        await SendPacketAsync(reply);
    }
}