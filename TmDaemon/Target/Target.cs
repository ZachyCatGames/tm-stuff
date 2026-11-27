using System.Diagnostics;
using System.Security.AccessControl;
using Test.htcs;
using Test.Services;
using Test.Services.dbg;
using Test.Services.env;
using Test.Services.hio;
using Test.Services.htcs;
using Test.Services.powermanagement;
using Test.TargetManager;

namespace Test.Target;

public class Target : IDisposable
{
    private class TargetPmObserver : IPmObserver
    {
        private readonly Target parent;

        public TargetPmObserver(Target target)
        {
            this.parent = target;
        }

        public Task NotifyStateChange(TargetEventType trans) => this.parent.OnPmStateChanged(trans);

        public Task NotifySuspend() => this.parent.OnSuspend();
    }
    
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
    private readonly HostPmService          pmService;
    
    private int currentContextId;
    private readonly Dictionary<int, TargetClientContext> clientContexts = new();
    
    public TargetInfo? Info { get; private set; }

    private async Task OnPmStateChanged(TargetEventType trans)
    {
        /* Notify our parent manager. */
        this.parent.NotifyTargetPmTransition(this, trans);

        this.pmService.NotifyPrepDone();
    }
    
    public Target(ITargetManager parent, ServiceManager serviceManager, IPacketManager packetManager, TargetInfo info)
    {
        this.parent = parent;
        
        this.packetManager  = packetManager;
        this.serviceManager = serviceManager;
        
        /* Set info. */
        this.Info = info;
        
        /* Setup hio and htcs managers. */
        this.hostfsManager   = new HostFilesystemManager();
        this.hostPortManager = new HostPortManager();
        this.htcsManager     = new HtcsSocketManager(this.hostPortManager);
        
        /* Create services. */
        this.htcsService   = new HostHtcsService(serviceManager, this.htcsManager);
        this.hioService    = new HostIOService(serviceManager, this.hostfsManager);
        this.hioDirService = new HostDirectoryIOService(serviceManager, this.hostfsManager);
        this.envService    = new HostEnvService(serviceManager);
        this.debugService  = new HostDebugService(serviceManager, this.taskIdAlloc);
        this.pmService     = new HostPmService(serviceManager, new TargetPmObserver(this), true);
        
        /* Register services. */
        this.serviceManager.RegisterService(this.htcsService);
        this.serviceManager.RegisterService(this.hioService);
        this.serviceManager.RegisterService(this.hioDirService);
        this.serviceManager.RegisterService(this.envService);
        this.serviceManager.RegisterService(this.debugService);
        this.serviceManager.RegisterService(this.pmService);
        
        /* Setup client context vars. */
        this.currentContextId = 0;

        /* Update our status to connected. */
        Debug.Assert(this.Info != null);
        this.Info.Status = TargetStatus.Connected;
    }

    public void Dispose()
    {
        //CloseAsync().Wait();
        this.packetManager.Dispose();
        
        /* Finalize the file manager. */
        this.hostfsManager.Dispose();
        
        /* Finalize the htcs socket manager. */
        this.htcsManager.Dispose();
    }

    public AsyncLockHolder AcquireLock() =>  AsyncLockHolder.Enter(this.asyncLock);

    public Task<AsyncLockHolder> AcquireLockAsync() => AsyncLockHolder.EnterAsync(this.asyncLock);
    
    private void DisconnectUnsafe()
    {
        /* Tear down the packet manager. */
        this.packetManager.DisconnectAsync().Wait();
        
        /* Finalize service manager. */
        this.serviceManager.CancelAllTasksAsync().Wait();
        this.serviceManager.Finalize();
        
        /* Cancel any port event observers. */
        foreach (var ctx in this.clientContexts.Values)
        {
            ctx.Cancel();
        }
        
        /* Update our status to disconnected. */
        this.Info.Status = TargetStatus.Disconnected;

        //Console.WriteLine("{0} disconnected", Info.SerialNumber);
    }
    
    public void Disconnect()
    {
        using var l = AcquireLock();
        DisconnectUnsafe();
    }

    public async Task DisconnectAsync()
    {
        using var l = await AcquireLockAsync();
        
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

    private async Task OnSuspend()
    {
        /* Do nothing if already suspended/suspending. */
        Debug.Assert(this.Info != null);
        if (this.Info.Status == TargetStatus.Asleep)
            return;
        
        /* Tag ourself as suspended. */
        this.Info.Status = TargetStatus.Asleep;
        
        /* Notify our packet manager. */
        await this.packetManager.NotifySuspendAsync();
    }

    public async Task NotifyResume(object obj)
    {
        Debug.Assert(this.Info != null);
        Debug.Assert(this.Info.Status == TargetStatus.Asleep);

        /* Attempt resuming the packet manager. */
        await this.packetManager.NotifyResumeAsync(obj);
        
        Console.WriteLine("[target] resumed");
        
        /* Tag ourself as awake/connected. */
        //this.Info.Status = TargetStatus.Connected;
    }

    public async Task<int> InitializeClientContext(UInt64 flags)
    {
        /* Allocate a context id. */
        int contextId = Interlocked.Increment(ref currentContextId);
        
        /* Create a new client context. */
        var ctx = new TargetClientContext(flags);
        
        /* Register HTCS callback. */
        if ((flags & TargetContextFlags.HTCS) != 0)
            await this.htcsManager.RegisterEventCallbackAsync(ctx.HtcsContext);
        
        /* Register the client context. */
        using (var h = await this.AcquireLockAsync())
            this.clientContexts.Add(contextId, ctx);

        return contextId;
    }

    public async Task<TargetResult> FinalizeClientContext(int contextId)
    {
        /* Find the correct context. */
        if (!this.clientContexts.TryGetValue(contextId, out var ctx))
            return TargetResult.InvalidContextId;

        /* Release the contextId. */
        using (var h = await this.AcquireLockAsync())
            this.clientContexts.Remove(contextId);
        
        /* Unregister our callback. */
        if ((ctx.Flags & TargetContextFlags.HTCS) != 0)
            await this.htcsManager.UnregisterEventCallbackAsync(ctx.HtcsContext);
        
        /* Cancel any push/pop operation. */
        ctx.Cancel();

        return TargetResult.Success;
    }

    public PortInfo? QueryHtcsPortByName(string portName) => this.htcsManager.GetPortInfoByName(portName);

    public int QueryHtcsPortCount() => this.htcsManager.PortCount;

    public List<PortInfo> QueryHtcsPortInfos(int maxCount) => this.htcsManager.GetPortInfos(maxCount);

    public async Task<(TargetResult, PortEvent?)> GetHtcsPortUpdateAsync(int contextId, CancellationToken cancelToken)
    {
        /* Find the correct context. */
        TargetClientContext? ctx;
        using (var h = await AcquireLockAsync())
        {
            if (!this.clientContexts.TryGetValue(contextId, out ctx))
                return (TargetResult.InvalidContextId, null);
        }
        
        /* This context must have HTCS events enabled. */
        if ((ctx.Flags & TargetContextFlags.HTCS) == 0)
            return (TargetResult.CtxOpNotEnabled, null);
        
        /* Wait for a port update. */
        PortEvent ev;
        try
        {
            ev = await ctx.HtcsContext.PopEvent();
        }
        catch (OperationCanceledException)
        {
            return (TargetResult.OperationCanceled, null);
        }

        return (TargetResult.Success, ev);
    }
    
    public IPacketManager GetPacketManager()
    {
        return this.packetManager;
    }

    public void NotifyAbruptClose() => parent.NotifyAbruptDisconnectEvent(this);
}