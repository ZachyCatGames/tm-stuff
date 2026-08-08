namespace Test.htcs;

public class HtcsRecvTask : ServiceTask
{
    HtcsSocketManager htcsManager;
    public HtcsRecvTask(Service parent, HtcsSocketManager manager, uint taskId) : base(parent, parent.GetServiceId(), TaskType.HtcsSend, taskId, 0)
    {
        this.htcsManager = manager;
    }

    async Task Process()
    {
        /* Receive info packet. */
        Packet pkt = await this.WaitForPacket();

        pkt.Read(out int fd);
        pkt.Read(out Int64 remaining);
        pkt.Read(out int flags);

        /* Release the packet. */
        pkt.Release();

        while (remaining > 0)
        {
            /* Allocate a send packet. */
            var reply = this.AllocSendPacket();

            /* Get data buffer. */
            Int64 curSize = Math.Min(remaining, Packet.DataMaxSize - 0xD);
            var buf = reply.GetDataBuffer(0xD, (int)curSize);

            /* Receive a packet from the host socket. */
            Int32 result = 0;
            Int32 retval = 0;
            try
            {
                retval = await htcsManager.RecvPacketAsync(fd, buf, flags);;
            }
            catch (HtcsException excpt)
            {
                result = ResultConversion.HtcsToTmipc(excpt.error);
                retval = -1;
            }

            Int32 readSize, errorRetCode;
            if (retval < 0)
            {
                readSize = 0;
                errorRetCode = -1;
                result = 1;
            }
            else
            {
                readSize = retval;
                errorRetCode = 0;
                result = 0;
            }

            bool last = curSize == remaining;

            /* Setup the packet. */
            reply.serviceId = this.parent.GetServiceId();
            reply.taskId = this.taskId;
            reply.taskType = this.type;
            reply.isInitiate = false;
            reply.Reset();
            reply.Write(fd);
            reply.Write(errorRetCode);
            reply.Write(result);
            reply.Write(last);
            reply.AdvancePosition(readSize);
            reply.WriteHeader();

            /* Send the packet to the device. */
            this.SendPacket(reply);

            /* Decrement remaining. */
            remaining -= curSize;
        }
    }

    protected override Task Run()
    {
        return this.Process();
    }
}
