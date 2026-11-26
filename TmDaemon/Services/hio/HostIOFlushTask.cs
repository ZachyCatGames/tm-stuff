

namespace Test.Services.hio;

public class HostIOFlushTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostIOFlushTask(IService parent, HostFilesystemManager mgr, uint taskId) : base(parent, TaskType.FlushFile, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packets.Packet pkt = await this.WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        pkt.Release();

        Console.WriteLine("[hio] FlushFile, fd={0}", fd);

        /* Try to flush the file. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        try
        {
            await this.mgr.FlushFileAsync((int)fd, GetCancelToken());
        }
        catch (HioException excpt)
        {
            err = excpt.error;
        }

        /* Setup a reply. */
        Packets.Packet reply = await this.AllocSendPacketAsync();
        reply.isInitiate = false;
        reply.Write(fd);
        reply.Write((UInt32)err);
        reply.Write((UInt64)0);

        /* Send the reply. */
        await this.SendPacketAsync(reply);
    }
}