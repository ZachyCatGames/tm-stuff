namespace Test;

public class HostTaskIdAllocator
{
    public const uint TaskIdOriginBitsMask = 0xFFFF0000;
    public const int TaskIdOriginBitsShift = 16;
    public const uint TaskIdForDevice = 0x0u    << TaskIdOriginBitsShift;
    public const uint TaskIdForHost   = 0x1u    << TaskIdOriginBitsShift;
    public const uint TaskIdForMgr    = 0xFFFFu << TaskIdOriginBitsShift;
    
    private uint currentHostTaskId = 0;
    private uint currentMgrTaskId  = 0;

    public uint AllocateTaskId()
    {
        return (Interlocked.Increment(ref currentHostTaskId) & 0xFFFF) | TaskIdForHost;
    }

    public uint AllocateTaskIdForManager()
    {
        return (Interlocked.Increment(ref currentMgrTaskId) & 0xFFFF) | TaskIdForMgr;
    }
}