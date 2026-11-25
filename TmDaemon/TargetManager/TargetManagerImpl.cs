using Test.Target;

namespace Test.TargetManager;

public class TargetManagerImpl : ITargetManager
{
    private readonly Dictionary<string, Target.Target> targets   = new();
    private readonly List<ITargetProvider>      providers = new();

    private readonly Dictionary<int, TargetManagerClientContext> clientContexts   = new();
    private int                                                  currentContextId = 0;
    
    private readonly List<ITargetEventCallback> targetEvCallbacks = new();
    

    private TargetInfo RegisterConnection(Target.Target targ)
    {
        /* Register the target. */
        targets[targ.Info.SerialNumber] = targ;
        
        /* Send a connect callback to all listeners. */
        var info = targ.Info;
        targetEvCallbacks.ForEach(c => c.Notify(new TargetEvent(info, TargetEventType.Connected)));
        
        return info;
    }
    
    public TargetManagerImpl()
    {
        /* Create providers for USB and TICS (TODO). */
        providers.Add(new UsbTargetProvider(this));
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
            targetEvCallbacks.Add(ctx.TargetEventContext.EventCallback);
        }

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
        
        /* Unregister event callback if needed. */
        if ((ctx.Flags & ManagerContextFlags.TargetEvent) != 0)
        {
            targetEvCallbacks.Remove(ctx.TargetEventContext.EventCallback);
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
            ev = await ctx.TargetEventContext.PopTargetEvent(cancelToken);
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
                return RegisterConnection(targ);
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
        targetEvCallbacks.ForEach(c => c.Notify(new TargetEvent(target.Info, TargetEventType.Disconnected)));
        
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
}