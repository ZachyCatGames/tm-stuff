

namespace Test.hio;

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

        /* Try to get the file's IO type. */
        /* 
         * NOTE: This just returns 0 atm.
         * I need to look at fs to see what this does / means.
         * FS tries to use it when an emtpy str is given to
         * OpenHostFileSystem, which is done by fs::MountHostRoot
         * in sdknso.
         *
         * NOTE 2: fs doesn't seem to care what this returns for 
         * OpenHostFileSystem? Both for actual value and error code.
         */
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