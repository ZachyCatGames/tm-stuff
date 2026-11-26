using Test.Packets;

namespace Test.Services.powermanagement;

public class HostPmService : ServiceBase
{
    public class PmStateChangeTask : ServiceTask
    {
        public PmStateChangeTask(HostPmService parent, uint taskId) : base(parent, TaskType.PmStateChange, taskId, 0)
        {}
    
        protected override async Task Run()
        {
            var parent = (HostPmService)this.parent;

            Packet msg = await this.WaitForPacket();
            msg.Read(out int state);
            msg.Release();

            if (parent.supportsStateChangeEv)
            {
                /* Forward the state change. */
                parent.inProgress = true;
                parent.prevState = parent.curState;
                parent.curState = (PmState)state;
                await parent.stateObserver.NotifyStateChange(parent.GetTransition());
        
                /* Wait for our parent to signal us. */
                await parent.prepDoneEv.WaitAsync();
            }
            
            /* Send a reply. */
            Packet pkt = await this.AllocSendPacketAsync();
            pkt.Write(-1);
            await this.SendPacketAsync(pkt);
        }
    }
    
    public class PmSuspendTask : ServiceTask
    {
        public PmSuspendTask(HostPmService parent, uint taskId) : base(parent, TaskType.PmSuspend, taskId, 0)
        {}
    
        protected override async Task Run()
        {
            var parent = (HostPmService)this.parent;

            Packet msg = await this.WaitForPacket();
            msg.Read(out int state);
            msg.Release();

            if (!parent.supportsStateChangeEv)
            {
                /* Emulate modern PmStateChange behavior by sending MinimumAwake + SleepReady. */
                parent.inProgress = true;
                parent.prevState = parent.curState;
                parent.curState = PmState.MinimumAwake;
                await parent.stateObserver.NotifyStateChange(parent.GetTransition());
                await parent.prepDoneEv.WaitAsync();
                parent.prevState = parent.curState;
                parent.curState = PmState.SleepReady;
                await parent.stateObserver.NotifyStateChange(parent.GetTransition());
                await parent.prepDoneEv.WaitAsync();
            }
            
            /* Send a reply. */
            Packet pkt = await this.AllocSendPacketAsync();
            pkt.Write(-1);
            await this.SendPacketAsync(pkt);
            
            /* Signal that we're disconnecting due to sleep. */
            await parent.stateObserver.NotifySuspend();
        }
    }
    
    public const UInt32 ServiceId = 0xCC11BCFD;
    
    private readonly IPmObserver stateObserver;

    private readonly bool supportsStateChangeEv;

    private readonly SemaphoreSlim prepDoneEv = new(0);
    private PmState curState = PmState.FullAwake;
    private PmState prevState = PmState.FullAwake;
    private bool    inProgress = false;
    
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
        
        Console.WriteLine("[pm] new state = {0}", curState);
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

        ServiceTask? task = null;
        switch (pkt.taskType)
        {
            case TaskType.PmSuspend:
                task = new PmSuspendTask(this, pkt.taskId);
                break;
            case TaskType.PmStateChange:
                task = new PmStateChangeTask(this, pkt.taskId);
                break;
            default:
                return null;
        }
        return task;
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
