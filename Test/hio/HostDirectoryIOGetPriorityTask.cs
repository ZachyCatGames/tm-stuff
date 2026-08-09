

namespace Test.hio;

public class HostDirectoryIOGetPriorityTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostDirectoryIOGetPriorityTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, parent.serviceId, TaskType.GetPriorityForDirectory, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packet pkt = await this.WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        pkt.Release();

        Console.WriteLine("[hio] GetPriorityForDirectory, fd={0}", fd);

        /* Try to set the dir's priority. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        Int32 prio = -1;
        try
        {
            prio = mgr.GetPriorityForDirectory((int)fd);
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
        reply.Write((Int64)prio); // target casts to s32 again
        reply.WriteHeader();
        reply.Print();

        /* Send the reply. */
        this.SendPacket(reply);
    }
}