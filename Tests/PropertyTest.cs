using System.Runtime.Intrinsics;
using System.Text.Json;
namespace Tests;

public partial class PropertyTest
{

    [Property]
    public Property Decode_Dot_Encode_Eq_Id(RandomInputData inputData)
    {
        var input = inputData.InputData;
        var honeycomb = new OptimisedHoneycomb(input.K, input.IV);
        var encoded = honeycomb.Encode(input.M, input.AD, input.C);
        var outputBuffer = new Vector256<byte>[input.M.Length];
        var decoded = honeycomb.Decode(encoded.C, input.AD, outputBuffer);

        var json = JsonSerializer.Serialize(input);
        var path = "input.json";
        File.WriteAllText(path, json);

        return input.M.Zip(decoded.M).All(pair => pair.First == pair.Second).And(encoded.T == decoded.T);
    }
}