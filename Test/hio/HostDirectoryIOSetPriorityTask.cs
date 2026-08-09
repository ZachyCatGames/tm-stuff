

namespace Test.hio;

public class HostDirectoryIOSetPriorityTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostDirectoryIOSetPriorityTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, parent.serviceId, TaskType.SetPriorityForDirectory, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packet pkt = await this.WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        pkt.Read(out Int32 prio);
        pkt.Release();

        Console.WriteLine("[hio] SetPriorityForDirectory, fd={0}", fd);

        /* Try to set the dir's priority. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        try
        {
            mgr.SetPriorityForDirectory((int)fd, prio);
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
        reply.Write((UInt64)0); // unused
        reply.WriteHeader();
        reply.Print();

        /* Send the reply. */
        this.SendPacket(reply);
    }
}