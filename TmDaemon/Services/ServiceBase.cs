using Test.Packets;

namespace Test.Services;

public abstract class ServiceBase : IService
{
    public UInt32 serviceId;
    
    public readonly ServiceManager serviceManager;

    public ServiceBase(UInt32 serviceId, ServiceManager mgr)
    {
        this.serviceId = serviceId;
        this.serviceManager = mgr;
    }
    
    protected void RegisterNewTask(ServiceTask task) => this.serviceManager.RegisterNewTask(task);

    public ServiceManager GetManager() => this.serviceManager;
    
    public bool AcceptsServicesId(UInt32 serviceId) => this.serviceId == serviceId;
    public UInt32 GetPrimaryServiceId() => this.serviceId;
    
    public abstract ServiceTask? ProcessNewRequest(Packet pkt);
}