namespace Test.Services;

public interface IService
{
    public ServiceManager GetManager();
    
    public bool AcceptsServicesId(UInt32 serviceId);
    public UInt32 GetPrimaryServiceId();
    
    public ServiceTask? ProcessNewRequest(Packet.Packet pkt);
}
