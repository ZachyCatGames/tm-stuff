

using System.Data.SqlTypes;

namespace Test.hio;

public class HostIOWriteTask : ServiceTask
{
    HostFilesystemManager mgr;

    public HostIOWriteTask(Service parent, HostFilesystemManager mgr, uint taskId) : base(parent, TaskType.WriteFile, taskId, 0)
    {
        this.mgr = mgr;
    }

    protected override async Task Run()
    {
        Int32 totalWritten = 0;

        HioErrorCode err = HioErrorCode.SuccessContinue;
        bool done = false;
        while (!done)
        {
            /* Get the packet. */
            Packet pkt = await WaitForPacket();

            /* Parse the packet. */
            pkt.Read(out Int64 fd);
            pkt.Read(out Int64 offset);
            pkt.Read(out Int32 size);
            pkt.Read(out bool atEnd);

            Console.WriteLine("[hio] WriteFile, fd={0}, offs={1}, size={2}", fd, offset, size);

            /* Try to read the file. */
            err = HioErrorCode.SuccessContinue;
            try
            {
                await mgr.ReadFileAsync((int)fd, pkt.GetBuffer(), offset, Packet.HeaderSize + 0xD, size);
            }
            catch (HioException excpt)
            {
                err = excpt.error;
                size = 0;
            }
            catch (Exception excpt)
            {
                Console.WriteLine(excpt);
            }

            /* Advance total written size. */
            totalWritten += size;

            /* Release the packet. */
            pkt.Release();

            /* Send SuccessEnd if this is the final packet. */
            if (err == HioErrorCode.SuccessContinue || atEnd)
                done = true;
        }

        /* Setup our reply. */
        /*
         * NOTE: The totalWritten field is treated similarly to 
         * the total read field in ReadTask, but isn't actually
         * returned over the hipc interface, _shrugs_.
         * It's effectiely unused and this is my guess at its intent.
         */
        Packet reply = await AllocSendPacketAsync();
        reply.isInitiate = false;
        reply.Write(totalWritten); // unused
        reply.Write((Int32)err);
        reply.WriteHeader();
        //reply.Print();

        /* Send the reply. */
        await SendPacketAsync(reply);
    }
}