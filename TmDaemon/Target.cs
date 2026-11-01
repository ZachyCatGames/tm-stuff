using Test.hio;
using Test.htcs;
using Test.env;

namespace Test;

public class Target : IDisposable
{
    private readonly ITargetManager parent;

    private readonly SemaphoreSlim asyncLock = new(1);
    
    private readonly ServiceManager serviceManager;
    private readonly IPacketManager packetManager;

    private readonly HostFilesystemManager hostfsManager;

    private readonly HostPortManager hostPortManager;
    private readonly HtcsSocketManager htcsManager;

    private readonly HostHtcsService htcsService;
    private readonly HostIOService hioService;
    private readonly HostDirectoryIOService hioDirService;
    private readonly HostEnvService envService;
    
    private readonly SemaphoreSlim doneSignal =  new SemaphoreSlim(0);

    private int currentContextId;
    private readonly Dictionary<int, TargetClientContext> clientContexts;

    private readonly Task disconnectMonitor;
    private readonly SemaphoreSlim disconnectSignal = new(0);
    private readonly CancellationTokenSource disconnectTokenSource = new();
    
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
        
        /* Run abrupt disconnect monitor. */
        disconnectMonitor = DisconnectMonitor();
    }

    private async Task DisconnectMonitor()
    {
        /* Wait for a disconnect. */
        try
        {
            await disconnectSignal.WaitAsync(disconnectTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        
        /* Disconnect. */
        await DisconnectAsync();
    }

    public void Dispose()
    {
        packetManager.Dispose();
    }

    public AsyncLockHolder AcquireLock() =>  AsyncLockHolder.Enter(asyncLock);

    public Task<AsyncLockHolder> AcquireLockAsync() => AsyncLockHolder.EnterAsync(asyncLock);

    public void Connect()
    {
        /* Start the service manager. */
        serviceManager.Init(packetManager);
        
        /* Start the packet manager properly. */
        packetManager.Connect();
        
        /* Update our status to connected. */
        Info.Status = TargetStatus.Connected;
    }

    private void DisconnectUnsafe()
    {
        /* Cancel the abrupt disconnect monitor. */
        disconnectTokenSource.Cancel();
        
        /* Do nothing if already disconnected. */
        if (Info.Status == TargetStatus.Disconnected)
            return;
        
        /* Finalize service manager. */
        serviceManager.Finalize();
        
        /* Cancel any port event observers. */
        foreach (var ctx in clientContexts.Values)
        {
            ctx.CancelPop();
        }
        
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
        using var l = AcquireLock();
        DisconnectUnsafe();
    }

    public async Task DisconnectUnsafeAsync()
    {
        /* Run the disconnect routine in a thread. */
        var thread = new Thread(async () =>
        {
            /* Disconnect from the target. */
            DisconnectUnsafe();

            /* Signal that we've finished. */
            doneSignal.Release();
        });
        thread.Start();

        /* Wait for it to finish. */
        await doneSignal.WaitAsync();

        /* Join it. */
        thread.Join();
    }

    public async Task DisconnectAsync()
    {
        using var l = await AcquireLockAsync();
        await DisconnectUnsafeAsync();
    }

    public async Task<int> InitializeClientContext()
    {
        /* Allocate a context id. */
        int contextId = Interlocked.Increment(ref currentContextId);
        
        /* Create a new client context. */
        var ctx = new TargetClientContext();
        
        /* Register context callback(s). */
        await htcsManager.RegisterEventCallbackAsync(ctx.PortEventCallback);
        
        /* Register the client context. */
        using (var h = await AcquireLockAsync())
            clientContexts.Add(contextId, ctx);

        return contextId;
    }

    public async Task<TargetResult> FinalizeClientContext(int contextId)
    {
        /* Find the correct context. */
        if (!clientContexts.TryGetValue(contextId, out var ctx))
            return TargetResult.InvalidContextId;

        /* Release the contextId. */
        using (var h = await AcquireLockAsync())
            clientContexts.Remove(contextId);
        
        /* Unregister our callback. */
        await htcsManager.UnregisterEventCallbackAsync(ctx.PortEventCallback);
        
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
        TargetClientContext? ctx;
        using (var h = await AcquireLockAsync())
        {
            if (!clientContexts.TryGetValue(contextId, out ctx))
                return (TargetResult.InvalidContextId, null);
        }
        
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
    
    public void NotifyAbruptDisconnect()
    {
        disconnectSignal.Release();
    }
}