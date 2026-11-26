

namespace Test.Services.hio;

public class HostIOCreateTask : ServiceTask
{
    public HostIOCreateTask(IService parent, uint taskId) : base(parent, TaskType.CreateFile, taskId, 0)
    {}
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packets.Packet pkt = await this.WaitForPacket();

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
            HostFilesystemManager.CreateFile(path, size);
        }
        catch (HioException excpt)
        {
            err = excpt.error;
            Console.WriteLine(excpt);
        }

        /* Setup a reply. */
        Packets.Packet reply = await this.AllocSendPacketAsync();
        reply.isInitiate = false;
        reply.Write(fd);
        reply.Write((UInt32)err);
        reply.Write((UInt64)0); // unused

        /* Send the reply. */
        await this.SendPacketAsync(reply);
    }
}