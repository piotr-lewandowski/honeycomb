using System.Runtime.Intrinsics;

namespace Honeycomb;

public record InputData(
    Vector256<byte>[] M,
    Vector256<byte>[] AD,
    Vector256<byte>[] C,
    Vector64<byte> K,
    Vector64<byte> IV
)
{
    public HexData ToHexData()
    {
        return new HexData(
            M?.Select(v => Vector256ToString(v)).Aggregate((a, b) => a + b),
            AD.Select(v => Vector256ToString(v)).Aggregate((a, b) => a + b),
            C?.Select(v => Vector256ToString(v)).Aggregate((a, b) => a + b),
            Vector64ToString(K),
            Vector64ToString(IV)
        );
    }

    public static string Vector256ToString(Vector256<byte> v)
    {
        var bytes = new byte[32];
        v.AsByte().CopyTo(bytes);
        return Convert.ToHexString(bytes);
    }

    public static string Vector64ToString(Vector64<byte> v)
    {
        var bytes = new byte[8];
        v.AsByte().CopyTo(bytes);
        return Convert.ToHexString(bytes);
    }
}

public record HexData(
    string M,
    string AD,
    string C,
    string K,
    string IV
) {}