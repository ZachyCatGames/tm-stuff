
using System.Security.Cryptography;
using System.Text;

namespace Test;

public static class Util
{
    public static int FindNullTerminator(ArraySegment<byte> bs)
    {
        for (int i = 0; i < bs.Count; i++)
        {
            if (bs[i] == 0)
                return i;
        }
        return bs.Count - 1;
    }
    
    public static int FindNullTerminator(byte[] bs, int index, int count)
    {
        return FindNullTerminator(new ArraySegment<byte>(bs, index, count));
    }
    
    public static int FindNullTerminator(byte[] bs)
    {
        return FindNullTerminator(new ArraySegment<byte>(bs));
    }
    
    public static string DecodeNullTerminatedString(Encoding enc, ArraySegment<byte> bs)
    {
        int term = FindNullTerminator(bs);
        return enc.GetString(bs[0..term]);
    }
    
    public static string DecodeNullTerminatedString(Encoding enc, byte[] bs)
    {
        return DecodeNullTerminatedString(enc, new ArraySegment<byte>(bs));
    }
    
    public static string DecodeNullTerminatedString(Encoding enc, byte[] bs, int index, int count)
    {
        return DecodeNullTerminatedString(enc, new ArraySegment<byte>(bs, index, count));
    }
}