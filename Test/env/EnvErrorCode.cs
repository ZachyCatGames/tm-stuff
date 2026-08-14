

namespace Test.env;

public enum EnvErrorCode
{
    /* NOTE: TMA swaps 0 and 1. */
    NotFound                = 0,
    Success                 = 1,
    UserBufferTooSmall      = 2,
    PacketBufferTooSmall    = 3,
    ConnectionFailure       = 4,
    MiscError               = 5,
}
