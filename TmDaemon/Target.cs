using System.Diagnostics;
using Test.hio;
using Test.htcs;
using Test.env;

namespace Test;

public class Target : IDisposable, IAsyncDisposable
{
    private readonly ITargetManager parent;

    private readonly SemaphoreSlim asyncLock = new(1);
    
    private readonly ServiceManager serviceManager;
    private readonly IPacketManager packetManager;

    private readonly HostFilesystemManager hostfsManager;

    private readonly HostPortManager   hostPortManager;
    private readonly HtcsSocketManager htcsManager;

    private readonly HostHtcsService        htcsService;
    private readonly HostIOService          hioService;
    private readonly HostDirectoryIOService hioDirService;
    private readonly HostEnvService         envService;
    
    private int currentContextId;
    private readonly Dictionary<int, TargetClientContext> clientContexts;

    private Task disconnectMonitor;
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
        
        clientContexts   = new();
    }

    public async Task InitializeAsync()
    {
        /* Register services. */
        serviceManager.RegisterService(htcsService);
        serviceManager.RegisterService(hioService);
        serviceManager.RegisterService(hioDirService);
        serviceManager.RegisterService(envService);
        
        /* Setup client context vars. */
        currentContextId = 0;
        
        /* Start the service manager. */
        serviceManager.Init(packetManager);
        
        /* Get initial info. */
        Info = await packetManager.GetTargetInfo();
        
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
        //CloseAsync().Wait();
        packetManager.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        //await CloseAsync();
        packetManager.Dispose();
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
        /* Cancel the abrupt disconnect monitor. */
        disconnectTokenSource.Cancel();
        
        /* Do nothing if already disconnected. */
        if (Info.Status == TargetStatus.Disconnected)
            return;
        
        /* Tear down the packet manager. */
        packetManager.DisconnectAsync().Wait();
        
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
        /* Cancel the abrupt disconnect monitor. */
        disconnectTokenSource.Cancel();
        
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
    
    public void NotifyAbruptClose() => disconnectSignal.Release();
}