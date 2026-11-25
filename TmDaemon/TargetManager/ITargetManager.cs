using Test.Target;

namespace Test.TargetManager;

public interface ITargetManager
{
    public Task<int> InitializeClientContext(UInt64 flags);
    public Task<TargetResult> FinalizeClientContext(int contextId);
    
    public Task<int> QueryTargetCountAsync(QueryType type);
    public Task<List<TargetInfo>> QueryTargetInfosAsync(QueryType type, int maxCount);
    public Task<TargetInfo?> QueryTargetInfoBySerialAsync(string serial);

    public Task<TargetInfo?> ConnectTargetAsync(string serialNumber);
    public Task DisconnectTargetAsync(Target.Target target);
    public Task<Target.Target?> GetTargetAsync(string serialNumber);
    
    public Task<(TargetResult, TargetEvent?)> GetTargetUpdateAsync(int contextId, CancellationToken cancelToken);
    public Task<(TargetResult, TargetInfo?)> DiscoverTargetAsync(int contextId, CancellationToken cancelToken);

    public void NotifyDiscoveryEvent(TargetInfo target, bool attached);
}
