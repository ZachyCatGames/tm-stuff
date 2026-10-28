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
    
    private SemaphoreSlim doneSignal =  new SemaphoreSlim(0);

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

    private void DisconnectImpl()
    {
        /* Finalize service manager. */
        serviceManager.Finalize();
        
        /* Finalize the file manager. */
        hostfsManager.Dispose();
        
        /* Finalize the htcs socket manager. */
        htcsManager.Dispose();
        
        /* Finalize the packet manager. */
        packetManager.Disconnect();
    }

    public void Disconnect()
    {
        DisconnectImpl();
    }

    public async Task DisconnectAsync()
    {
        /* Run the disconnect routine in a thread. */
        var thread = new Thread(DisconnectImpl);
        thread.Start();
        
        /* Wait for it to finish. */
        await doneSignal.WaitAsync();
        
        /* Join it. */
        thread.Join();
    }
    
    public IPacketManager GetPacketManager()
    {
        return packetManager;
    }
}