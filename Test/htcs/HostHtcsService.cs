using Test.htcs;
namespace Test.htcs;


public class HostHtcsService : Service
{
    const uint ServiceId = 0xB644D830;
    HtcsSocketManager manager;

    public HostHtcsService(ServiceManager mgr) : base(ServiceId, mgr)
    {
        this.manager = new HtcsSocketManager();
    }

    public override ServiceTask? ProcessNewRequest(Packet pkt)
    {
        ServiceTask? task = null;
        Console.WriteLine("packet get");
        switch ((TaskType)pkt.taskType)
        {
            case TaskType.HtcsSocket:
                task = this.ProcessSocket(pkt);
                break;
            case TaskType.HtcsClose:
                task = this.ProcessClose(pkt);
                break;
            case TaskType.HtcsBind:
                task = this.ProcessBind(pkt);
                break;
            case TaskType.HtcsListen:
                task = this.ProcessListen(pkt);
                break;
            case TaskType.HtcsAccept:
                task = this.ProcessAccept(pkt);
                break;
            case TaskType.HtcsRecv:
                task = this.ProcessRecv(pkt);
                break;
            case TaskType.HtcsSend:
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
    HtcsSocketTask ProcessSocket(Packet pkt)
    {
        return new (this, manager, pkt.taskId);
    }

    HtcsCloseTask ProcessClose(Packet pkt)
    {
        return new (this, manager, pkt.taskId);
    }

    HtcsBindTask ProcessBind(Packet pkt)
    {
        return new (this, manager, pkt.taskId);
    }

    HtcsListenTask ProcessListen(Packet pkt)
    {
        return new (this, manager, pkt.taskId);
    }

    HtcsAcceptTask ProcessAccept(Packet pkt)
    {
        return new (this, manager, pkt.taskId);
    }

    HtcsRecvTask ProcessRecv(Packet pkt)
    {
        return new (this, manager, pkt.taskId);
    }

    HtcsSendTask ProcessSend(Packet pkt)
    {
        return new (this, manager, pkt.taskId);
    }


}
