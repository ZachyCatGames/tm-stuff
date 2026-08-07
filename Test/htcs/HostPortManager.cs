using System.Net;
using System.Net.Sockets;

namespace Test.htcs;

public class HostPortManager
{
    class HostPort
    {
        public UInt32 hostIp;
        public UInt16 hostTcpPort;

        public string htcsPortName;
        public string htcsPeerName;

        public HostPort(UInt32 ip, UInt16 port, string htcsPort, string htcsPeer)
        {
            this.hostIp = ip;
            this.hostTcpPort = port;
            this.htcsPortName = htcsPort;
            this.htcsPeerName = htcsPeer;
        }
    }

    readonly LinkedList<HostPort> hostPorts = new();

    HostPort? FindPortByName(string name)
    {
        foreach (var port in hostPorts)
        {
            if (port.htcsPortName == name)
            {
                return port;
            }
        }

        return null;
    }

    public void RegisterPort(UInt32 ip, UInt16 port, string htcsPort, string htcsPeer)
    {
        hostPorts.AddLast(new HostPort(ip, port, htcsPort, htcsPeer));
    }

    public async Task<int> ConnectAsync(Socket sock, SockAddrHtcs addr)
    {
        /* Find the port. */
        HostPort? port = this.FindPortByName(addr.portName);
        if (port == null)
        {
            return -1;
        }

        /* Try to connect to the host port. */
        await sock.ConnectAsync(new IPEndPoint(port.hostIp, port.hostTcpPort));

        return 0;
    }
}