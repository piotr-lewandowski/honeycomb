using System.Runtime.Intrinsics;

namespace Honeycomb;

public record InputData(
    Vector256<byte>[] M,
    Vector256<byte>[] AD,
    Vector256<byte>[] C,
    Vector128<byte> K,
    Vector128<byte> IV
)
{
    public HexData ToHexData()
    {
        return new HexData(
            M?.Select(v => Vector256ToString(v)).Aggregate((a, b) => a + b),
            AD.Select(v => Vector256ToString(v)).Aggregate((a, b) => a + b),
            C?.Select(v => Vector256ToString(v)).Aggregate((a, b) => a + b),
            Vector128ToString(K),
            Vector128ToString(IV)
        );
    }

    public static string Vector256ToString(Vector256<byte> v)
    {
        var bytes = new byte[32];
        v.AsByte().CopyTo(bytes);
        return Convert.ToHexString(bytes);
    }

    public static string Vector128ToString(Vector128<byte> v)
    {
        var bytes = new byte[16];
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
)
{
    public InputData ToInputData()
    {
        var m = M?.Chunk(64).Select(s => StringToVector256(new string(s))).ToArray();
        var ad = AD.Chunk(64).Select(s => StringToVector256(new string(s))).ToArray();
        var c = C?.Chunk(64).Select(s => StringToVector256(new string(s))).ToArray();
        var k = StringToVector128(K);
        var iv = StringToVector128(IV);
        return new InputData(m, ad, c, k, iv);
    }

    public static byte[] StringToByteArray(string hex)
    {
        var bytes = hex.Chunk(2).Select(s => Convert.ToByte(new string(s), 16)).ToArray();
        return Convert.FromHexString(hex);
    }

    public static Vector128<byte> StringToVector128(string hex)
    {
        var bytes = StringToByteArray(hex.PadRight(32));
        return Vector128.Create(bytes);
    }

    public static Vector256<byte> StringToVector256(string hex)
    {
        var bytes = StringToByteArray(hex.PadRight(64));
        return Vector256.Create(bytes);
    }
}