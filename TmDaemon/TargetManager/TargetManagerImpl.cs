using System.Diagnostics;
using System.Threading.Channels;
using Test.Services.powermanagement;
using Test.Target;

namespace Test.TargetManager;

public class TargetManagerImpl : ITargetManager
{
    private readonly Dictionary<string, Target.Target> targets   = new();
    private readonly List<ITargetProvider>             providers = new();

    private readonly Dictionary<int, TargetManagerClientContext> clientContexts   = new();
    private int                                                  currentContextId = 0;
    
    private readonly List<EventRecorder<TargetEvent>> targetEvCallbacks = new();
    private readonly List<EventRecorder<TargetInfo>> discoveryObservers = new();

    private readonly List<TargetInfo> registeredInfo = new();
    private readonly SemaphoreSlim    targetEvLock   = new(1);

    private bool isDiscoveryActive = false;
    private readonly List<TargetInfo> discovered = new();
    private readonly SemaphoreSlim discoveryLock = new(1);

    private readonly Task                    disconnectMonitor;
    private readonly Channel<Target.Target>  disconnectTargets = Channel.CreateUnbounded<Target.Target>();
    private readonly CancellationTokenSource disconnectTokenSource = new();
    
    private async Task<TargetInfo> RegisterConnection(Target.Target targ)
    {
        /* Register the target. */
        targets[targ.Info.SerialNumber] = targ;
        
        /* Send a connect event to all listeners. */
        var info = targ.Info;
        using (var l = await AsyncLockHolder.EnterAsync(targetEvLock))
            targetEvCallbacks.ForEach(c => c.OnNext(new TargetEvent(info, TargetEventType.Connected)));
        
        return info;
    }
    
    public TargetManagerImpl()
    {
        /* Create providers for USB and TICS (TODO). */
        this.providers.Add(new UsbTargetProvider(this));
        
        /* Start the disconnect monitor. */
        disconnectMonitor = DisconnectMonitor();

        /* TODO: Fill out registered list. */
    }
    
    private async Task DisconnectMonitor()
    {
        /* Wait for a disconnect. */
        Target.Target target;
        try
        {
            target = await disconnectTargets.Reader.ReadAsync(disconnectTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        
        /* Disconnect. */
        await this.DisconnectTargetAsync(target);
    }

    private async Task InitializeEventContextImpl(TargetManagerClientContext ctx)
    {
        using var l = await AsyncLockHolder.EnterAsync(targetEvLock);
        
        /* Add the event observer. */
        Debug.Assert(ctx.TargetEventContext != null);
        this.targetEvCallbacks.Add(ctx.TargetEventContext);
        
        /* Send events for each currently registered target. */
        Debug.Assert(this.targetEvCallbacks.Count >= 1);
        foreach (var targ in registeredInfo)
        {
            TargetEventType type = targ.Status == TargetStatus.Disconnected ? TargetEventType.Disconnected : TargetEventType.Connected;
            ctx.TargetEventContext.OnNext(new TargetEvent(targ, type));
        }
    }

    private async Task InitializeDiscoveryContextImpl(TargetManagerClientContext ctx)
    {
        bool isFirst;
        using (var l = await AsyncLockHolder.EnterAsync(discoveryLock))
        {
            /* Add the discovery observer. */
            Debug.Assert(ctx.TargetDiscoveryContext != null);
            this.discoveryObservers.Add(ctx.TargetDiscoveryContext);
            Debug.Assert(this.discoveryObservers.Count >= 1);

            isFirst = discoveryObservers.Count == 1;
        }
        
        /* If this is the first, start discovery on all providers. */
        if (isFirst)
        {
            /* NOTE: this MUST not be done under the lock. */
            providers.ForEach(p => p.BeginTargetHotplug());
            isDiscoveryActive = true;
        }
        else
        {
            /* Send notificaitons for all current events. */
            discovered.ForEach(d => ctx.TargetDiscoveryContext.OnNext(d));
        }
    }
    
    public async Task<int> InitializeClientContext(UInt64 flags)
    {
        /* Allocate a context id. */
        int contextId = Interlocked.Increment(ref currentContextId);
        
        /* Create a new client context. */
        var ctx = new TargetManagerClientContext(flags);
        
        /* Register event callback if needed. */
        if ((flags & ManagerContextFlags.TargetEvent) != 0)
        {
            await this.InitializeEventContextImpl(ctx);
        }
        if ((flags & ManagerContextFlags.TargetDiscover) != 0)
        {
            await this.InitializeDiscoveryContextImpl(ctx);
        }

        /* Register the client context. */
        clientContexts.Add(contextId, ctx);

        return contextId;
    }

    private async Task FinalizeEventContextImpl(TargetManagerClientContext ctx)
    {
        using var l = await AsyncLockHolder.EnterAsync(targetEvLock);
        
        /* Remove the observer. */
        targetEvCallbacks.Remove(ctx.TargetEventContext);
    }

    private async Task FinalizeDiscoveryContextImpl(TargetManagerClientContext ctx)
    {
        using var loc = await AsyncLockHolder.EnterAsync(discoveryLock);
        
        /* Remove the observer. */
        discoveryObservers.Remove(ctx.TargetDiscoveryContext);
        
        /* If this was the last observer, stop discovery. */
        if (discoveryObservers.Count == 0)
        {
            foreach (var p in providers)
                await p.EndTargetHotplug();
            isDiscoveryActive = false;
        }
    }

    public async Task<TargetResult> FinalizeClientContext(int contextId)
    {
        /* Find the correct context. */
        if (!clientContexts.TryGetValue(contextId, out var ctx))
            return TargetResult.InvalidContextId;

        /* Release the contextId. */
        clientContexts.Remove(contextId);
        
        /* Unregister event callback if needed. */
        if ((ctx.Flags & ManagerContextFlags.TargetEvent) != 0)
        {
            await this.FinalizeEventContextImpl(ctx);
        }
        if ((ctx.Flags & ManagerContextFlags.TargetDiscover) != 0)
        {
            await this.FinalizeDiscoveryContextImpl(ctx);
        }
        
        /* Cancel any push/pop operations. */
        ctx.CancelPop();

        return TargetResult.Success;
    }
    
    public async Task<int> QueryTargetCountAsync(QueryType type)
    {
        int all = 0;
        if (type == QueryType.All || type == QueryType.Connected)
        {
            foreach (var p in providers)
            {
                all += await p.QueryConnectedCountAsync();
            }
        }
        if (type == QueryType.All || type == QueryType.Disconnected)
        {
            foreach (var p in providers)
            {
                all += await p.QueryDisconnectedCountAsync();
            }
        }
        return all;
    }

    public async Task<List<TargetInfo>> QueryTargetInfosAsync(QueryType type, int maxCount)
    {
        var infos =  await QueryTargetInfosImpl(type);
        return infos.Slice(0, Math.Min(maxCount, infos.Count));
    }

    public async Task<TargetInfo?> QueryTargetInfoBySerialAsync(string serial)
    {
        /* Query all targets. */
        var infos = await QueryTargetInfosImpl(QueryType.All);
        
        /* See if one with <serial> exists. */
        foreach (var info in infos)
        {
            if (info.SerialNumber == serial)
                return info;
        }

        return null;
    }

    public async Task<(TargetResult, TargetEvent?)> GetTargetUpdateAsync(int contextId, CancellationToken cancelToken)
    {
        /* Find the correct context. */
        if (!clientContexts.TryGetValue(contextId, out var ctx))
            return (TargetResult.InvalidContextId, null);
        
        /* This context must support target events. */
        if ((ctx.Flags & ManagerContextFlags.TargetEvent) == 0)
        {
            return (TargetResult.CtxOpNotEnabled, null);
        }
        
        /* Wait for a target update. */
        TargetEvent ev;
        try
        {
            ev = await ctx.TargetEventContext.PopEvent();
        }
        catch (OperationCanceledException)
        {
            return (TargetResult.OperationCanceled, null);
        }

        return (TargetResult.Success, ev);
    }

    public async Task<TargetInfo?> ConnectTargetAsync(string serialNumber)
    {
        /* Return the target if it already exists. */
        /* TODO: maybe this should return AlreadyConnected? */
        if (targets.TryGetValue(serialNumber, out var outTarg))
        {
            return outTarg.Info;
        }
        
        foreach (var provider in providers)
        {
            Target.Target? targ = await provider.ConnectAsync(serialNumber);
            if (targ != null)
            {
                return await RegisterConnection(targ);
            }
        }
        return null;
    }

    public async Task DisconnectTargetAsync(Target.Target target)
    {
        /* Call disconnect. */
        await target.DisconnectAsync();
        
        /* Remove the target from our dict. */
        /* Do nothing if it's not an active conn. */
        if (!targets.Remove(target.Info.SerialNumber))
            return;
        
        /* Send a disconnect event to all listeners. */
        using (var l = await AsyncLockHolder.EnterAsync(targetEvLock))
            targetEvCallbacks.ForEach(c => c.OnNext(new TargetEvent(target.Info, TargetEventType.Disconnected)));
        
        /* Dispose the target. */
        target.Dispose();
    }

    public async Task<Target.Target?> GetTargetAsync(string serialNumber)
    {
        if (targets.TryGetValue(serialNumber, out var outTarg))
        {
            return outTarg;
        }

        return null;
    }
    
    public async Task<(TargetResult, TargetInfo?)> DiscoverTargetAsync(int contextId, CancellationToken cancelToken)
    {
        TargetManagerClientContext? ctx;
        using (var l = await AsyncLockHolder.EnterAsync(discoveryLock))
        {
            if (!clientContexts.TryGetValue(contextId, out ctx))
                return (TargetResult.InvalidContextId, null);
        }
        
        /* This context must have discovery enabled. */
        Debug.Assert(ctx != null);
        if (!ctx.HasDiscovery)
            return (TargetResult.CtxOpNotEnabled, null);
        
        /* Wait for a new discovery. */
        var info = await ctx.TargetDiscoveryContext.PopEvent();
        return (TargetResult.Success, info);
    }

    public void NotifyDiscoveryEvent(TargetInfo target, bool attached)
    {
        using var l =  AsyncLockHolder.Enter(discoveryLock);
        
        /* Add to the discovered list. */
        discovered.Add(target);
        
        /* Notify current observers. */
        discoveryObservers.ForEach(c => c.OnNext(target));
    }

    private async Task<List<TargetInfo>> QueryTargetInfosImpl(QueryType type)
    {
        List<TargetInfo> infos = new List<TargetInfo>();
        
        if (type == QueryType.All || type == QueryType.Connected)
        {
            foreach (var p in providers)
                infos.AddRange(await p.QueryConnectedInfosAsync());
        }
        if (type == QueryType.All || type == QueryType.Disconnected)
        {
            foreach (var p in providers)
                infos.AddRange(await p.QueryDisconnectedInfosAsync());
        }

        return infos;
    }

    public void NotifyAbruptDisconnectEvent(Target.Target target) => disconnectTargets.Writer.TryWrite(target);

    public void NotifyTargetSuspending(Target.Target target)
    {
        /* Notify all event observers. */
        this.targetEvCallbacks.ForEach(x => x.OnNext(new TargetEvent(target.Info, TargetEventType.Suspended)));
    }

    public void NotifyTargetPmTransition(Target.Target target, TargetEventType trans)
    {
        /* Notify all event observers. */
        this.targetEvCallbacks.ForEach(x => x.OnNext(new TargetEvent(target.Info, trans)));
    }
}