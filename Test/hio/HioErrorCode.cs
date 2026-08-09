
namespace Test.hio;

public enum HioErrorCode {
    SuccessContinue         = 0,
    SuccessEnd              = 1,
    AllocationFailed        = 2,
    DirectoryNotEmpty       = 3,
    DirectoryStatusChanged  = 4,
    EntryCorrupted          = 5,
    FileCorrupted           = 6,
    FileDataCorrupted       = 7,
    Unknown8                = 8,
    Unknown9                = 9,
    Unknown10               = 10,
    Unknown11               = 11,
    OutOfRange              = 12,
    PathAlreadyExists       = 13,
    PathNotFound            = 14,
    TargetLocked            = 15,
    Unknown16               = 16,
    UsableSpaceNotEnough    = 17,

    /* Unofficial extensions. */
    /* Official tma will convert these to a default result, */
    /* which is _fine_. */
    /* I mostly have these for clarity on the manager end. */
    InvalidFileDescriptor   = 100,
    Unknown                 = 101,
    NotImplemented          = 102,
}
    