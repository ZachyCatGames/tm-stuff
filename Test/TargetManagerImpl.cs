using System.Diagnostics;

namespace Test;

public class TargetManagerImpl : ITargetManager
{
    private class TargetRef
    {
        public Target target;
        public int refCount;
    }
    
    private Dictionary<string, TargetRef> targets = new();
    private List<ITargetProvider> providers = new();

    public TargetManagerImpl()
    {
        /* Create providers for USB and TICS (TODO). */
        providers.Add(new UsbTargetProvider(this));
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
        List<TargetInfo> infos = new List<TargetInfo>();
        
        if (type == QueryType.All || type == QueryType.Connected)
        {
            providers.ForEach(async (provider) => infos.AddRange(await provider.QueryConnectedInfosAsync()));
        }
        if (type == QueryType.All || type == QueryType.Disconnected)
        {
            providers.ForEach(async (provider) => infos.AddRange(await provider.QueryDisconnectedInfosAsync()));
        }

        return infos.Slice(0, maxCount);
    }

    public async Task<TargetInfo?> ConnectTargetAsync(string serialNumber)
    {
        /* Return the target if it already exists. */
        /* TODO: maybe this should return AlreadyConnected? */
        if (targets.ContainsKey(serialNumber))
        {
            var t = targets[serialNumber];
            t.refCount++;
            return t.target.Info;
        }
        
        foreach (var provider in providers)
        {
            Target? targ = await provider.ConnectAsync(serialNumber);
            if (targ != null)
            {
                targets[targ.Info.SerialNumber] = new TargetRef
                {
                    target = targ,
                    refCount = 1
                };
                return targ.Info;
            }
        }
        return null;
    }

    public void NotifyOnDisconnect(Target targ)
    {
        string sn = targ.Info.SerialNumber;
        Debug.Assert(targets.ContainsKey(sn));
        
        /* Remove the target from our dict. */
        targets.Remove(sn);
    }

    public void NotifyOnClose(Target targ)
    {
        string sn = targ.Info.SerialNumber;
        Debug.Assert(targets.ContainsKey(sn));
        
        /* Decrease ref count. */
        targets[sn].refCount--;
    }

}