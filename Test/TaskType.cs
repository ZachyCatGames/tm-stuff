namespace Test;

public enum TaskType : ushort
{
    HtcsSocket = 0x1A,
    HtcsClose = 0x1B,
    HtcsConnect = 0x1C,
    HtcsBind = 0x1D,
    HtcsListen = 0x1E,
    HtcsAccept = 0x1F,
    HtcsRecv = 0x20,
    HtcsSend = 0x21,
    HtcsShutdown = 0x22,
    HtcsFcntl = 0x23,
}
