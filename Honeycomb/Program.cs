
using BenchmarkDotNet.Running;
using Benchmarks;
using System.CommandLine.Parsing;
using System.CommandLine;
using Honeycomb;
using System.Text.Json;
using System.Runtime.Intrinsics;
using System.Text;

Console.WriteLine();

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
    // var jsonString = File.ReadAllText(file.FullName);
    // JsonSerializerOptions options = new JsonSerializerOptions
    // {
    //     DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    // };

    // if (mode == Modes.Encode)
    // {
    //     var hexData = JsonSerializer.Deserialize<HexData>(jsonString);
    //     var inputData = hexData.ToInputData();
    //     var honeycomb = new HoneycombImpl(inputData.K, inputData.IV);
    //     var outputBuffer = new Vector256<byte>[inputData.M.Length];
    //     var encoded = honeycomb.Encode(inputData.M, inputData.AD, outputBuffer);

    //     var outputData = new InputData(inputData.M, inputData.AD, encoded.C, inputData.K, inputData.IV);
    //     var hexOutputData = outputData.ToHexData() with { M = null };
    //     var outputFile = new FileInfo(Path.GetFileNameWithoutExtension(file.FullName) + ".out" + ".json");

    //     File.WriteAllText(outputFile.FullName, JsonSerializer.Serialize(hexOutputData, options));
    // }
    // else
    // {
    //     var hexData = JsonSerializer.Deserialize<HexData>(jsonString);
    //     var inputData = hexData.ToInputData();
    //     var honeycomb = new HoneycombImpl(inputData.K, inputData.IV);
    //     var outputBuffer = new Vector256<byte>[inputData.C.Length];
    //     var encoded = honeycomb.Decode(inputData.C, inputData.AD, outputBuffer);

    //     var outputData = new InputData(encoded.M, inputData.AD, inputData.C, inputData.K, inputData.IV);
    //     var hexOutputData = outputData.ToHexData() with { C = null };
    //     var outputFile = new FileInfo(Path.GetFileNameWithoutExtension(file.FullName) + ".out" + ".json");

    //     File.WriteAllText(outputFile.FullName, JsonSerializer.Serialize(hexOutputData, options));
    // }

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

var interactiveCommand = new Command("interactive",
    "Allows for encoding of a message typed by the user"
)
{ };

interactiveCommand.SetHandler(dir =>
{
    var padder = new Padding();
    var k = Vector64.AsByte(Vector64.Create(0x428A2F98D728AE22));
    var iv = Vector64.AsByte(Vector64.Create(0xE9B5DBA58189DBBC));
    var cipher = new HoneycombImpl(k, iv);

    var mode = ReadMode();

    if (mode == Modes.Encode)
    {
        var (msg, len) = ReadMessage();
        var ad = ReadAd();
        var outBuffer = new Vector256<byte>[msg.Length];

        var (output, tag) = cipher.Encode(msg, ad, outBuffer);
        var outputBytes = output.SelectMany(v => {
            var b = new byte[32];
            v.AsByte().CopyTo(b);
            return b;
        }).Take(len).ToArray();
        var outputStr = Convert.ToHexString(outputBytes);

        Console.WriteLine("Result:");
        Console.WriteLine(outputStr);
        Console.WriteLine("Tag:");
        Console.WriteLine(tag);
    }
    else
    {
        var (ctxt, len) = ReadCiphertext();
        var ad = ReadAd();
        var outBuffer = new Vector256<byte>[ctxt.Length];
        var padLen = ctxt.Length * 32 - len;

        var (output, tag) = cipher.Decode(ctxt, ad, outBuffer, padLen);
        var outputBytes = padder.Unpad(output, padLen);
        var outputStr = Encoding.UTF8.GetString(outputBytes);

        Console.WriteLine("Result:");
        Console.WriteLine(outputStr);
        Console.WriteLine("Tag:");
        Console.WriteLine(tag);
    }


    Modes ReadMode()
    {
        Modes? mode = null;

        while(mode is null)
        {
            Console.WriteLine("Do you want to encrypt or decrypt? (e/d)");

            var resp = Console.ReadLine();

            if(resp == "e" || resp == "E" || resp == "encrypt")
                mode = Modes.Encode;
            else if(resp == "d" || resp == "D" || resp == "decrypt")
                mode = Modes.Decode;
        }

        return mode.Value;
    }

    (Vector256<byte>[], int) ReadMessage()
    {
        Console.WriteLine("Input the message you want to encrypt:");

        var text = Console.ReadLine();
        var bytes = Encoding.UTF8.GetBytes(text);

        return (padder.Pad(bytes), bytes.Length);
    }

    (Vector256<byte>[], int) ReadCiphertext()
    {
        Console.WriteLine("Input the ciphertext to decrypt:");

        var text = Console.ReadLine();
        var bytes = Convert.FromHexString(text);
        var padded = padder.Pad(bytes);

        return (padded, bytes.Length);
    }
    
    Vector256<byte>[] ReadAd()
    {
        Console.WriteLine("Input associated data:");

        var text = Console.ReadLine();
        var bytes = Encoding.UTF8.GetBytes(text);

        return padder.Pad(bytes);
    }
});

rootCommand.Add(interactiveCommand);

await rootCommand.InvokeAsync(args);

public enum Modes
{
    Encode, Decode
}