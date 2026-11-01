namespace Test;

using System.Threading.Channels;
using Test.htcs;

public class TargetClientContext
{
    private class PortCallbackImpl : htcs.IPortEventCallback
    {
        private TargetClientContext parent;
        
        public PortCallbackImpl(TargetClientContext ctx)
        {
            parent = ctx;
        }

        public void Callback(PortEvent ev) => parent.PushPortEvent(ev);
    }

    private PortCallbackImpl portCallback;
    
    private Channel<PortEvent> portEventQueue = Channel.CreateUnbounded<PortEvent>();

    private CancellationTokenSource cancelPop = new();
    
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