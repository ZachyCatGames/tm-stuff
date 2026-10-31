namespace Test;
using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.Marshal;

public struct PacketHeader
{
    public UInt32 serviceId;
    public UInt32 taskId;
    public UInt16 taskType;
    public byte isInitiate;
    public byte reserved_0xb;
    public Int32 dataSize;


    public uint reserved_0x10;
    public uint reserved_0x14;
    public uint reserved_0x18;
    public uint reserved_0x1C;

    public PacketHeader(UInt32 serviceId, UInt32 taskId, UInt16 taskType, byte isInitiate, Int32 dataSize)
    {
        this.serviceId = serviceId;
        this.taskId = taskId;
        this.taskType = taskType;
        this.isInitiate = isInitiate;
        this.reserved_0xb = 0;
        this.dataSize = dataSize;
        this.reserved_0x10 = 0;
        this.reserved_0x14 = 0;
        this.reserved_0x18 = 0;
        this.reserved_0x1C = 0;
    }

    public PacketHeader(byte[] arr)
    {
        int len = Marshal.SizeOf(this);
        IntPtr ptr = IntPtr.Zero;
        try
        {
            ptr = Marshal.AllocHGlobal(len);
            Marshal.Copy(arr, 0, ptr, len);
            this = (PacketHeader)Marshal.PtrToStructure(ptr, this.GetType());
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }

    }

    public void WriteTo(byte[] buf)
    {
        int len = Marshal.SizeOf(this);

        IntPtr ptr = Marshal.AllocHGlobal(len);
        Marshal.StructureToPtr(this, ptr, true);
        Marshal.Copy(ptr, buf, 0, len);
        Marshal.FreeHGlobal(ptr);
    }
};
