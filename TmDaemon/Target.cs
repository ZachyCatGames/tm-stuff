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

    private int currentContextId;
    private Dictionary<int, TargetClientContext> clientContexts;
    
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
        
        /* Setup client context vars. */
        currentContextId = 0;
        clientContexts   = new();
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
        Info.Status = TargetStatus.Connected;
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
        Info.Status = TargetStatus.Disconnected;

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

    public async Task<int> InitializeClientContext()
    {
        /* Allocate a context id. */
        int contextId = Interlocked.Increment(ref currentContextId);
        
        /* Create a new client context. */
        var ctx = new TargetClientContext();
        
        /* Register context callback(s). */
        htcsManager.RegisterEventCallback(ctx.PortEventCallback);
        
        /* Register the client context. */
        clientContexts.Add(contextId, ctx);

        return contextId;
    }

    public async Task<TargetResult> FinalizeClientContext(int contextId)
    {
        /* Find the correct context. */
        if (!clientContexts.TryGetValue(contextId, out var ctx))
            return TargetResult.InvalidContextId;

        /* Release the contextId. */
        clientContexts.Remove(contextId);
        
        /* Unregister our callback. */
        htcsManager.UnregisterEventCallback(ctx.PortEventCallback);
        
        /* Cancel any push/pop operation. */
        ctx.CancelPop();

        return TargetResult.Success;
    }

    public PortInfo? QueryHtcsPortByName(string portName) => htcsManager.GetPortInfoByName(portName);

    public int QueryHtcsPortCount() => htcsManager.PortCount;

    public List<PortInfo> QueryHtcsPortInfos(int maxCount) => htcsManager.GetPortInfos(maxCount);

    public async Task<(TargetResult, htcs.PortEvent?)> GetHtcsPortUpdateAsync(int contextId, CancellationToken cancelToken)
    {
        /* Find the correct context. */
        if (!clientContexts.TryGetValue(contextId, out var ctx))
            return (TargetResult.InvalidContextId, null);
        
        /* Wait for a port update. */
        PortEvent ev;
        try
        {
            ev = await ctx.PopPortEventAsync(cancelToken);
        }
        catch (OperationCanceledException)
        {
            return (TargetResult.OperationCanceled, null);
        }

        return (TargetResult.Success, ev);
    }
    
    public IPacketManager GetPacketManager()
    {
        return packetManager;
    }
}