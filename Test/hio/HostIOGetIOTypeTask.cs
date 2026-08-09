

namespace Test.hio;

public class HostIOGetIOTypeTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostIOGetIOTypeTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, parent.serviceId, TaskType.GetIOType, taskId, 0)
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

        Console.WriteLine("[hio] GetIOType, fd={0}", fd);

        /* Try to get the file's IO type. */
        /* 
         * NOTE: This just returns 0 atm.
         * I need to look at fs to see what this does / means.
         * FS tries to use it when an emtpy str is given to
         * OpenHostFileSystem, which is done by fs::MountHostRoot
         * in sdknso.
         */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        UInt32 ioType = 0;
        try
        {
            ioType = mgr.GetIOType(path);
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
        reply.Write((UInt64)ioType);
        reply.WriteHeader();
        reply.Print();

        /* Send the reply. */
        this.SendPacket(reply);
    }
}