using System.Diagnostics;

namespace Test;

public class TargetManagerImpl : ITargetManager
{
    private readonly Dictionary<string, Target> targets = new();
    private readonly List<ITargetProvider> providers = new();

    private readonly Dictionary<int, TargetManagerClientContext> clientContexts = new();
    private readonly List<ITargetEventCallback> targetEvCallbacks = new();
    private int currentContextId = 0;

    private TargetInfo RegisterConnection(Target targ)
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

    public async Task<int> InitializeClientContext()
    {
        /* Allocate a context id. */
        int contextId = Interlocked.Increment(ref currentContextId);
        
        /* Create a new client context. */
        var ctx = new TargetManagerClientContext();
        
        /* Register context callback(s). */
        targetEvCallbacks.Add(ctx.TargetEventCallback);
        
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
        targetEvCallbacks.Remove(ctx.TargetEventCallback);
        
        /* Cancel any push/pop operations. */
        ctx.CancelPop();

        return TargetResult.Success;
    }
    
    public async Task<int> QueryTargetCountAsync(QueryType type)
    {
        int all = 0;
        if (type == QueryType.All || type == QueryType.Connected)
        {
            providers.ForEach(async (provider) => all += await provider.QueryConnectedCountAsync());
        }
        if (type == QueryType.All || type == QueryType.Disconnected)
        {
            providers.ForEach(async (provider) => all += await provider.QueryDisconnectedCountAsync());
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
        
        /* Wait for a port update. */
        TargetEvent ev;
        try
        {
            ev = await ctx.PopTargetEvent(cancelToken);
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
            Target? targ = await provider.ConnectAsync(serialNumber);
            if (targ != null)
            {
                return RegisterConnection(targ);
            }
        }
        return null;
    }

    public async Task<Target?> GetTargetAsync(string serialNumber)
    {
        if (targets.TryGetValue(serialNumber, out var outTarg))
        {
            return outTarg;
        }

        return null;
    }

    public void NotifyOnDisconnect(Target targ)
    {
        string sn = targ.Info.SerialNumber;
        Debug.Assert(targets.ContainsKey(sn));
        
        /* Remove the target from our dict. */
        targets.Remove(sn);
        
        /* Send a disconnect event to all listeners. */
        targetEvCallbacks.ForEach(c => c.Notify(new TargetEvent(targ.Info, TargetEventType.Disconnected)));
    }

    private async Task<List<TargetInfo>> QueryTargetInfosImpl(QueryType type)
    {
        List<TargetInfo> infos = new List<TargetInfo>();
        
        if (type == QueryType.All || type == QueryType.Connected)
        {
            providers.ForEach(async (provider) => infos.AddRange(await provider.QueryConnectedInfosAsync()));
        }
        if (type == QueryType.All || type == QueryType.Disconnected)
        {
            providers.ForEach(async (provider) => infos.AddRange(await provider.QueryDisconnectedInfosAsync()));
        }

        return infos;
    }
}