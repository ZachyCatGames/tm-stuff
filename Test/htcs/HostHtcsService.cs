using Test.htcs;
namespace Test.htcs;


public class HostHtcsService : Service
{
    const uint ServiceId = 0xB644D830;
    HtcsSocketManager manager;

    public HostHtcsService(ServiceManager mgr, HostPortManager portMgr) : base(ServiceId, mgr)
    {
        this.manager = new HtcsSocketManager(portMgr);
    }

    public override ServiceTask? ProcessNewRequest(Packet pkt)
    {
        ServiceTask? task = null;
        Console.WriteLine("packet get");
        pkt.Print();
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
