namespace Test;

// this will host the send and recv threads
// send will send the packet over the underlying transpor tchannel
// send can be a virtual func

// recv will be a bit more complicated, requests will need to be forwarded to the ServicesManager

public interface IPacketManager : IDisposable
{
    public void SendPacket(Packet pkt);
    public Task SendPacketAsync(Packet pkt);

    public void Connect();

    public void GetBeaconPacket(out BeaconPacket? beacon);

    /* Target info comes from transport-specific beacon packets, so this lives here. */
    public TargetInfo GetTargetInfo();

    public void Start();
}
