using Test.Packets;

namespace Test.Services.powermanagement;

public class HostPmService : ServiceBase
{
    public const UInt32 ServiceId = 0xCC11BCFD;
    
    private readonly IPmObserver stateObserver;

    private readonly bool supportsStateChangeEv;

    private readonly SemaphoreSlim prepDoneEv = new(0);
    private PmState curState = PmState.FullAwake;
    private PmState prevState = PmState.FullAwake;
    private bool inProgress = false;
    
    public HostPmService(ServiceManager srvMgr, IPmObserver observer, bool supportsStateChangeEv) : base(ServiceId, srvMgr)
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
        Console.WriteLine("[pm] Request");
        pkt.Print();
        
        switch (pkt.taskType)
        {
            case TaskType.PmSuspend:
                /* Send a suspend notification if the target doesn't support state change events. */
                if (!supportsStateChangeEv)
                {
                    /* Emulate modern PmStateChange behavior by sending MinimumAwake + SleepReady. */
                    inProgress = true;
                    prevState = curState;
                    curState = PmState.MinimumAwake;
                    stateObserver.Notify(this.GetTransition());
                    prevState = curState;
                    curState = PmState.SleepReady;
                    stateObserver.Notify(this.GetTransition());
                }
                else
                {
                    this.prepDoneEv.Release();
                }
                break;
            case TaskType.PmStateChange:
                /* Send a notification if the target supports state change events. */
                if (supportsStateChangeEv)
                {
                    inProgress = true;
                    pkt.Read(out int state);
                    prevState = curState;
                    curState = (PmState)state;
                    stateObserver.Notify(this.GetTransition());
                }
                else
                {
                    this.prepDoneEv.Release();
                }
                break;
            default:
                return null;
        }
        Console.WriteLine("[pm] new state = {0}", curState);
        return new PmTask(this, pkt.taskType, pkt.taskId, prepDoneEv);
    }

    private TargetEventType GetTransition()
    {
        if (prevState == PmState.FullAwake && curState == PmState.MinimumAwake)
            return TargetEventType.Suspending;
        if (curState == PmState.SleepReady)
            return TargetEventType.Suspended;

        return TargetEventType.Unknown;
    }
}
