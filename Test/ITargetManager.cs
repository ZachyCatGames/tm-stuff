namespace Test;

public interface ITargetManager
{
    public Task<int> QueryTargetCountAsync();
    public Task<TargetInfo> QueryTargetInfoAsync(int index);

    public Task<int> QueryConnectedTargetCountAsync();
    public Task<TargetInfo> QueryConnectedTargetInfoAsync(int index);
}
