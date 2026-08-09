

namespace Test.hio;

public class HostDirectoryIOReadTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostDirectoryIOReadTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, TaskType.ReadDirectory, taskId, 0)
    {
        this.mgr = mgr;
    }
    
    async void SendErrorImpl(HioErrorCode err)
    {
        Packet pkt = this.AllocRecvPacket();
        pkt.Write((Int32)err);
        pkt.WriteHeader();
        await SendPacketAsync(pkt);
    }

    protected override async Task Run()
    {
        /* Get the packet. */
        Packet pkt = await WaitForPacket();

        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        pkt.Read(out Int32 count);
        pkt.Release();

        Console.WriteLine("[hio] ReadDirectory, fd={0}", fd);
        
        /* Read the directory entries. */
        LinkedList<HioDirectoryEntry> entries;
        try
        {
            entries = mgr.ReadDirectory((int)fd, count);
        }
        catch(HioException excpt)
        {
            /* Send an error back. */
            this.SendErrorImpl(excpt.error);
            return;
        }
        
        foreach (var entry in entries) {
            Packet reply = await AllocSendPacketAsync();
            reply.Write((Int32)0);
            reply.AdvancePosition(entry.WriteTo(reply.GetBuffer(), Packet.HeaderSize + 0x4));
            await SendPacketAsync(reply);
        }

    }
}