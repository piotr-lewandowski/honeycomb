using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;

namespace Honeycomb;

public class NaiveHoneycomb
{
    private Vector128<byte>[] T3;
    private Vector128<byte>[] T4;
    private Vector128<byte>[] T6;
    private Vector128<byte> Z0 = Vector128.Create(0x428A2F98D728AE22, 0x7137449123EF65CD).AsByte();
    private Vector128<byte> Z1 = Vector128.Create(0xB5C0FBCFEC4D3B2F, 0xE9B5DBA58189DBBC).AsByte();
    private Vector128<byte> K;
    private Vector128<byte> IV;

    public NaiveHoneycomb(Vector128<byte> k, Vector128<byte> iv)
    {
        T3 = new Vector128<byte>[3];
        T4 = new Vector128<byte>[4];
        T6 = new Vector128<byte>[6];
        K = k;
        IV = iv;
    }

    public void Initialize(Vector128<byte> K, Vector128<byte> IV)
    {
        T3[0] = K;
        T3[1] = K;
        T3[2] = IV;

        T4[0] = K;
        T4[1] = K;
        T4[2] = IV;
        T4[3] = Z0;

        T6[0] = K;
        T6[1] = K;
        T6[2] = IV;
        T6[3] = Z1;
        T6[4] = Vector128<byte>.Zero;
        T6[5] = Vector128<byte>.Zero;

        for (int i = 0; i < 15; i++)
        {
            Update(Z0, Z1, Z0);
        }
    }

    private void ProcessAssociatedData(Vector256<byte>[] AD)
    {
        foreach (Vector256<byte> d in AD)
        {
            var d1 = d.GetLower();
            var d2 = d.GetUpper();
            Update(d1, d2, d1 ^ d2);
        }
    }

    public (Vector256<byte>[] C, Vector128<byte> T) Encode(Vector256<byte>[] M, Vector256<byte>[] AD)
    {
        Initialize(K, IV);
        ProcessAssociatedData(AD);

        var C = new Vector256<byte>[M.Length];
        for (var i = 0; i < M.Length; i++)
        {
            var m1 = M[i].GetLower();
            var m2 = M[i].GetUpper();
            Update(m1, m2, m1 ^ m2);

            var c1 = T3[0] ^ T3[2] ^ T4[1] ^ (T6[3] & T4[3]);
            var c2 = T6[0] ^ T4[2] ^ T3[1] ^ (T6[5] & T3[2]);

            var c = Vector256.Create(c1, c2);
            C[i] = c;
        }

        var T = MakeTag(AD, M);

        return (C, T);
    }

    public Vector128<byte> MakeTag(Vector256<byte>[] AD, Vector256<byte>[] M)
    {
        Update(Vector128.Create(0, AD.Length).AsByte(), Vector128.Create(0, M.Length).AsByte(), Vector128.Create(0, AD.Length ^ M.Length).AsByte());

        for (int i = 0; i < 20; i++)
        {
            Update(Z1, Z0, Z1);
        }

        return
            T3[0]
            ^ T3[1]
            ^ T3[2]
            ^ T4[0]
            ^ T4[1]
            ^ T4[2]
            ^ T4[3]
            ^ T6[0]
            ^ T6[1]
            ^ T6[2]
            ^ T6[3]
            ^ T6[4]
            ^ T6[5];
    }

    public (Vector256<byte>[] M, Vector128<byte> T) Decode(Vector256<byte>[] C, Vector256<byte>[] AD)
    {
        Initialize(K, IV);
        ProcessAssociatedData(AD);

        var M = new Vector256<byte>[C.Length];
        for (var i = 0; i < C.Length; ++i)
        {
            Update(Vector128<byte>.Zero, Vector128<byte>.Zero, Vector128<byte>.Zero);
            var c1 = C[i].GetLower();
            var c2 = C[i].GetUpper();

            var m1 = c1 ^ T3[0] ^ T3[2] ^ T4[1] ^ (T6[3] & T4[3]);
            var m2 = c2 ^ T6[0] ^ T4[2] ^ T3[1] ^ (T6[5] & T3[2]) ^ m1;

            T3[0] = T3[0] ^ m1;
            T4[0] = T4[0] ^ m2;
            T6[0] = T6[0] ^ m1 ^ m2;

            var m = Vector256.Create(m1, m2);
            M[i] = m;
        }

        var T = MakeTag(AD, M);

        return (M.ToArray(), T);
    }


    private Vector128<byte>[] Round(Vector128<byte>[] T, Vector128<byte> M)
    {
        int s = T.Length;
        Vector128<byte>[] T_new = new Vector128<byte>[s];
        T_new[0] = Aes.Encrypt(T[s - 1], T[0]) ^ M;
        T_new[1] = Aes.Encrypt(T[0], Z0);
        for (int i = 2; i < s; i++)
        {
            T_new[i] = T[i - 1];
        }
        return T_new;
    }

    public void Update(Vector128<byte> m0, Vector128<byte> m1, Vector128<byte> m2)
    {
        T3 = Round(T3, m0);
        T4 = Round(T4, m1);
        T6 = Round(T6, m2);
    }
}