using Test.Packets;

namespace Test.Target;

// this will host the send and recv threads
// send will send the packet over the underlying transpor tchannel
// send can be a virtual func

// recv will be a bit more complicated, requests will need to be forwarded to the ServicesManager

public interface IPacketManager : IDisposable
{
    public Task DisconnectAsync();

    public Task NotifySuspendAsync();
    public Task NotifyResumeAsync(object obj);
}
