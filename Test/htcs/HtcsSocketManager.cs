using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Test.htcs;

/*
Each function has two return values:
    - direct return value
        - this is usually 0 on success or some negative on error
        - the main exception is Socket / CreateSocket, which returns an fd
    - error / result code
        - this is probably SocketError
*/

/*
    needs to work as:
        a device socket connecting to a host port
        a device port
        a device port w/ a connection from a host socket
*/

public class HtcsSocketManager
{
    const int SocketCountMax = 0x1000;

    enum HtcsSocketType
    {
        None,
        Port,
        Session,
    }

    // could have a ring buffer for each socket recv / send end
    // each socket has a constantly running send / recv task that feeds
    // the ring buffer into the host socket
    // the tasks are started on accept / bind

    class HtcsSocket
    {
        public int fd;

        public HtcsSocketType type;

        public string portName;
        public string peerName;

        public HtcsSocket(int fd)
        {
            this.fd = fd;
            this.type = HtcsSocketType.None;
            this.portName = "";
            this.peerName = "";
        }

        public HtcsSocket(int fd, SockAddrHtcs addr)
        {
            this.fd = fd;
            this.type = HtcsSocketType.None;
            this.peerName = addr.peerName;
            this.portName = addr.portName;
        }

        public SockAddrHtcs GetAddress()
        {
            return new SockAddrHtcs(HtcsAddressFamily.Htcs, peerName, portName);
        }

        public virtual void Cleanup() {}
    }

    class TargetPort : HtcsSocket
    {
        public bool isListening;

        Socket hostSocket;
        Task hostAcceptTask;
        SemaphoreSlim pendingAcceptNum;

        public TargetPort(int fd, SockAddrHtcs addr) : base(fd, addr)
        {
            this.type = HtcsSocketType.Port;
            this.isListening = false;

            /* Create the host socket. */
            hostSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            /* Bind to localhost. */
            hostSocket.Bind(new IPEndPoint(0x0100007F, 0));

            /* Log host socket info. */
            //https://stackoverflow.com/questions/9895129/how-do-i-find-an-available-port-before-bind-the-socket-with-the-endpoint
            Console.WriteLine(((IPEndPoint)hostSocket.LocalEndPoint).Port);
        }

        public void HostListen(int backlogCount)
        {
            /* Begin listening. */
            hostSocket.Listen(backlogCount);

            /* Set listening flag. */
            this.isListening = true;
        }

        public Task<Socket> HostAcceptAsync()
        {
            /* Wait for the host socket to accept. */
            return hostSocket.AcceptAsync();
        }

        public override void Cleanup()
        {
            /* Close the host socket. */
            hostSocket.Close();
            hostSocket.Dispose();
        }
    }

    // 
    class SessionSocket : HtcsSocket
    {
        Socket hostSocket;

        public SessionSocket(int fd, SockAddrHtcs addr, Socket hostSock) : base(fd, addr)
        {
            this.hostSocket = hostSock;
        }

        public async Task<int> RecvAsync(ArraySegment<byte> buf, int flags)
        {
            /* Receive buffer. */
            return hostSocket.Receive(buf);
        }

        public async Task<int> SendAsync(ArraySegment<byte> buf, int flags)
        {
            /* Send the buffer. */
            return hostSocket.Send(buf);
        }

        public override void Cleanup()
        {
            /* Close the host socket. */
            hostSocket.Close();
        }
    }

    int curFileDescriptor;
    readonly HtcsSocket?[] sockets = new HtcsSocket[SocketCountMax];

    readonly HostPortManager hostPortMgr;

    public HtcsSocketManager(HostPortManager hpm)
    {
        for (int i = 0; i < SocketCountMax; i++)
        {
            sockets[i] = null;
        }
        curFileDescriptor = 1;

        this.hostPortMgr = hpm;
    }

    int AllocateFileDescriptor()
    {
        for (int i = curFileDescriptor + 1; i != curFileDescriptor; i++)
        {
            if (i == SocketCountMax)
            {
                i = 0;
            }

            if (sockets[i] == null)
            {
                curFileDescriptor = i;
                return i;
            }
        }

        return -1;
    }

    TargetPort? FindPortByName(string portName)
    {
        foreach (var sock in sockets)
        {
            if (sock is HtcsSocket s && s.portName == portName && s.type == HtcsSocketType.Port)
            {
                Console.WriteLine(String.Format("{0} {1}", portName, s.portName));
                Console.WriteLine();
                return (TargetPort)sock;
            }
        }
        return null;
    }

    public int CreateSocket()
    {
        /* Allocate a file descriptor. */
        int fd = AllocateFileDescriptor();
        if (fd < 0)
        {
            return fd;
        }

        /* Create the socket. */
        var sock = new HtcsSocket(fd);
        sockets[fd] = sock;

        return fd;
    }

    public int CloseSocket(int fd)
    {
        if (sockets[fd] == null)
            return -1;

        /* Free the socket. */
        sockets[fd] = null;
        return 0;
    }

    public int Bind(int fd, SockAddrHtcs addr)
    {
        /* Is this name already in use? */
        if (this.FindPortByName(addr.portName) is HtcsSocket s)
        {
            // TODO: errno
            Console.WriteLine("name in use");
            return -1;
        }

        /* Must be a valid socket. */
        if (sockets[fd] == null)
        {
            // TODO: error
            Console.WriteLine("invalid socket");
            return -1;
        }

        /* Get the socket. */
        HtcsSocket sock = (HtcsSocket)sockets[fd];

        /* Must not be connected. */
        if (sock.type != HtcsSocketType.None)
        {
            // TODO: errno
            Console.WriteLine("socket already connected");
            return -1;
        }

        /* Create a new Port socket. */
        // TODO: register the socket under the target's name
        var port = new TargetPort(fd, addr);

        /* Assign it to the file descriptor. */
        sockets[fd] = port;

        return 0;
    }

    public int Listen(int fd, int backlogCount)
    {
        /* Get the socket. */
        HtcsSocket? sockRaw = sockets[fd];

        /* It must be real and be a port. */
        if (sockRaw is not HtcsSocket s)
        {
            // TODO: errno
            return -1;
        }
        else if (s.type != HtcsSocketType.Port)
        {
            // TODO: errno
            return -1;
        }

        /* Get it as a Port. */
        TargetPort port = (TargetPort)sockRaw;

        /* Is it already listening? */
        if (port.isListening)
        {
            // TODO: errno
            return -1;
        }

        /* Begin listening. */
        // TODO: error handling
        port.HostListen(backlogCount);

        return 0;
    }

    public async Task<Tuple<int, SockAddrHtcs?>> AcceptAsync(int fd)
    {
        /* Get the socket. */
        HtcsSocket? sockRaw = sockets[fd];

        /* It must be real and be a port. */
        if (sockRaw is not HtcsSocket s)
        {
            // TODO: errno
            Console.WriteLine("Accept: sock not initialized");
            return new Tuple<int, SockAddrHtcs?>(-1, null);
        }
        else if (s.type != HtcsSocketType.Port)
        {
            // TODO: errno
            Console.WriteLine("Accept: Socket not a port");
            return new Tuple<int, SockAddrHtcs?>(-1, null);
        }

        /* Get it as a Port. */
        TargetPort port = (TargetPort)sockRaw;

        /* Is it listening? */
        if (!port.isListening)
        {
            // TODO: errno
            Console.WriteLine("Accept: Socket not listening");
            return new Tuple<int, SockAddrHtcs?>(-1, null);
        }

        /* Wait for the host socket. */
        // TODO: error handling
        Console.WriteLine("Cringe");
        Socket newHostSock = await port.HostAcceptAsync();
        Console.WriteLine("Cringe2");

        /* Allocate a file desc for the new socket. */
        int newFd = this.AllocateFileDescriptor();

        /* Setup a session socket. */
        sockets[newFd] = new SessionSocket(newFd, port.GetAddress(), newHostSock);

        return new Tuple<int, SockAddrHtcs>(newFd, port.GetAddress());
    }

    public async Task<int> ConnectAsync(int fd, SockAddrHtcs addr) {
        /* Get the socket. */
        HtcsSocket? sockRaw = this.sockets[fd];

        /* It must be initialized but inactive. */
        if (sockRaw is not HtcsSocket sock) {
            // TODO: errno
            return -1;
        }
        else if (sock.type != HtcsSocketType.None) {
            // TODO: errno
            return -1;
        }

        /* Try to connect to the port. */
        Socket hostSock = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        int res = await this.hostPortMgr.ConnectAsync(hostSock, addr);
        if (res != 0) {
            return res;
        }

        /* Create a new session socket. */
        this.sockets[fd] = new SessionSocket(fd, addr, hostSock);

        return 0;
    }

    public async Task<int> RecvPacketAsync(int fd, ArraySegment<byte> buffer, int flags)
    {
        /* Grab the socket. */
        HtcsSocket? sockRaw = sockets[fd];

        /* Make sure the socket is a valid and a session. */
        if (sockRaw is not HtcsSocket s)
        {
            // TODO: errno
            return -1;
        }
        else if (s.type != HtcsSocketType.Session)
        {
            // TODO: errno
            return -1;
        }

        /* Cast to session. */
        var session = (SessionSocket)sockRaw;

        /* Wait for a packet. */
        return await session.RecvAsync(buffer, flags);
    }

    public async Task<int> SendPacketAsync(int fd, ArraySegment<byte> buffer, int flags)
    {
        /* Grab the socket. */
        HtcsSocket? sockRaw = sockets[fd];

        /* Make sure the socket is a valid and a session. */
        if (sockRaw is not HtcsSocket s)
        {
            // TODO: errno
            return -1;
        }
        else if (s.type != HtcsSocketType.Session)
        {
            // TODO: errno
            return -1;
        }

        /* Cast to session. */
        var session = (SessionSocket)sockRaw;

        /* Wait for a packet. */
        return await session.RecvAsync(buffer, flags);
    }
}
