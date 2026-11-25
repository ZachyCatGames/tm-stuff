namespace Test.TargetManager;

public class TargetList : List<Target.Target>, IDisposable, IAsyncDisposable
{
    public TargetList() {}
    
    public TargetList(List<Target.Target> list) : base(list) {}

    public void Dispose()
    {
        foreach (var target in this)
        {
            target.CloseAsync().Wait();
            target.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var targ in this)
        {
            await targ.CloseAsync();
            await targ.DisposeAsync();
        }
    }
}