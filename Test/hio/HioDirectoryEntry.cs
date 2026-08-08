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
    
    public void ToBytes(byte[] bs, int offs)
    {
        using (var ms = new MemoryStream(bs, offs, MaxPackedSize, true))
        {
            // TODO: is UTF-8 correct here?
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)type);
                writer.Write(name);
                writer.Write(size);
            }
        }
    }
}
