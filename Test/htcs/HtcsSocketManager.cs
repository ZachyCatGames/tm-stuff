using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Test.htcs;

public class HtcsSocketManager
{
    const int SocketCountMax = 0x1000;

    enum HtcsSocketType
    {
        None,
        Port,
        Session,
    }

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
            try {
                /* Wait for the host socket to accept. */
                return hostSocket.AcceptAsync();
            }
            catch(SocketException excpt) {
                throw new HtcsException(excpt.SocketErrorCode);
            }
            catch(ObjectDisposedException excpt) {
                throw new HtcsException(ErrorCode.HTCS_ECONNABORTED);
            }
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
            try {
                /* Receive buffer. */
                return hostSocket.Receive(buf);
            }
            catch(SocketException excpt) {
                throw new HtcsException(excpt.SocketErrorCode);
            }
            catch(ObjectDisposedException excpt) {
                throw new HtcsException(ErrorCode.HTCS_ECONNABORTED);
            }
        }

        public async Task<int> SendAsync(ArraySegment<byte> buf, int flags)
        {
            try {
                /* Send the buffer. */
                return hostSocket.Send(buf);
            }
            catch(SocketException excpt) {
                throw new HtcsException(excpt.SocketErrorCode);
            }
            catch(ObjectDisposedException excpt) {
                throw new HtcsException(ErrorCode.HTCS_ECONNABORTED);
            }
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

    bool DoesPortNameExist(string portName) {
        return hostPortMgr.DoesPortNameExist(portName) || this.FindPortByName(portName) != null;
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

    public void CloseSocket(int fd)
    {
        if (sockets[fd] == null)
            throw new HtcsException(ErrorCode.HTCS_EBADF);

        /* Free the socket. */
        sockets[fd] = null;
    }

    public void Bind(int fd, SockAddrHtcs addr)
    {
        /* Is this name already in use? */
        if (this.DoesPortNameExist(addr.portName))
            throw new HtcsException(ErrorCode.HTCS_EADDRINUSE);

        /* Must be a valid socket. */
        if (sockets[fd] == null)
            throw new HtcsException(ErrorCode.HTCS_EBADF);

        /* Get the socket. */
        HtcsSocket sock = (HtcsSocket)sockets[fd];

        /* Must not be connected. */
        if (sock.type != HtcsSocketType.None)
            throw new HtcsException(ErrorCode.HTCS_EINVAL);

        /* Create a new Port socket. */
        // TODO: register the socket under the target's name
        var port = new TargetPort(fd, addr);

        /* Assign it to the file descriptor. */
        sockets[fd] = port;
    }

    public void Listen(int fd, int backlogCount)
    {
        /* Get the socket. */
        HtcsSocket? sockRaw = sockets[fd];

        /* It must be real and be a port. */
        if (sockRaw is not HtcsSocket s)
            throw new HtcsException(ErrorCode.HTCS_EBADF);
        else if (s.type != HtcsSocketType.Port)
            throw new HtcsException(ErrorCode.HTCS_EADDRINUSE);

        /* Get it as a Port. */
        TargetPort port = (TargetPort)sockRaw;

        /* Do nothing if already listening. */
        if (port.isListening) 
            return;

        /* Begin listening. */
        port.HostListen(backlogCount);
    }

    public async Task<SockAddrHtcs> AcceptAsync(int fd)
    {
        /* Get the socket. */
        HtcsSocket? sockRaw = sockets[fd];

        /* It must be real and be a port. */
        if (sockRaw is not HtcsSocket s)
            throw new HtcsException(ErrorCode.HTCS_EBADF);
        else if (s.type != HtcsSocketType.Port)
            throw new HtcsException(ErrorCode.HTCS_EINVAL);

        /* Get it as a Port. */
        TargetPort port = (TargetPort)sockRaw;

        /* Is it listening? */
        if (!port.isListening)
            throw new HtcsException(ErrorCode.HTCS_EINVAL);

        /* Wait for the host socket. */
        Socket newHostSock = await port.HostAcceptAsync();

        /* Allocate a file desc for the new socket. */
        int newFd = this.AllocateFileDescriptor();

        /* Setup a session socket. */
        sockets[newFd] = new SessionSocket(newFd, port.GetAddress(), newHostSock);

        return port.GetAddress();
    }

    public async Task ConnectAsync(int fd, SockAddrHtcs addr) {
        /* Get the socket. */
        HtcsSocket? sockRaw = this.sockets[fd];

        /* It must be initialized but inactive. */
        if (sockRaw is not HtcsSocket sock)
            throw new HtcsException(ErrorCode.HTCS_EBADF);
        else if (sock.type != HtcsSocketType.None)
            throw new HtcsException(ErrorCode.HTCS_EISCONN);

        /* Try to connect to the port. */
        Socket hostSock = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await this.hostPortMgr.ConnectAsync(hostSock, addr);

        /* Create a new session socket. */
        this.sockets[fd] = new SessionSocket(fd, addr, hostSock);
    }

    public async Task<int> RecvPacketAsync(int fd, ArraySegment<byte> buffer, int flags)
    {
        /* Grab the socket. */
        HtcsSocket? sockRaw = sockets[fd];

        /* Make sure the socket is a valid and a session. */
        if (sockRaw is not HtcsSocket s)
            throw new HtcsException(ErrorCode.HTCS_EBADF);
        else if (s.type != HtcsSocketType.Session)
            throw new HtcsException(ErrorCode.HTCS_ENOTCONN);

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
            throw new HtcsException(ErrorCode.HTCS_EBADF);
        else if (s.type != HtcsSocketType.Session)
            throw new HtcsException(ErrorCode.HTCS_ENOTCONN);

        /* Cast to session. */
        var session = (SessionSocket)sockRaw;

        /* Wait for a packet. */
        return await session.RecvAsync(buffer, flags);
    }
}
