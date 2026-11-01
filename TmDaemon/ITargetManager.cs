namespace Test;

public interface ITargetManager
{
    public Task<int> InitializeClientContext();
    public Task<TargetResult> FinalizeClientContext(int contextId);
    public Task<bool> IsValidContextId(int contextId);
    
    public Task<int> QueryTargetCountAsync(QueryType type);
    public Task<List<TargetInfo>> QueryTargetInfosAsync(QueryType type, int maxCount);
    public Task<TargetInfo?> QueryTargetInfoBySerialAsync(string serial);
    public Task<(TargetResult, TargetInfo?)> GetTargetUpdateAsync(int contextId, CancellationToken cancelToken);

    public Task<TargetInfo?> ConnectTargetAsync(string serialNumber);
    public Task<Target?> GetTargetAsync(string serialNumber);

    /* eh, should these really be here? */
    public void NotifyOnDisconnect(Target targ);
}
