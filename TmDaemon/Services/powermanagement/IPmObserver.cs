namespace Test.Services.powermanagement;

public interface IPmObserver
{
    /* Yeah this naming is kinda dumb... NotifySuspend is supposed to kill (or prep to)
     * kill comms, whereas NotifyStateChange is supposed indicate upcoming transitions. */
    public Task NotifyStateChange(TargetEventType trans);
    public Task NotifySuspend();
}