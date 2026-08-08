using System.Net.Sockets;

namespace Test.htcs;

public class ResultConversion
{
    static public ErrorCode SocketToHtcs(SocketError err)
    {
        // TODO
        return ErrorCode.HTCS_EUNKNOWN;
    }
    static public int HtcsToTmipc(ErrorCode err)
    {
        switch (err)
        {
            case ErrorCode.HTCS_ENONE:
                return 0;
            case ErrorCode.HTCS_EACCES:
                return 0x271D;
            case ErrorCode.HTCS_EADDRINUSE:
                return 0x2740;
            case ErrorCode.HTCS_EADDRNOTAVAIL:
                return 0x2741;
            case ErrorCode.HTCS_EAGAIN:
                return 0x2733;
            case ErrorCode.HTCS_EALREADY:
                return 0x2735;
            case ErrorCode.HTCS_EBADF:
                return 0x2719;
            case ErrorCode.HTCS_EBUSY:
                return 0x274a;
            case ErrorCode.HTCS_ECONNABORTED:
                return 0x2745;
            case ErrorCode.HTCS_ECONNREFUSED:
                return 0x274D;
            case ErrorCode.HTCS_ECONNRESET:
                return 0x2746;
            case ErrorCode.HTCS_EDESTADDRREQ:
                return 0x2737;
            case ErrorCode.HTCS_EFAULT:
                return 0x271e;
            case ErrorCode.HTCS_EINPROGRESS:
                return 0x2734;
            case ErrorCode.HTCS_EINTR:
                return 0x2714;
            case ErrorCode.HTCS_EINVAL:
                // can also be 0x2736, 0x273d
                return 0x2726;
            case ErrorCode.HTCS_EIO:
                // not present in 0.7 / TM?
                return 1; // unknown
            case ErrorCode.HTCS_EISCONN:
                return 0x2748;
            case ErrorCode.HTCS_EMFILE:
                return 0x2728;
            case ErrorCode.HTCS_EMSGSIZE:
                return 0x2738;
            case ErrorCode.HTCS_ENETDOWN:
                return 0x2742;
            case ErrorCode.HTCS_ENETRESET:
                return 0x2744;
            case ErrorCode.HTCS_ENOBUFS:
                return 0x2747;
            case ErrorCode.HTCS_ENOMEM:
                // not in 0.7 / TM?
                return 1;
            case ErrorCode.HTCS_ENOTCONN:
                return 0x2749;
            case ErrorCode.HTCS_ETIMEDOUT:
                return 0x2749;
            case ErrorCode.HTCS_EUNKNOWN:
            default:
                // numerous others...
                return 0x2715;

        }
    }
}