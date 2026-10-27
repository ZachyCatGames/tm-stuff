namespace Test;

public interface ITargetManager
{
    public Task<int> QueryTargetCountAsync(QueryType type);
    public Task<TargetInfo> QueryTargetInfosAsync(QueryType type, int index);

    public Task<TargetInfo?> ConnectTargetAsync(string serialNumber);
}
