namespace Test;

public class TargetManagerImpl : ITargetManager
{
    private List<Target> targets;

    public TargetManagerImpl()
    {
        targets = new List<Target>();
    }

    public void AddTarget(Target target)
    {
        targets.Add(target);
    }
    
    public async Task<int> QueryTargetCountAsync()
    {
        return 1;
    }

    public async Task<TargetInfo> QueryTargetInfoAsync(int index)
    {
        return new TargetInfo
        {
            address = "69.69.69.69",
            serialNumber = "XAW696969696969"
        };
    }

    public async Task<int> QueryConnectedTargetCountAsync()
    {
        return 1;
    }
    
    public async Task<TargetInfo> QueryConnectedTargetInfoAsync(int index)
    {
        return new TargetInfo
        {
            address = "69.69.69.69",
            serialNumber = "XAW696969696420"
        };
    }
    
}