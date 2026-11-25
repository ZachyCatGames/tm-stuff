using System.Net.Sockets;

namespace Test.Services.htcs;

public class HtcsException : Exception
{
    public ErrorCode error;

    public HtcsException()
    {
        this.error = ErrorCode.HTCS_EUNKNOWN;
    }

    public HtcsException(SocketError err)
    {
        this.error = ResultConversion.SocketToHtcs(err);
    }

    public HtcsException(ErrorCode err)
    {
        this.error = err;
    }
}