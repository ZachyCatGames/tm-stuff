using System.Threading.Channels;
using Test.Services.htcs;

namespace Test.Target;

public class TargetClientContext
{
    private class PortCallbackImpl : IPortEventCallback
    {
        private readonly TargetClientContext parent;
        
        public PortCallbackImpl(TargetClientContext ctx)
        {
            parent = ctx;
        }

        public void Notify(PortEvent ev) => parent.PushPortEvent(ev);
    }

    private readonly PortCallbackImpl        portCallback;
    private readonly CancellationTokenSource cancelPop = new();
    
    private readonly Channel<PortEvent> portEventQueue = Channel.CreateUnbounded<PortEvent>();
    
    public TargetClientContext()
    {
        portCallback = new(this);
    }
    
    public IPortEventCallback PortEventCallback { get => portCallback; }
    
    public void PushPortEvent(PortEvent ev) => portEventQueue.Writer.TryWrite(ev);
    public async Task<PortEvent> PopPortEventAsync(CancellationToken cancelToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, cancelPop.Token);
        return await portEventQueue.Reader.ReadAsync(cts.Token);
    }

    public void CancelPop() => cancelPop.Cancel();
}