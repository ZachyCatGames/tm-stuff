namespace Test;

// this will host the send and recv threads
// send will send the packet over the underlying transpor tchannel
// send can be a virtual func

// recv will be a bit more complicated, requests will need to be forwarded to the ServicesManager

public interface IPacketManager : IDisposable
{
    public Task ConnectAsync();
    public Task ReconnectAsync();
    public Task DisconnectAsync();
    public Task CloseAsync();

    public Task<BeaconPacket?> GetBeaconPacket();

    /* Target info comes from transport-specific beacon packets, so this lives here. */
    public Task<TargetInfo> GetTargetInfo();
}
