using System.Runtime.CompilerServices;

namespace Test;

public class AsyncLockHolder : IDisposable
{
    private SemaphoreSlim sem;
        
    public AsyncLockHolder(SemaphoreSlim sem)
    {
        this.sem = sem;
    }

    public static AsyncLockHolder Enter(SemaphoreSlim sem)
    {
        sem.Wait();
        return new AsyncLockHolder(sem);
    }
    
    public static async Task<AsyncLockHolder> EnterAsync(SemaphoreSlim sem)
    {
        await sem.WaitAsync();
        return new AsyncLockHolder(sem);
    }

    public void Dispose() => sem.Release();
}
