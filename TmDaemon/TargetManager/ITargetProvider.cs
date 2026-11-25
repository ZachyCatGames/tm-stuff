namespace Test.TargetManager;

public interface ITargetProvider
{
    public Task<int> QueryDisconnectedCountAsync();
    public Task<List<TargetInfo>> QueryDisconnectedInfosAsync();
    
    public Task<int> QueryConnectedCountAsync();
    public Task<List<TargetInfo>> QueryConnectedInfosAsync();
    
    public Task<Target.Target?> ConnectAsync(string serialNumber);

    public void BeginTargetDiscovery();
    public Task EndTargetDiscoveryAsync();
}
