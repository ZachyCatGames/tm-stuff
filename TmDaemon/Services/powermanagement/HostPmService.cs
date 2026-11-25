using Test.Packets;

namespace Test.Services.powermanagement;

public class HostPmService : ServiceBase
{
    public const UInt32 ServiceId = 0xCC11BCFD;
    
    private readonly IObserver<PmState> stateObserver;

    private readonly bool supportsStateChangeEv;

    private readonly SemaphoreSlim prepDoneEv = new(0);
    private PmState curState = PmState.Invalid;
    private bool inProgress = false;
    
    public HostPmService(ServiceManager srvMgr, IObserver<PmState> observer, bool supportsStateChangeEv) : base(ServiceId, srvMgr)
    {
        this.stateObserver = observer;
        this.supportsStateChangeEv = supportsStateChangeEv;
    }

    public void NotifyPrepDone()
    {
        /* Indicate that we're done. */
        inProgress = false;
        
        /* Wake the task so it can tell the target it's eepy time. */
        prepDoneEv.Release();
    }

    /* This should be called by TargetManager or TargetProvider on old BCID vers. */
    public void NotifyResumed()
    {
        curState = PmState.FullAwake;
    }

    public override ServiceTask? ProcessNewRequest(Packet pkt)
    {
        Console.WriteLine("PM Request");
        pkt.Print();
        
        switch (pkt.taskType)
        {
            case TaskType.PmSuspend:
                /* Send a suspend notification if the target doesn't support state change events. */
                if (!supportsStateChangeEv)
                {
                    inProgress = true;
                    curState = PmState.SleepReady;
                    stateObserver.OnNext(curState);
                }
                break;
            case TaskType.PmStateChange:
                /* Send a notification if the target supports state change events. */
                if (supportsStateChangeEv)
                {
                    inProgress = true;
                    pkt.Read(out int state);
                    curState = (PmState)state;
                    stateObserver.OnNext(curState);
                }
                break;
            default:
                return null;
        }
        
        return new PmTask(this, pkt.taskType, pkt.taskId, prepDoneEv);
    }
}
