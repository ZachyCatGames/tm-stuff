namespace Test;

public interface ITargetManager
{
    public Task<int> QueryTargetCountAsync(QueryType type);
    public Task<List<TargetInfo>> QueryTargetInfosAsync(QueryType type, int maxCount);

    public Task<TargetInfo?> ConnectTargetAsync(string serialNumber);
    public Task<bool> DisconnectTargetAsync(string serialNumber);
}
