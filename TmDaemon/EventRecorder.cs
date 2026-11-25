using System.Threading.Channels;

namespace Test;

public class EventRecorder<T> : IObserver<T>
{
    private readonly Channel<T> eventQueue = Channel.CreateUnbounded<T>();
    
    private readonly CancellationToken cancelToken;

    public EventRecorder(CancellationToken cancelToken)
    {
        this.cancelToken = cancelToken;
    }
    
    public void OnCompleted()
    {
        
    }

    public void OnError(Exception error)
    {
        
    }

    public void OnNext(T value) => eventQueue.Writer.TryWrite(value);

    public async Task<T> PopEvent() => await eventQueue.Reader.ReadAsync(cancelToken);
}