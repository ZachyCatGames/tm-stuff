using Test.hio;
using Test.htcs;
using Test.env;

namespace Test;

public class Target : IDisposable
{
    private ServiceManager serviceManager;
    private IPacketManager packetManager;

    private HostFilesystemManager hostfsManager;

    private HostPortManager hostPortManager;
    private HtcsSocketManager htcsManager;

    private HostHtcsService htcsService;
    private HostIOService hioService;
    private HostDirectoryIOService hioDirService;
    private HostEnvService envService;

    public TargetInfo Info { get; }

    public string SerialNumber { get; }
    public string Address { get; }
    public string ConnectionType { get; }

    public Target(ServiceManager serviceManager, IPacketManager packetManager)
    {
        this.packetManager  = packetManager;
        this.serviceManager = serviceManager;
        
        /* Setup hio and htcs managers. */
        hostfsManager   = new HostFilesystemManager();
        hostPortManager = new HostPortManager();
        htcsManager     = new HtcsSocketManager(hostPortManager);
        
        /* Create services. */
        htcsService   = new HostHtcsService(serviceManager, htcsManager);
        hioService    = new HostIOService(serviceManager, hostfsManager);
        hioDirService = new HostDirectoryIOService(serviceManager, hostfsManager);
        envService    = new HostEnvService(serviceManager);
        
        /* Register services. */
        serviceManager.RegisterService(htcsService);
        serviceManager.RegisterService(hioService);
        serviceManager.RegisterService(hioDirService);
        serviceManager.RegisterService(envService);
        
        Info            = packetManager.GetTargetInfo();
        SerialNumber    = Info.serialNumber;
        Address         = Info.address;
        ConnectionType  = Info.connectionType;
    }

    public void Dispose()
    {
        packetManager.Dispose();
    }

    public void Connect()
    {
        /* Start the service manager. */
        serviceManager.Init(packetManager);
        
        /* Start the packet manager properly. */
        packetManager.Connect();
    }

    public void Disconnect()
    {
        /* TODO: serviceManager.Finalize() */
        /* TODO: hostfsManager.Finalize() */
        /* TODO: hostPortManager.Finalize() */
        /* TODO: htcsManager.Finalize() */
        
        /* Finalize the packet manager. */
        packetManager.Disconnect();
    }
    
    public IPacketManager GetPacketManager()
    {
        return packetManager;
    }
}