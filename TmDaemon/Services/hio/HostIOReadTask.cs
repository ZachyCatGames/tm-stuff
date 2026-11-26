

namespace Test.Services.hio;

public class HostIOReadTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostIOReadTask(IService parent, HostFilesystemManager mgr, uint taskId) : base(parent, TaskType.ReadFile, taskId, 0)
    {
        this.mgr = mgr;
    }

    protected override async Task Run()
    {
        /* Get the packet. */
        Packets.Packet pkt = await this.WaitForPacket();
        
        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        pkt.Read(out Int64 offset);
        pkt.Read(out Int64 reamining);
        pkt.Release();
        
        Console.WriteLine("[hio] ReadFile, fd={0}", fd);

        HioErrorCode err = HioErrorCode.SuccessContinue;
        while (reamining > 0 && err == HioErrorCode.SuccessContinue)
        {
            /* Allocate a send a packet. */
            Packets.Packet reply = await this.AllocSendPacketAsync();
            reply.isInitiate = false;
            
            /* Determine how much we should read. */
            int curSize = (int)Math.Min(reamining, Packets.Packet.DataMaxSize - 0x8);
            Console.WriteLine("Reading {0}", curSize);
        
            /* Try to read the file. */
            err = HioErrorCode.SuccessContinue;
            try
            {
                await this.mgr.ReadFileAsync((int)fd, reply.GetBuffer(), offset, Packets.Packet.HeaderSize + 0x8, curSize, GetCancelToken());
            }
            catch (HioException excpt)
            {
                err = excpt.error;
                curSize = 0;
            }
            catch (Exception excpt)
            {
                Console.WriteLine(excpt);
            }
            
            Console.WriteLine("Cringe");
            
            /* Reduce remaining, advance offset. */
            reamining -= curSize;
            offset    += curSize;

            /* Send SuccessEnd if this is the final packet. */
            if (err == HioErrorCode.SuccessContinue && reamining <= 0)
                err = HioErrorCode.SuccessEnd;
        
            /* Setup other reply fields. */
            reply.Write((Int32)curSize);
            reply.Write((Int32)err);
            reply.AdvancePosition(curSize);
        
            /* Send the reply. */
            await this.SendPacketAsync(reply);
        }
    }
}