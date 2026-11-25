using System.Threading.Channels;
using Test.Target;

namespace Test.TargetManager;

public class TargetManagerClientContext
{
    public readonly UInt64 Flags;
    
    private CancellationTokenSource cancelSrc = new();
    
    public bool HasTargetEvent => TargetEventContext != null;
    public bool HasDiscovery => TargetDiscoveryContext != null;
    
    public EventRecorder<TargetEvent>? TargetEventContext { get; init; }
    public EventRecorder<TargetInfo>? TargetDiscoveryContext { get; init;  }

    public TargetManagerClientContext(UInt64 flags)
    {
        this.Flags = flags;
        
        /* Initialize target event context if needed. */
        if ((flags & (UInt64)ManagerContextFlags.TargetEvent) != 0)
        {
            TargetEventContext = new(cancelSrc.Token);
        }
        if ((flags & (UInt64)ManagerContextFlags.TargetDiscover) != 0)
        {
            TargetDiscoveryContext = new(cancelSrc.Token);
        }
    }
    
    public void CancelPop() => cancelSrc.Cancel();
}
