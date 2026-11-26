using System.Threading.Channels;
using Test.Services.htcs;

namespace Test.Target;

public class TargetClientContext
{
    public readonly UInt64 Flags;
    
    private readonly CancellationTokenSource cts = new();
    
    public bool HasHTCS => HtcsContext != null;
    
    public EventRecorder<PortEvent>? HtcsContext { get; init; }

    public TargetClientContext(UInt64 flags)
    {
        this.Flags = flags;
        
        /* Setup HTCS context if required. */
        if ((this.Flags & (UInt64)TargetContextFlags.HTCS) != 0)
        {
            this.HtcsContext = new EventRecorder<PortEvent>(this.cts.Token);
        }
        
    }

    public void Cancel() => this.cts.Cancel();
}