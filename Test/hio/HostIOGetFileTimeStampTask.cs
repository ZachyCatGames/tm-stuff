

namespace Test.hio;

public class HostIOGetFileTimeStampTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostIOGetFileTimeStampTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, parent.serviceId, TaskType.GetFileTimeStamp, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packet pkt = await this.WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        string path = pkt.ReadString(0x301);
        pkt.Release();

        Console.WriteLine("[hio] GetFileTimeStamp, fd={0}", fd);

        /* Try to delete the file. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        FileTimeStamp ft = new(0,0,0);
        try
        {
            ft = mgr.GetFileTimeStamp(path);
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
        reply.Write(ft.v1);
        reply.Write(ft.v2);
        reply.Write(ft.v3);
        reply.WriteHeader();
        reply.Print();

        /* Send the reply. */
        this.SendPacket(reply);
    }
}