using System.Threading.Channels;
using Test.Services.htcs;

namespace Test.Target;

public class TargetClientContext
{
    public class HtcsManager
    {
        private class PortCallbackImpl : IPortEventCallback
        {
            private readonly HtcsManager parent;
        
            public PortCallbackImpl(HtcsManager ctx)
            {
                parent = ctx;
            }

            public void Notify(PortEvent ev) => parent.PushPortEvent(ev);
        }
        
        private readonly PortCallbackImpl   portCallback;
        private readonly CancellationToken  cancelToken;
        private readonly Channel<PortEvent> portEventQueue = Channel.CreateUnbounded<PortEvent>();
        
        public HtcsManager(CancellationToken cancelToken)
        {
            this.cancelToken  = cancelToken;
            this.portCallback = new PortCallbackImpl(this);
        }
        
        public IPortEventCallback PortEventCallback { get => portCallback; }
        
        public void PushPortEvent(PortEvent ev) => portEventQueue.Writer.TryWrite(ev);
        
        public async Task<PortEvent> PopPortEventAsync(CancellationToken cancelToken)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, cancelToken);
            return await portEventQueue.Reader.ReadAsync(cts.Token);
        }
    }

    public readonly UInt64 Flags;
    
    private readonly CancellationTokenSource cancelToken;

    public HtcsManager? HtcsContext { get; init; }

    public TargetClientContext(UInt64 flags)
    {
        Flags = flags;
        
        /* Setup HTCS context if required. */
        if ((Flags & (UInt64)TargetContextFlags.HTCS) != 0)
        {
            HtcsContext = new HtcsManager(cancelToken.Token);
        }
        
    }

    public void Cancel() => cancelToken.Cancel();
}