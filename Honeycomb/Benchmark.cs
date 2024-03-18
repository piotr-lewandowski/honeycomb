using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using Honeycomb;

namespace Benchmarks;

[MemoryDiagnoser]
public class EncryptBenchmark
{
    [Params(1024 / 32, 1024 * 1024 / 32, 1024 * 1024 * 1024 / 32)]
    public int N { get; set; }
    private Vector256<byte>[] M;
    private Vector256<byte>[] AD;
    private Vector256<byte>[] C;

    private byte[] mBytes;
    private byte[] adBytes;
    private byte[] keyBytes;
    private byte[] ivBytes;

    private byte[] cipherBytes;
    private byte[] tagBytes;

    private Aes aes;
    private AesGcm aesGcm;
    private OptimisedHoneycomb optimisedHoneycomb;
    private NaiveHoneycomb naiveHoneycomb;

    private Random rand = new Random();

    [GlobalSetup]
    public void Setup()
    {
        M = new Vector256<byte>[N];
        AD = new Vector256<byte>[N];
        C = new Vector256<byte>[N];
        mBytes = new byte[N * 32];
        adBytes = new byte[N * 32];
        for (int i = 0; i < N; i++)
        {
            M[i] = RandomWord();
            AD[i] = RandomWord();
            M[i].AsByte().CopyTo(mBytes, i * 32);
            AD[i].AsByte().CopyTo(adBytes, i * 32);
        }

        var IV = RandomWord().GetLower();
        var K = RandomWord().GetLower();
        keyBytes = new byte[16];
        K.AsByte().CopyTo(keyBytes, 0);

        ivBytes = new byte[12]; // aesGcm only supports 12 byte IVs
        var tmp = new byte[16];
        IV.AsByte().CopyTo(tmp, 0);
        ivBytes = tmp[0..12];

        cipherBytes = new byte[N * 32];
        tagBytes = new byte[16];

        optimisedHoneycomb = new OptimisedHoneycomb(K, IV);
        naiveHoneycomb = new NaiveHoneycomb(K, IV);
        aesGcm = new AesGcm(keyBytes);
        aes = Aes.Create();
        aes.Key = keyBytes;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

    }

    public EncryptBenchmark()
    {
    }

    [Benchmark]
    public Vector256<byte>[] EncryptOptimised() => optimisedHoneycomb.Encode(M, AD, C).C;

    [Benchmark]
    public Vector256<byte>[] Encrypt() => naiveHoneycomb.Encode(M, AD).C;

    [Benchmark(Baseline = true)]
    public void EncryptAes() => aes.EncryptEcb(mBytes, cipherBytes, PaddingMode.None);

    [Benchmark]
    public void EncryptAesGcm() => aesGcm.Encrypt(ivBytes, mBytes, cipherBytes, tagBytes, adBytes);

    private Vector256<byte> RandomWord()
    {
        var one = rand.NextInt64();
        var two = rand.NextInt64();
        var three = rand.NextInt64();
        var four = rand.NextInt64();

        return Vector256.Create(one, two, three, four).AsByte();
    }
}
