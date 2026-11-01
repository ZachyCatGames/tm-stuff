namespace Test;

using System.Threading.Channels;

public class TargetManagerClientContext
{
    private class EventCallbackImpl : ITargetEventCallback
    {
        private readonly TargetManagerClientContext parent;

        public EventCallbackImpl(TargetManagerClientContext parent)
        {
            this.parent = parent;
        }

        public void Notify(TargetEvent ev) => parent.PushTargetEvent(ev);
    }

    private readonly EventCallbackImpl       evCallback;
    private readonly CancellationTokenSource cancelPop = new();
    
    private readonly Channel<TargetEvent> targetEventQueue = Channel.CreateUnbounded<TargetEvent>();

    public TargetManagerClientContext()
    {
        evCallback = new(this);
    }
    
    public ITargetEventCallback TargetEventCallback { get => evCallback; }
    
    public void PushTargetEvent(TargetEvent ev) => targetEventQueue.Writer.TryWrite(ev);
    public async Task<TargetEvent> PopTargetEvent(CancellationToken cancelToken) => await targetEventQueue.Reader.ReadAsync(cancelToken);
    public void CancelPop() => cancelPop.Cancel();
}
