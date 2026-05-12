// Copyright (c) Microsoft. All rights reserved.

using System.Globalization;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace GettingStarted;

public sealed class Step8_Pipelining(ITestOutputHelper output) : BaseTest(output)
{
    /// <summary>
    /// 提供一個範例，示範如何將多個函式組合成單一函式，
    /// 並依序呼叫，將前一個輸出作為下一個輸入。
    /// </summary>
    [Fact]
    public async Task CreateFunctionPipeline()
    {
        IKernelBuilder builder = Kernel.CreateBuilder();
        builder.AddAzureOpenAIChatClient(
                deploymentName: TestConfiguration.AzureOpenAI.DeploymentName,
                endpoint: TestConfiguration.AzureOpenAI.Endpoint,
                credentials: new DefaultAzureCredential());
        builder.Services.AddLogging(c => c.AddConsole().SetMinimumLevel(LogLevel.Trace));
        Kernel kernel = builder.Build();

        Console.WriteLine("================ PIPELINE ================");
        {
            // 建立一個函式管線：將字串解析為 double、乘上一個 double、截斷為 int，最後轉為自然語言描述。
            KernelFunction parseDouble = KernelFunctionFactory.CreateFromMethod((string s) => double.Parse(s, CultureInfo.InvariantCulture), "parseDouble");
            KernelFunction multiplyByN = KernelFunctionFactory.CreateFromMethod((double i, double n) => i * n, "multiplyByN");
            KernelFunction truncate = KernelFunctionFactory.CreateFromMethod((double d) => (int)d, "truncate");
            KernelFunction humanize = KernelFunctionFactory.CreateFromPrompt(new PromptTemplateConfig()
            {
                Template = "Spell out this number in English: {{$number}}",
                InputVariables = [new() { Name = "number" }],
            });
            KernelFunction pipeline = KernelFunctionCombinators.Pipe([parseDouble, multiplyByN, truncate, humanize], "pipeline");

            KernelArguments args = new()
            {
                ["s"] = "123.456",
                ["n"] = (double)78.90,
            };

            // - 會呼叫 parseInt32 函式，從參數讀取 "123.456"，並解析為 (double)123.456。
            // - 會呼叫 multiplyByN 函式，帶入 i=123.456 與 n=78.90，回傳 (double)9740.6784。
            // - 會呼叫 truncate 函式，帶入 d=9740.6784，回傳 (int)9740，作為最終結果。
            Console.WriteLine(await pipeline.InvokeAsync(kernel, args));
        }

        Console.WriteLine("================ GRAPH ================");
        {
            KernelFunction rand = KernelFunctionFactory.CreateFromMethod(() => Random.Shared.Next(), "GetRandomInt32");
            KernelFunction mult = KernelFunctionFactory.CreateFromMethod((int i, int j) => i * j, "Multiply");

            // - 呼叫 rand，並將隨機數存入 args["i"]
            // - 呼叫 rand，並將隨機數存入 args["j"]
            // - 將 arg["i"] 與 args["j"] 相乘，得到最終結果
            KernelFunction graph = KernelFunctionCombinators.Pipe(new[]
            {
                (rand, "i"),
                (rand, "j"),
                (mult, "")
            }, "graph");

            Console.WriteLine(await graph.InvokeAsync(kernel));
        }
    }
}

public static class KernelFunctionCombinators
{
    /// <summary>
    /// 呼叫函式管線，依序執行每個函式，並將前一個輸出作為下一個函式的第一個參數。
    /// </summary>
    /// <param name="functions">要呼叫的函式管線。</param>
    /// <param name="kernel">執行作業所使用的 Kernel。</param>
    /// <param name="arguments">參數集合。</param>
    /// <param name="cancellationToken">用於監控取消要求的取消權杖。</param>
    public static Task<FunctionResult> InvokePipelineAsync(
        IEnumerable<KernelFunction> functions, Kernel kernel, KernelArguments arguments, CancellationToken cancellationToken) =>
        Pipe(functions).InvokeAsync(kernel, arguments, cancellationToken);

    /// <summary>
    /// 呼叫函式管線，依序執行每個函式，並將前一個輸出以指定參數名稱傳給下一個函式。
    /// </summary>
    /// <param name="functions">要呼叫的函式序列，以及每次函式呼叫結果要指派的參數名稱。</param>
    /// <param name="kernel">執行作業所使用的 Kernel。</param>
    /// <param name="arguments">參數集合。</param>
    /// <param name="cancellationToken">用於監控取消要求的取消權杖。</param>
    public static Task<FunctionResult> InvokePipelineAsync(
        IEnumerable<(KernelFunction Function, string OutputVariable)> functions, Kernel kernel, KernelArguments arguments, CancellationToken cancellationToken) =>
        Pipe(functions).InvokeAsync(kernel, arguments, cancellationToken);

    /// <summary>
    /// 建立一個函式；呼叫此函式時會依序呼叫所有提供的函式。
    /// </summary>
    /// <param name="functions">要呼叫的函式管線。</param>
    /// <param name="functionName">組合後作業的名稱。</param>
    /// <param name="description">組合後作業的描述。</param>
    /// <returns>最後一個函式的結果。</returns>
    /// <remarks>
    /// 前一個函式的結果會餵入下一個函式的第一個參數。
    /// </remarks>
    public static KernelFunction Pipe(
        IEnumerable<KernelFunction> functions,
        string? functionName = null,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(functions);

        KernelFunction[] funcs = functions.ToArray();
        Array.ForEach(funcs, f => ArgumentNullException.ThrowIfNull(f));

        var funcsAndVars = new (KernelFunction Function, string OutputVariable)[funcs.Length];
        for (int i = 0; i < funcs.Length; i++)
        {
            string p = "";
            if (i < funcs.Length - 1)
            {
                var parameters = funcs[i + 1].Metadata.Parameters;
                if (parameters.Count > 0)
                {
                    p = parameters[0].Name;
                }
            }

            funcsAndVars[i] = (funcs[i], p);
        }

        return Pipe(funcsAndVars, functionName, description);
    }

    /// <summary>
    /// 建立一個函式；呼叫此函式時會依序呼叫所有提供的函式。
    /// </summary>
    /// <param name="functions">要呼叫的函式管線，以及每次函式呼叫結果要指派的參數名稱。</param>
    /// <param name="functionName">組合後作業的名稱。</param>
    /// <param name="description">組合後作業的描述。</param>
    /// <returns>最後一個函式的結果。</returns>
    /// <remarks>
    /// 前一個函式的結果會餵入下一個函式的第一個參數。
    /// </remarks>
    public static KernelFunction Pipe(
        IEnumerable<(KernelFunction Function, string OutputVariable)> functions,
        string? functionName = null,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(functions);

        (KernelFunction Function, string OutputVariable)[] arr = functions.ToArray();
        Array.ForEach(arr, f =>
        {
            ArgumentNullException.ThrowIfNull(f.Function);
            ArgumentNullException.ThrowIfNull(f.OutputVariable);
        });

        return KernelFunctionFactory.CreateFromMethod(async (Kernel kernel, KernelArguments arguments) =>
        {
            FunctionResult? result = null;
            for (int i = 0; i < arr.Length; i++)
            {
                result = await arr[i].Function.InvokeAsync(kernel, arguments).ConfigureAwait(false);
                if (i < arr.Length - 1)
                {
                    arguments[arr[i].OutputVariable] = result.GetValue<object>();
                }
            }

            return result;
        }, functionName, description);
    }
}
