

using System.Collections.Concurrent;
using System.Reflection;

namespace Test;

public class PacketQueue
{
    readonly BlockingCollection<Packet> queue;
    readonly SemaphoreSlim addSem;
    readonly SemaphoreSlim takeSem;
    
    public PacketQueue(int max)
    {
        queue = new(max);
        addSem = new(max);
        takeSem = new(max);
    }
    
    public void Add(Packet pkt)
    {
        addSem.Wait();
        queue.Add(pkt);
        takeSem.Release();
    }
    
    public async Task AddAsync(Packet pkt)
    {
        await addSem.WaitAsync();
        queue.Add(pkt);
        takeSem.Release();
    }
    
    public Packet Take()
    {
        takeSem.Wait();
        var it = queue.Take();
        addSem.Release();
        return it;
    }
    
    public async Task<Packet> TakeAsync()
    {
        await takeSem.WaitAsync();
        var it = queue.Take();
        addSem.Release();
        return it;
    }
}