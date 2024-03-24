using System.Runtime.Intrinsics;

interface IPadding
{
    Vector256<byte>[] Pad(byte[] bytes);

    byte[] Unpad(Vector256<byte>[] padded, int padLength);
}

class Padding : IPadding
{

    public Vector256<byte>[] Pad(byte[] bytes)
    {
        var padLength = 32 - (bytes.Length % 32);
        if (padLength == 0)
            padLength = 32;

        var padding = new byte[padLength];

        for (var i = 0; i < padLength; ++i)
        {
            padding[i] = 0;
        }
        
        return bytes.Concat(padding).Chunk(32).Select(b => Vector256.Create(b)).ToArray();
    }

    public byte[] Unpad(Vector256<byte>[] padded, int padLength)
    {
        var bytes = padded.SelectMany(v => {
            var b = new byte[32];
            v.AsByte().CopyTo(b);
            return b;
        }).ToArray();

        var unpadded = bytes.SkipLast(padLength).ToArray();

        return unpadded;
    }
}