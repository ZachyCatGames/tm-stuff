namespace Test;

using System.Threading.Channels;

public class TargetManagerClientContext
{
    private Channel<TargetEvent> targetEventQueue = Channel.CreateUnbounded<TargetEvent>();
    
    public void TargetEventCallback(TargetEvent ev) => targetEventQueue.Writer.TryWrite(ev);

    public async Task<TargetEvent> PopTargetEvent(CancellationToken cancelToken) => await targetEventQueue.Reader.ReadAsync(cancelToken);
}