namespace Test.dbg;

public enum ReplyType
{
    Empty       = 0x00,
    S32         = 0x04,
    S64         = 0x07,
    Buffer      = 0x09,
    ModuleInfo  = 0x0A,
    ProcessInfo = 0x0B,
    ThreadInfo  = 0x0C,
    Empty2      = 0x0E,
    Buffer2     = 0x0F,
}
