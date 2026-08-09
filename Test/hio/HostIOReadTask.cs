

using System.Data.SqlTypes;

namespace Test.hio;

public class HostIOReadTask : ServiceTask
{
    HostFilesystemManager mgr;
    
    public HostIOReadTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, parent.serviceId, TaskType.ReadFile, taskId, 0)
    {
        this.mgr = mgr;
    }

    protected override async Task Run()
    {
        /* Get the packet. */
        Packet pkt = await this.WaitForPacket();
        
        /* Parse the packet. */
        pkt.Read(out Int64 fd);
        pkt.Read(out Int64 offset);
        pkt.Read(out Int64 reamining);
        pkt.Release();
        
        Console.WriteLine("[hio] Reading file, fd={0}", fd);

        HioErrorCode err = HioErrorCode.SuccessContinue;
        while (reamining > 0 && err == HioErrorCode.SuccessContinue)
        {
            /* Allocate a send a packet. */
            Packet reply = this.AllocSendPacket();
            reply.isInitiate = false;
            
            /* Determine how much we should read. */
            int curSize = (int)Math.Min(reamining, Packet.DataMaxSize - 0x8);
            Console.WriteLine("Reading {0}", curSize);
        
            /* Try to read the file. */
            err = HioErrorCode.SuccessContinue;
            try
            {
                await mgr.ReadFileAsync((int)fd, reply.GetBuffer(), offset, Packet.HeaderSize + 0x8, curSize);
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
            
            /* Reduce remaining. */
            reamining -= curSize;
            
            /* Send SuccessEnd if this is the final packet. */
            if (err == HioErrorCode.SuccessContinue && reamining <= 0)
                err = HioErrorCode.SuccessEnd;
        
            /* Setup other reply fields. */
            reply.Write((Int32)curSize);
            reply.Write((Int32)err);
            reply.AdvancePosition(curSize);
            reply.WriteHeader();
            reply.Print();
        
            /* Send the reply. */
            this.SendPacket(reply);
        }
    }
}