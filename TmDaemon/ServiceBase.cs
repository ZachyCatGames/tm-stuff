namespace Test;

public abstract class ServiceBase : IService
{
    public UInt32 serviceId;
    
    public readonly ServiceManager serviceManager;

    public ServiceBase(UInt32 serviceId, ServiceManager mgr)
    {
        this.serviceId = serviceId;
        serviceManager = mgr;
    }
    
    protected void RegisterTask(ServiceTask task) => serviceManager.RegisterTask(task);

    public ServiceManager GetManager() => serviceManager;
    
    public bool AcceptsServicesId(UInt32 serviceId) => this.serviceId == serviceId;
    public UInt32 GetPrimaryServiceId() => this.serviceId;
    
    public abstract ServiceTask? ProcessNewRequest(Packet pkt);
}