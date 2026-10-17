using Test.hio;
using Test.htcs;

namespace Test;

public class Target
{
    private ServiceManager serviceManager;
    private IPacketManager packetManager;

    private HostFilesystemManager hostfsManager;

    private HostPortManager hostPortManager;
    private HtcsSocketManager htcsManager;

    public string SerialNumber { get; }
    public string Address { get; }
    public string ConnectionType { get; }

    public Target(IPacketManager packetManager, TargetInfo info)
    {
        serviceManager  = new ServiceManager();
        hostfsManager   = new HostFilesystemManager();
        hostPortManager = new HostPortManager();
        htcsManager     = new HtcsSocketManager(hostPortManager);
        
        SerialNumber   = info.serialNumber;
        Address        = info.address;
        ConnectionType = info.connectionType;
        
        this.packetManager = packetManager;
    }
}