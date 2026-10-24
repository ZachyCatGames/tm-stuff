namespace Test;

public interface ITargetProvider
{
    public Task<int> QueryTargetCountAsync();
    public Task<List<TargetInfo>> QueryTargetInfosAsync();
    public Task<Target?> ConnectAsync(string serialNumber);
}
