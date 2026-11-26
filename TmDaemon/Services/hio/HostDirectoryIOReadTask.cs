

namespace Test.Services.hio;

public class HostDirectoryIOReadTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostDirectoryIOReadTask(IService parent, HostFilesystemManager mgr, uint taskId) : base(parent, TaskType.ReadDirectory, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    async Task SendErrorImpl(HioErrorCode err)
    {
        Packets.Packet pkt = await this.AllocSendPacketAsync();
        pkt.Write((Int32)err);
        pkt.WriteHeader();
        await this.SendPacketAsync(pkt);
    }

    protected override async Task Run()
    {
        /* Get the packet. */
        Packets.Packet pkt = await this.WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        pkt.Read(out Int32 count);
        pkt.Release();

        Console.WriteLine("[hio] ReadDirectory, fd={0}", fd);
        
        /* Read the directory entries. */
        LinkedList<HioDirectoryEntry> entries;
        try
        {
            entries = this.mgr.ReadDirectory((int)fd, count);
        }
        catch(HioException excpt)
        {
            /* Send an error back. */
            this.SendErrorImpl(excpt.error);
            return;
        }
        
        foreach (var entry in entries) {
            Packets.Packet reply = await this.AllocSendPacketAsync();
            reply.Write((Int32)0);
            reply.AdvancePosition(entry.WriteTo(reply.GetBuffer(), Packets.Packet.HeaderSize + 0x4));
            await this.SendPacketAsync(reply);
        }

    }
}