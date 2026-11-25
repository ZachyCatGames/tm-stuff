using Test.htcs;
using Test.Services;
using Test.Services.dbg;
using Test.Services.env;
using Test.Services.hio;
using Test.Services.htcs;
using Test.TargetManager;

namespace Test.Target;

public class Target : IDisposable, IAsyncDisposable
{
    private readonly ITargetManager parent;

    private readonly SemaphoreSlim asyncLock = new(1);
    
    private readonly ServiceManager serviceManager;
    private readonly IPacketManager packetManager;
    
    private readonly HostTaskIdAllocator taskIdAlloc = new ();

    private readonly HostFilesystemManager hostfsManager;

    private readonly HostPortManager   hostPortManager;
    private readonly HtcsSocketManager htcsManager;

    private readonly HostHtcsService        htcsService;
    private readonly HostIOService          hioService;
    private readonly HostDirectoryIOService hioDirService;
    private readonly HostEnvService         envService;
    private readonly HostDebugService       debugService;
    
    private int currentContextId;
    private readonly Dictionary<int, TargetClientContext> clientContexts;
    
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
        debugService  = new HostDebugService(serviceManager, taskIdAlloc);
        
        clientContexts   = new();
    }

    public async Task InitializeAsync()
    {
        /* Register services. */
        serviceManager.RegisterService(htcsService);
        serviceManager.RegisterService(hioService);
        serviceManager.RegisterService(hioDirService);
        serviceManager.RegisterService(envService);
        serviceManager.RegisterService(debugService);
        
        /* Setup client context vars. */
        currentContextId = 0;
        
        /* Get initial info. */
        Info = await packetManager.GetTargetInfo();
    }

    public void Dispose()
    {
        //CloseAsync().Wait();
        packetManager.Dispose();
        
        /* Finalize the file manager. */
        hostfsManager.Dispose();
        
        /* Finalize the htcs socket manager. */
        htcsManager.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        this.Dispose();
    }

    public AsyncLockHolder AcquireLock() =>  AsyncLockHolder.Enter(asyncLock);

    public Task<AsyncLockHolder> AcquireLockAsync() => AsyncLockHolder.EnterAsync(asyncLock);

    public async Task ConnectAsync()
    {
        /* Start the packet manager properly. */
        await packetManager.ConnectAsync();

        /* Update our status to connected. */
        Info.Status = TargetStatus.Connected;
    }

    private void DisconnectUnsafe()
    {
        /* Do nothing if already disconnected. */
        if (Info.Status == TargetStatus.Disconnected)
            return;
        
        /* Tear down the packet manager. */
        packetManager.DisconnectAsync().Wait();
        
        /* Finalize service manager. */
        serviceManager.CancelAllTasksAsync().Wait();
        serviceManager.Finalize();
        
        /* Cancel any port event observers. */
        foreach (var ctx in clientContexts.Values)
        {
            ctx.Cancel();
        }
        
        /* Update our status to disconnected. */
        Info.Status = TargetStatus.Disconnected;

        //Console.WriteLine("{0} disconnected", Info.SerialNumber);
    }
    
    public void Disconnect()
    {
        using var l = AcquireLock();
        DisconnectUnsafe();
    }

    public async Task DisconnectUnsafeAsync()
    {
        /* Run the disconnect routine in a thread. */
        var doneSignal =  new SemaphoreSlim(0);
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
    
    private async Task CloseAsyncUnsafe()
    {
        /* Call close on the packet manager. */
        await packetManager.CloseAsync();

        /* Finalize service manager. */
        serviceManager.Finalize();
        
        /* Finalize the file manager. */
        hostfsManager.Dispose();
        
        /* Finalize the htcs socket manager. */
        htcsManager.Dispose();
        
        /* Tear down the rest of packet manager. */
        packetManager.Dispose();
    }

    public async Task CloseAsync()
    {
        using var l = await AcquireLockAsync();
        await CloseAsyncUnsafe();
    }

    public async Task<int> InitializeClientContext(UInt64 flags)
    {
        /* Allocate a context id. */
        int contextId = Interlocked.Increment(ref currentContextId);
        
        /* Create a new client context. */
        var ctx = new TargetClientContext(flags);
        
        /* Register HTCS callback. */
        if ((flags & TargetContextFlags.HTCS) != 0)
        {
            await htcsManager.RegisterEventCallbackAsync(ctx.HtcsContext.PortEventCallback);
        }
        
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
        if ((ctx.Flags & TargetContextFlags.HTCS) != 0)
        {
            await htcsManager.UnregisterEventCallbackAsync(ctx.HtcsContext.PortEventCallback);
        }
        
        /* Cancel any push/pop operation. */
        ctx.Cancel();

        return TargetResult.Success;
    }

    public PortInfo? QueryHtcsPortByName(string portName) => htcsManager.GetPortInfoByName(portName);

    public int QueryHtcsPortCount() => htcsManager.PortCount;

    public List<PortInfo> QueryHtcsPortInfos(int maxCount) => htcsManager.GetPortInfos(maxCount);

    public async Task<(TargetResult, PortEvent?)> GetHtcsPortUpdateAsync(int contextId, CancellationToken cancelToken)
    {
        /* Find the correct context. */
        TargetClientContext? ctx;
        using (var h = await AcquireLockAsync())
        {
            if (!clientContexts.TryGetValue(contextId, out ctx))
                return (TargetResult.InvalidContextId, null);
        }
        
        /* This context must have HTCS events enabled. */
        if ((ctx.Flags & TargetContextFlags.HTCS) == 0)
        {
            return (TargetResult.CtxOpNotEnabled, null);
        }
        
        /* Wait for a port update. */
        PortEvent ev;
        try
        {
            ev = await ctx.HtcsContext.PopPortEventAsync(cancelToken);
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

    public void NotifyAbruptClose() => parent.NotifyAbruptDisconnectEvent(this);
}