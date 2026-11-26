

namespace Test.Services.hio;

public class HostIOGetFileTimeStampTask : ServiceTask
{
    public HostIOGetFileTimeStampTask(IService parent, uint taskId) : base(parent, TaskType.GetFileTimeStamp, taskId, 0)
    {}
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packets.Packet pkt = await this.WaitForPacket();

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
            ft = HostFilesystemManager.GetFileTimeStamp(path);
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
        reply.Write(ft.v1);
        reply.Write(ft.v2);
        reply.Write(ft.v3);

        /* Send the reply. */
        await this.SendPacketAsync(reply);
    }
}