using System.Threading.Channels;
using Test.Target;

namespace Test.TargetManager;

public class TargetManagerClientContext
{
    public class TargetEventManager
    {
        private class EventCallbackImpl : ITargetEventCallback
        {
            private readonly TargetEventManager parent;

            public EventCallbackImpl(TargetEventManager parent)
            {
                this.parent = parent;
            }

            public void Notify(TargetEvent ev) => parent.PushTargetEvent(ev);
        }

        private readonly EventCallbackImpl evCallback;
        private readonly CancellationToken cancelPop;

        private readonly Channel<TargetEvent> targetEventQueue = Channel.CreateUnbounded<TargetEvent>();
        
        public TargetEventManager(CancellationToken cancelToken)
        {
            evCallback = new EventCallbackImpl(this);
            cancelPop = cancelToken;
        }
        
        public ITargetEventCallback EventCallback { get => evCallback; }
    
        public void PushTargetEvent(TargetEvent ev) => targetEventQueue.Writer.TryWrite(ev);
        public async Task<TargetEvent> PopTargetEvent(CancellationToken cancelToken) => await targetEventQueue.Reader.ReadAsync(cancelToken);
    }

    public readonly UInt64 Flags;
    
    private CancellationTokenSource cancelSrc = new();
    
    public TargetEventManager? TargetEventContext { get; init; }

    public TargetManagerClientContext(UInt64 flags)
    {
        this.Flags = flags;
        
        /* Initialize target event context if needed. */
        if ((flags & (UInt64)ManagerContextFlags.TargetEvent) != 0)
        {
            TargetEventContext = new TargetEventManager(cancelSrc.Token);
        }
    }
    
    public void CancelPop() => cancelSrc.Cancel();
}
