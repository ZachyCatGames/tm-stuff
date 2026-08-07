namespace Test.htcs;

public class HtcsSendTask : ServiceTask
{
    Int32 sentSoFar;
    HtcsSocketManager htcsManager;

    public HtcsSendTask(Service parent, HtcsSocketManager manager, uint taskId) : base(parent, parent.GetServiceId(), TaskType.HtcsRecv, taskId, 0)
    {
        this.htcsManager = manager;
    }

    async Task<bool> ProcessImpl(Packet pkt)
    {
        /* Parse the packet recv header. */
        pkt.Read(out int fd);
        pkt.Read(out int flags);
        pkt.Read(out int size);
        pkt.Read(out bool last);

        // TODO: Check size

        /* Get buffer. */
        var buf = pkt.GetDataBuffer(0xD, size);

        /* Send the packet to the host socket. */
        int retval = await htcsManager.SendPacketAsync(fd, buf, flags);

        /* Increment sent count. */
        this.sentSoFar += retval;

        /* End if this is the final packet or we hit an error. */
        if (last || retval < 0)
        {
            int errorRetCode, result;

            /* Error? */
            if (retval < 0)
            {
                // TODO: Determine proper result code
                errorRetCode = -1;
                result = 1;
                this.sentSoFar = -1;
            }
            else
            {
                errorRetCode = 0;
                result = 0;
            }

            /* Allocate a packet for our response. */
            Packet reply = this.AllocSendPacket();

            /* Setup the packet. */
            reply.serviceId = parent.GetServiceId();
            reply.taskId = this.taskId;
            reply.taskType = this.type;
            reply.isInitiate = false;
            reply.Reset();
            reply.Write(this.sentSoFar);
            reply.Write(result);
            reply.Write(errorRetCode);
            reply.WriteHeader();

            /* Send the packet to the device. */
            this.SendPacket(reply);

            return true;
        }

        return false;
    }

    async Task Process()
    {
        bool done = false;
        while (!done)
        {
            /* Wait for a packet. */
            Packet pkt = await this.WaitForPacket();

            /* Release the packet. */
            pkt.Release();

            /* Process the packet. */
            done = await this.ProcessImpl(pkt);
        }
    }

    protected override Task Run()
    {
        return this.Process();
    }
}
