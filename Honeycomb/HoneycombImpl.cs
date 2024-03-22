using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;

namespace Honeycomb;

public class HoneycombImpl
{
    private Vector64<byte>[] T0;
    private Vector64<byte>[] T1;
    private Vector64<byte>[] T2;
    private Vector64<byte>[] T3;
    private Vector64<byte> Z0 = Vector64.AsByte(Vector64.Create(0x428A2F98D728AE22));
    private Vector64<byte> Z1 = Vector64.AsByte(Vector64.Create(0xE9B5DBA58189DBBC));
    private Vector64<byte> Z2 = Vector64.AsByte(Vector64.Create(0xB5C0FBCFEC4D3B2F));
    private Vector64<byte> K;
    private Vector64<byte> IV;

    public HoneycombImpl(Vector64<byte> k, Vector64<byte> iv)
    {
        T0 = new Vector64<byte>[6];
        T1 = new Vector64<byte>[6];
        T2 = new Vector64<byte>[6];
        T3 = new Vector64<byte>[6];
        K = k;
        IV = iv;
    }

    public void Initialize(Vector64<byte> K, Vector64<byte> IV)
    {
        T0[0] = Vector64<byte>.Zero;
        T0[1] = Z2; 
        T0[2] = Z0;
        T0[3] = K;
        T0[4] = IV;
        T0[5] = Z1;

        T1[0] = Z1;
        T1[1] = Z2;
        T1[2] = K;
        T1[3] = IV;
        T1[4] = Vector64<byte>.Zero;
        T1[5] = Z0;

        T2[0] = Z2;
        T2[1] = Vector64<byte>.Zero;
        T2[2] = IV;
        T2[3] = Z1;
        T2[4] = Z0;
        T2[5] = K;

        T3[0] = K;
        T3[1] = IV;
        T3[2] = Z2;
        T3[3] = Vector64<byte>.Zero;
        T3[4] = Z0;
        T3[5] = Z1;

        for (int i = 0; i < 15; i++)
        {
            Update(
                Vector128.Create(Vector64<byte>.Zero, Z0),
                Vector128.Create(Z0, Z1), 
                Vector128.Create(Z1, Z2), 
                Vector128.Create(Z2, Vector64<byte>.Zero)
            );
        }
    }

    private void ProcessAssociatedData(Vector256<byte>[] AD)
    {
        foreach (Vector256<byte> d in AD)
        {
            var d1 = d.GetLower();
            var d2 = d.GetUpper();
            Update(Vector128<byte>.Zero, d1, d2, Vector128<byte>.Zero);
        }
    }

    public (Vector256<byte>[] C, UInt16 T) Encode(Vector256<byte>[] M, Vector256<byte>[] AD, Vector256<byte>[] C)
    {
        Initialize(K, IV);
        ProcessAssociatedData(AD);

        for (var i = 0; i < M.Length; i++)
        {
            var m1 = M[i].GetLower();
            var m2 = M[i].GetUpper();
            Update(Vector128<byte>.Zero, m1, m2, Vector128<byte>.Zero);

            var c0 = T0[4] ^ (T0[0] & T1[5]);
            var c1 = T2[1] ^ T3[5];
            var c2 = T1[4] ^ (T1[1] & T2[2]);
            var c3 = T3[1] ^ T3[3];

            var c = Vector256.Create(Vector128.Create(c0, c1), Vector128.Create(c2, c3));
            C[i] = c;
        }

        var T = MakeTag(AD, M);

        return (C, T);
    }

    public UInt16 MakeTag(Vector256<byte>[] AD, Vector256<byte>[] M)
    {
        Update(Vector128<byte>.Zero, Vector128.Create(0, AD.Length).AsByte(), Vector128.Create(0, M.Length).AsByte(), Vector128<byte>.Zero);

        for (int i = 0; i < 20; i++)
        {
            Update(
                Vector128.Create(Vector64<byte>.Zero, Z2),
                Vector128.Create(Z2, Z1), 
                Vector128.Create(Z1, Z0), 
                Vector128.Create(Z0, Vector64<byte>.Zero)
            );
        }

        var tag = T0[0] ^ T2[0] ^ T0[2] ^ T2[2] ^ T0[4] ^ T2[4] ^ T1[1] ^ T3[1] ^ T1[3] ^ T3[3] ^ T1[5] ^ T3[5];

        Vector64<UInt16> tag16 = Vector64.AsUInt16(tag);
        UInt16 result = (UInt16)(tag16[0] ^ tag16[1]);

        return result;
    }

    public (Vector256<byte>[] M, UInt16 T) Decode(Vector256<byte>[] C, Vector256<byte>[] AD, Vector256<byte>[] M)
    {
        Initialize(K, IV);
        ProcessAssociatedData(AD);

        for (var i = 0; i < C.Length; ++i)
        {
            Update(Vector128<byte>.Zero, Vector128<byte>.Zero, Vector128<byte>.Zero, Vector128<byte>.Zero);
            var c0 = C[i].GetLower().GetLower();
            var c1 = C[i].GetLower().GetUpper();
            var c2 = C[i].GetUpper().GetLower();
            var c3 = C[i].GetUpper().GetUpper();

            var m0 = c0 ^ T0[4] ^ (T0[0] & T1[5]); 
            var m1 = c1 ^ T2[1] ^ T3[5];
            var m2 = c2 ^ T1[4] ^ (T1[1] & T2[2]);
            var m3 = c3 ^ T3[1] ^ T3[3];

            T0[4] = T0[4] ^ m0;
            T1[4] = T1[4] ^ m2;
            T2[1] = T2[1] ^ m1;
            T3[1] = T3[1] ^ m3;

            var m = Vector256.Create(Vector128.Create(m0, m1), Vector128.Create(m2, m3));
            M[i] = m;
        }

        var T = MakeTag(AD, M);

        return (M.ToArray(), T);
    }


    public void Update(Vector128<byte> m0, Vector128<byte> m1, Vector128<byte> m2, Vector128<byte> m3)
    {
        (T0[4], T1[1]) = Mix(T0[4], T1[1], m0);
        (T0[3], T2[0]) = Mix(T0[3], T2[0], m1);
        (T1[3], T3[0]) = Mix(T1[3], T3[0], m2);
        (T2[4], T3[1]) = Mix(T2[4], T3[1], m3);

        Rotate(T0);
        Rotate(T1);
        Rotate(T2);
        Rotate(T3);
    }

    public (Vector64<byte> Y1, Vector64<byte> Y2) Mix(Vector64<byte> X1, Vector64<byte> X2, Vector128<byte> SK)
    {
        var x = Vector128.Create(X1, X2);
        var y = Aes.Encrypt(x, SK);

        return (y.GetLower(), y.GetUpper());
    }

    public void Rotate(Vector64<byte>[] T)
    {
        var tmp = T[T.Length - 1];
        for(int i=T.Length - 1; i>0; --i)
        {
            T[i] = T[i-1];
        }
        T[0] = tmp;
    }
}