using Test.hio;
using Test.htcs;
using Test.env;

namespace Test;

public class Target : IDisposable
{
    private ITargetManager parent;
    
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

    private TargetInfo? cachedInfo;

    public TargetInfo Info { get; private set; }

    public Target(ITargetManager parent, ServiceManager serviceManager, IPacketManager packetManager)
    {
        this.parent = parent;
        
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
        
        /* Get initial info. */
        Info = packetManager.GetTargetInfo();
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
        
        /* Update our status to connected. */
        cachedInfo.Status = TargetStatus.Connected;
    }

    private void DisconnectImpl()
    {
        /* Finalize service manager. */
        serviceManager.Finalize();
        
        /* Finalize the file manager. */
        hostfsManager.Dispose();
        
        /* Finalize the htcs socket manager. */
        htcsManager.Dispose();
        
        /* Tear down the rest of packet manager. */
        packetManager.Dispose();
        
        /* Notify manager. */
        parent.NotifyOnDisconnect(this);
        
        /* Update our status to disconnected. */
        cachedInfo.Status = TargetStatus.Disconnected;

        //Console.WriteLine("{0} disconnected", Info.serialNumber);
    }
    
    public void Disconnect()
    {
        DisconnectImpl();
    }

    public async Task DisconnectAsync()
    {
        /* Run the disconnect routine in a thread. */
        var thread = new Thread(() =>
        {
            /* Disconnect from the target. */
            DisconnectImpl();
        
            /* Signal that we've finished. */
            doneSignal.Release();
        });
        thread.Start();
        
        /* Wait for it to finish. */
        await doneSignal.WaitAsync();
        
        /* Join it. */
        thread.Join();
    }

    public PortInfo? QueryHtcsPortByName(string portName) => htcsManager.GetPortInfoByName(portName);

    public int QueryHtcsPortCount() => htcsManager.PortCount;

    public List<PortInfo> QueryHtcsPortInfos(int maxCount) => htcsManager.GetPortInfos(maxCount);
    
    public IPacketManager GetPacketManager()
    {
        return packetManager;
    }
}