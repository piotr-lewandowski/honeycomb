using System.Runtime.Intrinsics;

namespace Tests;

public record RandomInputData
{
    public RandomInputData(uint size)
    {
        InputData = GenerateTestData(size + 1);
    }
    public InputData InputData { get; }

    public InputData GenerateTestData(uint N)
    {
        var rand = new System.Random();
        var M = new Vector256<byte>[N];
        var AD = new Vector256<byte>[N];
        var C = new Vector256<byte>[N];
        for (int i = 0; i < N; i++)
        {
            M[i] = RandomWord();
            AD[i] = RandomWord();
        }

        var IV = RandomWord().GetLower();
        var K = RandomWord().GetLower();

        return new InputData(M, AD, C, K, IV);

        Vector256<byte> RandomWord()
        {
            var one = rand.NextInt64();
            var two = rand.NextInt64();
            var three = rand.NextInt64();
            var four = rand.NextInt64();

            return Vector256.Create(one, two, three, four).AsByte();
        }
    }

}
