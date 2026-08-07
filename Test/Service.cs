namespace Test;

public abstract class Service(uint id, ServiceManager mgr)
{
    public readonly uint serviceId = id;
    public readonly ServiceManager serviceManager = mgr;

    protected void RegisterTask(ServiceTask task)
    {
        serviceManager.RegisterTask(task);
    }

    public uint GetServiceId()
    {
        return serviceId;
    }

    public void NotifyDone(ServiceTask task)
    {
        serviceManager.NotifyTaskDone(task);
    }

    public abstract ServiceTask? ProcessNewRequest(Packet pkt);
}
