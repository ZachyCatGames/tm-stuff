namespace Test;

public class TargetManagerImpl : ITargetManager
{
    private List<Target> targets;
    private List<ITargetProvider> providers;

    public TargetManagerImpl()
    {
        targets   = new List<Target>();
        providers = new List<ITargetProvider>();
        
        /* Create providers for USB and TICS (TODO). */
        providers.Add(new UsbTargetProvider());
    }

    public void AddTarget(Target target)
    {
        targets.Add(target);
    }
    
    public async Task<int> QueryTargetCountAsync()
    {
        int all = 0;
        foreach (var provider in providers)
        {
            all += await provider.QueryTargetCountAsync();
        }
        return all;
    }

    public async Task<TargetInfo> QueryTargetInfoAsync(int index)
    {
        var list = await QueryTargetInfosImplAsync();
        return list[index];
    }

    public async Task<TargetInfo?> ConnectTargetAsync(string serialNumber)
    {
        foreach (var provider in providers)
        {
            Target? targ = await provider.ConnectAsync(serialNumber);
            if (targ != null)
            {
                targets.Add(targ);
                return targ.Info;
            }
        }
        return null;
    }

    public async Task<int> QueryConnectedTargetCountAsync()
    {
        return targets.Count;
    }
    
    public async Task<TargetInfo> QueryConnectedTargetInfoAsync(int index)
    {
        return targets[index].Info;
    }

    private async Task<List<TargetInfo>> QueryTargetInfosImplAsync()
    {
        var list = new List<TargetInfo>();
        foreach (var provider in providers)
        {
            var cur = await provider.QueryTargetInfosAsync();
            cur.ForEach((e) => list.Add(e));
        }

        return list;
    }
}