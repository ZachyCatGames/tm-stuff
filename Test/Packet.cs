namespace Test;
using System.Collections.Concurrent;

public class Packet
{
    public const int HeaderSize = 0x20;
    public const int DataMaxSize = 0xE000;
    public const int PacketMaxSize = HeaderSize + DataMaxSize;

    BlockingCollection<Packet> sourceQueue;

    byte[] buffer;
    ArraySegment<byte> headerBuffer;
    ArraySegment<byte> dataBuffer;
    int bufferPos;

    public UInt32 serviceId;
    public UInt32 taskId;
    public TaskType taskType;
    public bool isInitiate;
    public int dataSize { get; private set; }


    public Packet(BlockingCollection<Packet> srcQ)
    {
        sourceQueue = srcQ;
        this.buffer  = new byte[PacketMaxSize];
        this.headerBuffer = new ArraySegment<byte>(buffer, 0, HeaderSize);
        this.dataBuffer = new ArraySegment<byte>(buffer, HeaderSize, DataMaxSize);
        this.bufferPos = HeaderSize;
    }

    public void Reset()
    {
        bufferPos = HeaderSize;
    }

    public void ParseHeader()
    {
        var hdr = new PacketHeader(buffer);
        serviceId = hdr.serviceId;
        taskId = hdr.taskId;
        taskType = (TaskType)hdr.taskType;
        isInitiate = hdr.isInitiate != 0;
        dataSize = hdr.dataSize;
    }

    public void WriteHeader()
    {
        dataSize = bufferPos - HeaderSize;
        var hdr = new PacketHeader(serviceId, taskId, (ushort)taskType, isInitiate ? (byte)1 : (byte)0, dataSize);
        hdr.WriteTo(buffer);
    }

    public void Release()
    {
        /* Release ourself to our source queue. */
        sourceQueue.Add(this);
    }

    public byte[] GetBuffer()
    {
        return buffer;
    }

    public void AdvancePosition(int amt)
    {
        bufferPos += amt;
    }

    public ArraySegment<byte> GetDataBuffer(int size)
    {
        return new ArraySegment<byte>(buffer, HeaderSize, size);
    }

    public ArraySegment<byte> GetDataBuffer(int offset, int size)
    {
        return new ArraySegment<byte>(buffer, HeaderSize + offset, size);
    }
    
    public ArraySegment<byte> GetDataBuffer()
    {
        return dataBuffer;
    }

    public int GetDataSize()
    {
        return bufferPos - HeaderSize;
    }

    public void Write(bool v)
    {
        BitConverter.GetBytes(v).CopyTo(buffer, bufferPos);
        bufferPos += 1;
    }

    public void Write(Int32 v)
    {
        BitConverter.GetBytes(v).CopyTo(buffer, bufferPos);
        bufferPos += 4;
    }

    public void Write(UInt32 v)
    {
        BitConverter.GetBytes(v).CopyTo(buffer, bufferPos);
        bufferPos += 4;
    }

    public void Write(byte[] bytes)
    {
        bytes.CopyTo(buffer, bufferPos);
        bufferPos += bytes.Length;
    }

    public void Read(out bool v)
    {
        v = BitConverter.ToBoolean(buffer, bufferPos);
        bufferPos++;
    }

    public void Read(out Int32 v)
    {
        v = BitConverter.ToInt32(buffer, bufferPos);
        bufferPos += 4;
    }

    public void Read(out UInt32 v)
    {
        v = BitConverter.ToUInt32(buffer, bufferPos);
        bufferPos += 4;
    }

    public void Read(out Int64 v)
    {
        v = BitConverter.ToInt32(buffer, bufferPos);
        bufferPos += 8;
    }

    public void Read(out UInt64 v)
    {
        v = BitConverter.ToUInt32(buffer, bufferPos);
        bufferPos += 8;
    }

    public ArraySegment<byte> Read(int c)
    {
        var off = bufferPos - HeaderSize;
        var b = dataBuffer[off..(off + c)].ToArray();
        bufferPos += c;
        return b;
    }

    public void Print()
    {
        Console.WriteLine("\tServiceId  = {0:X08}", serviceId);
        Console.WriteLine("\tTaskId     = {0:X}", taskId);
        Console.WriteLine("\tTaskType   = {0:X}", taskType);
        Console.WriteLine("\tIsInitiate = {0}", isInitiate);
        Console.WriteLine("\tDataSize   = {0:X08}", dataSize);
    }
}
