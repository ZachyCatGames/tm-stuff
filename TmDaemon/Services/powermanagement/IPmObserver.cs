namespace Test.Services.powermanagement;

public interface IPmObserver
{
    public Task Notify(TargetEventType trans);
}