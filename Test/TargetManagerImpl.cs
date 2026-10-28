namespace Test;

public class TargetManagerImpl : ITargetManager
{
    private Dictionary<string, Target> targets;
    private List<ITargetProvider> providers;

    public TargetManagerImpl()
    {
        targets   = new Dictionary<string, Target>();
        providers = new List<ITargetProvider>();
        
        /* Create providers for USB and TICS (TODO). */
        providers.Add(new UsbTargetProvider());
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
            return targets[serialNumber].Info;
        }
        
        foreach (var provider in providers)
        {
            Target? targ = await provider.ConnectAsync(serialNumber);
            if (targ != null)
            {
                targets[targ.Info.serialNumber] = targ;
                return targ.Info;
            }
        }
        return null;
    }

    public async Task<bool> DisconnectTargetAsync(string serialNumber)
    {
        if (targets.ContainsKey(serialNumber))
        {
            /* Disconnect the target. */
            await targets[serialNumber].DisconnectAsync();
            
            /* Remove it from our list of targets. */
            targets.Remove(serialNumber);
            
            return true;
        }

        return false;
    }

}