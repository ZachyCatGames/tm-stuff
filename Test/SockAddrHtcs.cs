using System.Net.Sockets;
using System.Text;
using Test.htcs;
namespace Test;

public class SockAddrHtcs
{
    public const int PackedSize = 0x42;
    public const int PeerNameSizeMax = 0x20;
    public const int PortNameSizeMax = 0x20;

    public HtcsAddressFamily family;
    public string peerName;
    public string portName;

    public SockAddrHtcs(HtcsAddressFamily fam, string peer, string port)
    {
        this.family = fam;
        this.peerName = peer;
        this.portName = port;
    }

    public byte[] ToBytes()
    {
        var buf = new byte[PackedSize];
        ASCIIEncoding.ASCII.GetBytes(peerName).CopyTo(buf, 0x2);
        ASCIIEncoding.ASCII.GetBytes(portName).CopyTo(buf, 0x22);
        return buf;
    }

    /* bytes _should_ always be appropriately sized? */
    public static SockAddrHtcs FromBytes(ArraySegment<byte> bytes)
    {
        UInt16 family = BitConverter.ToUInt16(bytes);
        string peerName = Util.DecodeNullTerminatedString(Encoding.UTF8, bytes[0x02..0x22]);
        string portName = Util.DecodeNullTerminatedString(Encoding.UTF8, bytes[0x22..0x42]);
        return new SockAddrHtcs((HtcsAddressFamily)family, peerName, portName);
    }
}
