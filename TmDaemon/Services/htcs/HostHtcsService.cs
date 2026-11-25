namespace Test.Services.htcs;


public class HostHtcsService : ServiceBase
{
    const uint ServiceId = 0xB644D830;
    HtcsSocketManager manager;

    public HostHtcsService(ServiceManager mgr, HtcsSocketManager portMgr) : base(ServiceId, mgr)
    {
        manager = portMgr;
    }

    public override ServiceTask? ProcessNewRequest(Packets.Packet pkt)
    {
        ServiceTask? task = null;
#if HTCS_TASK_DEBUG
        Console.WriteLine("packet get");
        pkt.Print();
#endif
        switch ((TaskType)pkt.taskType)
        {
            case TaskType.Socket:
                task = this.ProcessSocket(pkt);
                break;
            case TaskType.Close:
                task = this.ProcessClose(pkt);
                break;
            case TaskType.Bind:
                task = this.ProcessBind(pkt);
                break;
            case TaskType.Listen:
                task = this.ProcessListen(pkt);
                break;
            case TaskType.Accept:
                task = this.ProcessAccept(pkt);
                break;
            case TaskType.Recv:
                task = this.ProcessRecv(pkt);
                break;
            case TaskType.Send:
                task = this.ProcessSend(pkt);
                break;
            default:
                Console.WriteLine("[htcs] Unknown Task Type:");
                pkt.Print();
                pkt.Release();
                break;
        }

        return task;
    }
    HtcsSocketTask ProcessSocket(Packets.Packet pkt)
    {
        return new (this, manager, pkt.taskId);
    }

    HtcsCloseTask ProcessClose(Packets.Packet pkt)
    {
        return new (this, manager, pkt.taskId);
    }

    HtcsBindTask ProcessBind(Packets.Packet pkt)
    {
        return new (this, manager, pkt.taskId);
    }

    HtcsListenTask ProcessListen(Packets.Packet pkt)
    {
        return new (this, manager, pkt.taskId);
    }

    HtcsAcceptTask ProcessAccept(Packets.Packet pkt)
    {
        return new (this, manager, pkt.taskId);
    }

    HtcsRecvTask ProcessRecv(Packets.Packet pkt)
    {
        return new (this, manager, pkt.taskId);
    }

    HtcsSendTask ProcessSend(Packets.Packet pkt)
    {
        return new (this, manager, pkt.taskId);
    }


}
