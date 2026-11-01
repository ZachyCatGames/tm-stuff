using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.VisualBasic;

namespace Test.htcs;

public class HtcsSocketManager : IDisposable
{
    const int SocketCountMax = 0x1000;

    enum HtcsSocketType
    {
        None,
        Port,
        Session,
    }

    class HtcsSocket : IDisposable
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
        
        public virtual void Dispose() {}

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
    }

    class TargetPort : HtcsSocket
    {
        public bool isListening;

        Socket hostSocket;
        private IPEndPoint ep;

        public TargetPort(int fd, SockAddrHtcs addr) : base(fd, addr)
        {
            this.type = HtcsSocketType.Port;
            this.isListening = false;

            /* Create the host socket. */
            hostSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            /* Bind to localhost. */
            hostSocket.Bind(new IPEndPoint(0x0100007F, 0));

            /* Log host socket info. */
            ep = (IPEndPoint)hostSocket.LocalEndPoint;
            Console.WriteLine(ep.Port);
        }

        public override void Dispose()
        {
#if HTCS_TASK_DEBUG
            Console.WriteLine("[htcs] Closing fd={0}", fd);
#endif
            /* Cleanup host socket. */
            hostSocket.Close();
            hostSocket.Dispose();
        }

        public void HostListen(int backlogCount)
        {
            /* Begin listening. */
            hostSocket.Listen(backlogCount);

            /* Set listening flag. */
            this.isListening = true;
        }

        public ValueTask<Socket> HostAcceptAsync(CancellationToken ct)
        {
            try {
                /* Wait for the host socket to accept. */
                return hostSocket.AcceptAsync(ct);
            }
            catch(SocketException excpt) {
                throw new HtcsException(excpt.SocketErrorCode);
            }
            catch(ObjectDisposedException excpt) {
                throw new HtcsException(ErrorCode.HTCS_ECONNABORTED);
            }
        }
        
        public PortInfo Info
        {
            get => new PortInfo { Ip = 0x0100007F, Port = (ushort)ep.Port, PeerName = peerName, PortName = portName };
        }
    }

    // 
    class SessionSocket : HtcsSocket
    {
        Socket hostSocket;

        public SessionSocket(int fd, SockAddrHtcs addr, Socket hostSock) : base(fd, addr)
        {
            this.type = HtcsSocketType.Session;
            this.hostSocket = hostSock;
        }

        public override void Dispose()
        {
#if HTCS_TASK_DEBUG
            Console.WriteLine("[htcs] Closing fd={0}", fd);
#endif
            /* Cleanup host socket. */
            hostSocket.Close();
            hostSocket.Dispose();
        }

        public async ValueTask<int> RecvAsync(ArraySegment<byte> buf, int flags, CancellationToken ct)
        {
            try {
                /* Receive buffer. */
                return await hostSocket.ReceiveAsync(buf, ct);
            }
            catch(SocketException excpt) {
                //Console.WriteLine(excpt.SocketErrorCode);
                throw new HtcsException(excpt.SocketErrorCode);
            }
            catch(ObjectDisposedException) {
                throw new HtcsException(ErrorCode.HTCS_ECONNABORTED);
            }
        }

        public async ValueTask<int> SendAsync(ArraySegment<byte> buf, int flags, CancellationToken ct)
        {
            try {
                /* Send the buffer. */
                return await hostSocket.SendAsync(buf, ct);
            }
            catch(SocketException excpt) {
                throw new HtcsException(excpt.SocketErrorCode);
            }
            catch(ObjectDisposedException) {
                throw new HtcsException(ErrorCode.HTCS_ECONNABORTED);
            }
        }
    }

    readonly FileDescriptorManager<HtcsSocket> sockets = new(SocketCountMax);
    readonly HostPortManager hostPortMgr;
    
    private readonly List<TargetPort> targetPorts = new();
    private readonly List<SessionSocket> sessions = new();
    private readonly SemaphoreSlim socketsLock = new(1);

    private readonly List<IPortEventCallback> portEvObservers = new();

    private int AllocateFileDescriptor() => this.sockets.AllocateFileDescriptor();

    private TargetPort? FindPortByName(string portName) => targetPorts.Find((sock) => sock.portName == portName);

    private bool DoesPortNameExist(string portName) => hostPortMgr.DoesPortNameExist(portName) || this.FindPortByName(portName) != null;

    private void RegisterPortSocketUnsafe(int fd, TargetPort port)
    {
        /* Assign it to the file descriptor. */
        sockets[fd] = port;
            
        /* Add it to the port list. */
        targetPorts.Add(port);
            
        /* Send an added event to each port event observer. */
        portEvObservers.ForEach(c => c.Notify(new PortEvent(port.Info, PortEventType.Added)));
    }
    
    private void RegisterPortSocket(int fd, TargetPort port)
    {
        using (var s = AsyncLockHolder.Enter(socketsLock))
            RegisterPortSocketUnsafe(fd, port);
    }

    private void CleanupPortSocketUnsafe(TargetPort port)
    {
        /* Remove it from the port list. */
        targetPorts.Remove(port);
            
        /* Send an event to each port even observer. */
        portEvObservers.ForEach(c => c.Notify(new PortEvent(port.Info, PortEventType.Removed)));
    }
    
    private void CleanupPortSocket(TargetPort port)
    {
        using (var s = AsyncLockHolder.Enter(socketsLock))
            CleanupPortSocketUnsafe(port);
    }

    public HtcsSocketManager(HostPortManager hpm)
    {
        this.hostPortMgr = hpm;
    }

    public void Dispose()
    {
        CloseAll();
    }

    public void CloseAll()
    {
        /* Clear both lists. */
        targetPorts.ForEach((s) => s.Dispose());
        sessions.ForEach((s) => s.Dispose());
        targetPorts.Clear();
        sessions.Clear();
        
        /* Close all used file descriptors. */
        sockets.Clear();
    }
    
    public int PortCount => targetPorts.Count;
    
    public AsyncLockHolder AcquireLock() => AsyncLockHolder.Enter(socketsLock);

    public async Task<AsyncLockHolder> AcquireLockAsync() => await AsyncLockHolder.EnterAsync(socketsLock);
    
    public PortInfo? GetPortInfoByName(string portName)
    {
        var port = FindPortByName(portName);
        if (port is not null)
            return port.Info;

        return null;
    }

    public void RegisterEventCallbackUnsafe(IPortEventCallback callback)
    {
        if (portEvObservers.Contains(callback))
            return;
            
        /* Push creation events for all current target ports. */
        targetPorts.ForEach(port => callback.Notify(new PortEvent(port.Info, PortEventType.Added)));

        /* Register the callback. */
        portEvObservers.Add(callback);
    }
    
    public async Task RegisterEventCallback(IPortEventCallback callback)
    {
        /*
         * This lock prevents other threads from adding new sockets while
         * these events are being pushed, to avoid duplicate or dropped events
         * */
        using (var s = await AsyncLockHolder.EnterAsync(socketsLock))
            RegisterEventCallbackUnsafe(callback);
    }
    
    public async Task UnregisterEventCallbackUnsafe(IPortEventCallback callback) => portEvObservers.Remove(callback);

    public async Task UnregisterEventCallback(IPortEventCallback callback)
    {
        using (var s = await AsyncLockHolder.EnterAsync(socketsLock))
            UnregisterEventCallbackUnsafe(callback);
    }

    /* I should not be allowed to use one-liners */
    public List<PortInfo> GetPortInfos(int maxCount) => targetPorts.Slice(0, Math.Min(maxCount, targetPorts.Count)).ConvertAll((t) => t.Info);

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
        
        /* Free the file descriptor. */
        sockets[fd].Dispose();
        sockets[fd] = null;
        
        /* Remove it from the appropriate list. */
        var s = sockets[fd];
        if (s is TargetPort t)
        {
            CleanupPortSocket(t);
        }
        else if (s is SessionSocket ses)
        {
            sessions.Remove(ses);
        }
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
        
        /* Register the port with fd. */
        RegisterPortSocket(fd, port);
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

    public async ValueTask<(int, SockAddrHtcs)> AcceptAsync(int fd, CancellationToken ct)
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
        Socket newHostSock = await port.HostAcceptAsync(ct);

        /* Allocate a file desc for the new socket. */
        int newFd = this.AllocateFileDescriptor();

        /* Setup a session socket. */
        var session = new SessionSocket(newFd, port.GetAddress(), newHostSock);
        sockets[newFd] = session;
        sessions.Add(session);
        
        return new (newFd, port.GetAddress());
    }

    public async ValueTask ConnectAsync(int fd, SockAddrHtcs addr, CancellationToken ct) {
        /* Get the socket. */
        HtcsSocket? sockRaw = this.sockets[fd];

        /* It must be initialized but inactive. */
        if (sockRaw is not HtcsSocket sock)
            throw new HtcsException(ErrorCode.HTCS_EBADF);
        else if (sock.type != HtcsSocketType.None)
            throw new HtcsException(ErrorCode.HTCS_EISCONN);

        /* Try to connect to the port. */
        Socket hostSock = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await this.hostPortMgr.ConnectAsync(hostSock, addr, ct);

        /* Create a new session socket. */
        var session = new SessionSocket(fd, addr, hostSock);
        this.sockets[fd] = session;
        sessions.Add(session);
    }

    public async ValueTask<int> RecvPacketAsync(int fd, ArraySegment<byte> buffer, int flags, CancellationToken ct)
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
        return await session.RecvAsync(buffer, flags, ct);
    }

    public async ValueTask<int> SendPacketAsync(int fd, ArraySegment<byte> buffer, int flags, CancellationToken ct)
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
        return await session.SendAsync(buffer, flags, ct);
    }
}
