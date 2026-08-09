

namespace Test.hio;

public class HostIOCreateTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostIOCreateTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, TaskType.CreateFile, taskId, 0)
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
        pkt.Read(out Int64 size);
        pkt.Release();

        Console.WriteLine("[hio] CreateFile, fd={0}", fd);

        /* Try to create the file. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        try
        {
            mgr.CreateFile(path, size);
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
        reply.Write((UInt64)0); // unused
        reply.WriteHeader();
        reply.Print();

        /* Send the reply. */
        await SendPacketAsync(reply);
    }
}