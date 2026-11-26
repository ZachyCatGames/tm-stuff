

namespace Test.Services.hio;

public class HostDirectoryIODeleteTask : ServiceTask
{

    public HostDirectoryIODeleteTask(IService parent, uint taskId) : base(parent, TaskType.DeleteDirectory, taskId, 0)
    {}
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packets.Packet pkt = await this.WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        string path = pkt.ReadString(0x301);
        pkt.Read(out bool recursive);
        pkt.Release();

        Console.WriteLine("[hio] DeleteDirectory, fd={0}", fd);

        /* Try to delete the dir. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        try
        {
            HostFilesystemManager.DeleteDirectory(path, recursive);
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
        reply.Write((UInt64)0); // unused

        /* Send the reply. */
        await this.SendPacketAsync(reply);
    }
}