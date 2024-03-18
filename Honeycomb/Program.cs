
using BenchmarkDotNet.Running;
using Benchmarks;
using System.CommandLine.Parsing;
using System.CommandLine;
using Honeycomb;
using System.Text.Json;
using System.Runtime.Intrinsics;

var fileArgument = new Argument<FileInfo>(
    name: "filepath",
    description: "File containing input parameters."
);

fileArgument.AddValidator(result =>
{
    var fileInfo = result.GetValueForArgument<FileInfo>(fileArgument);
    if (!fileInfo.Exists)
    {
        result.ErrorMessage = $"File {fileInfo.FullName} does not exist.";
    }
});

var modeOption = new Option<Modes>(
    new[] { "--mode", "-m" },
    description: "Whether to encode or decode.",
    getDefaultValue: () => Modes.Encode
);

var rootCommand = new RootCommand(
    "Implementation of the Honeycomb cypher."
) { fileArgument, modeOption };

rootCommand.SetHandler((file, mode) =>
{
    var jsonString = File.ReadAllText(file.FullName);
    JsonSerializerOptions options = new JsonSerializerOptions
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    if (mode == Modes.Encode)
    {
        var hexData = JsonSerializer.Deserialize<HexData>(jsonString);
        var inputData = hexData.ToInputData();
        var honeycomb = new OptimisedHoneycomb(inputData.K, inputData.IV);
        var outputBuffer = new Vector256<byte>[inputData.M.Length];
        var encoded = honeycomb.Encode(inputData.M, inputData.AD, outputBuffer);

        var outputData = new InputData(inputData.M, inputData.AD, encoded.C, inputData.K, inputData.IV);
        var hexOutputData = outputData.ToHexData() with { M = null };
        var outputFile = new FileInfo(Path.GetFileNameWithoutExtension(file.FullName) + ".out" + ".json");

        File.WriteAllText(outputFile.FullName, JsonSerializer.Serialize(hexOutputData, options));
    }
    else
    {
        var hexData = JsonSerializer.Deserialize<HexData>(jsonString);
        var inputData = hexData.ToInputData();
        var honeycomb = new OptimisedHoneycomb(inputData.K, inputData.IV);
        var outputBuffer = new Vector256<byte>[inputData.C.Length];
        var encoded = honeycomb.Decode(inputData.C, inputData.AD, outputBuffer);

        var outputData = new InputData(encoded.M, inputData.AD, inputData.C, inputData.K, inputData.IV);
        var hexOutputData = outputData.ToHexData() with { C = null };
        var outputFile = new FileInfo(Path.GetFileNameWithoutExtension(file.FullName) + ".out" + ".json");

        File.WriteAllText(outputFile.FullName, JsonSerializer.Serialize(hexOutputData, options));
    }

}, fileArgument, modeOption);

var benchmarkCommand = new Command("benchmark",
    "Runs a benchmark for the Honeycomb implementation."
)
{ };

benchmarkCommand.SetHandler(dir =>
{
    var summary = BenchmarkRunner.Run<EncryptBenchmark>();
});

rootCommand.Add(benchmarkCommand);

await rootCommand.InvokeAsync(args);

public enum Modes
{
    Encode, Decode
}