
using System.Runtime.Intrinsics;
using System.Text;

namespace Test.hio;

public class FileTimeStamp {
    // I'm aware this is 0x18 bytes large, no idea how it's structured?
    // probably some standard nn shit
    
    UInt64 v1;
    UInt64 v2;
    UInt64 v3;
    
    public FileTimeStamp(UInt64 v1, UInt64 v2, UInt64 v3) {
        this.v1 = v1;
        this.v2 = v2;
        this.v3 = v3;
    }
    
    public void ToBytes(byte[] buf, int offs)
    {
        using (var ms = new MemoryStream(buf, offs, 0x18, true))
        {
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(v1);
                writer.Write(v2);
                writer.Write(v3);
            }
        }
    }
}