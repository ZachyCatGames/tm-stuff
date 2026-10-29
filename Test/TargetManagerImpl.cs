using System.Diagnostics;

namespace Test;

public class TargetManagerImpl : ITargetManager
{
    private Dictionary<string, Target> targets = new();
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

        return infos.Slice(0, Math.Min(maxCount, infos.Count));
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
                targets[targ.Info.SerialNumber] = targ;
                return targ.Info;
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
    }
}