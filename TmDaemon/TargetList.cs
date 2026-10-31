namespace Test;

public class TargetList : List<Target>, IDisposable
{
    public TargetList() {}
    public TargetList(List<Target> list) : base(list) {}

    public void Dispose()
    {
        foreach (var target in this)
        {
            target.Dispose();
        }
    }
}