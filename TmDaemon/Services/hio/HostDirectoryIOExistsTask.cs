

namespace Test.Services.hio;

public class HostDirectoryIOExistsTask : ServiceTask
{

    public HostDirectoryIOExistsTask(IService parent, uint taskId) : base(parent, TaskType.DirectoryExists, taskId, 0)
    {}
    
    protected override async Task Run()
    {
        /* Get the packet. */
        Packets.Packet pkt = await this.WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        string path = pkt.ReadString(0x301);
        pkt.Release();

        Console.WriteLine("[hio] DirectoryExists, fd={0}", fd);

        /* Check if the dir exists. */
        HioErrorCode err = HioErrorCode.SuccessEnd;
        bool exists = false;
        try
        {
            exists = HostFilesystemManager.DirectoryExists(path);
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
        reply.Write((UInt64)(exists ? 1 : 0)); // unused

        /* Send the reply. */
        await this.SendPacketAsync(reply);
    }
}