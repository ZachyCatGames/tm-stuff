

namespace Test.htcs;

public enum ErrorCode {
    HTCS_ENONE         =  0,
    HTCS_EACCES        =  2,
    HTCS_EADDRINUSE    =  3,
    HTCS_EADDRNOTAVAIL =  4,
    HTCS_EAGAIN        =  6,
    HTCS_EALREADY      =  7,
    HTCS_EBADF         =  8,
    HTCS_EBUSY         = 10,
    HTCS_ECONNABORTED  = 13,
    HTCS_ECONNREFUSED  = 14,
    HTCS_ECONNRESET    = 15,
    HTCS_EDESTADDRREQ  = 17,
    HTCS_EFAULT        = 21,
    HTCS_EINPROGRESS   = 26,
    HTCS_EINTR         = 27,
    HTCS_EINVAL        = 28,
    HTCS_EIO           = 29,
    HTCS_EISCONN       = 30,
    HTCS_EMFILE        = 33,
    HTCS_EMSGSIZE      = 35,
    HTCS_ENETDOWN      = 38,
    HTCS_ENETRESET     = 39,
    HTCS_ENOBUFS       = 42,
    HTCS_ENOMEM        = 49,
    HTCS_ENOTCONN      = 56,
    HTCS_ETIMEDOUT     = 76,
    HTCS_EUNKNOWN      = 79,
    HTCS_EWOULDBLOCK   = HTCS_EAGAIN,
};