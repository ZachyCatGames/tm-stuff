using System.Runtime.InteropServices;
using System.Text;

namespace Test.hio;

public class HioDirectoryEntry {
    
    public readonly HioDirectoryEntryType type;
    public readonly string name;
    public readonly Int64 size;
    
    public const int MaxPackedSize = 0x301 + 1 + 8;
    
    public HioDirectoryEntry(HioDirectoryEntryType type, string name, Int64 size)
    {
        /* Name can't be longer than 0x300 chars. */
        if (name.Length > 0x300)
        {
            throw new HioException(HioErrorCode.AllocationFailed);
        }

        this.type = type;
        this.name = name;
        this.size = size;
    }
    
    public int WriteTo(byte[] bs, int offs)
    {
        int outPos = 0;
        using (var ms = new MemoryStream(bs, offs, MaxPackedSize, true))
        {
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)type);
                writer.Write(Encoding.UTF8.GetBytes(name));
                writer.Write((byte)0); // terminator
                writer.Write(size);
                outPos = (int)ms.Position;
            }
        }
        return outPos;
    }
}
