// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Azure.Identity;

namespace GettingStarted;

/// <summary>
/// 此範例示範如何使用 ChatClient 建立並使用 <see cref="Kernel"/>。
/// </summary>
public sealed class Step1_Create_Kernel(ITestOutputHelper output) : BaseTest(output)
{
    /// <summary>
    /// 示範如何使用 ChatClient 建立 <see cref="Kernel"/> 並執行提示詞。
    /// </summary>
    [Fact]
    public async Task CreateKernel()
    {
        // 使用 ChatClient 建立具備 Azure OpenAI 聊天補全能力的 Kernel
        Kernel kernel = Kernel.CreateBuilder()
            .AddAzureOpenAIChatClient(
                deploymentName: TestConfiguration.AzureOpenAI.DeploymentName,
                endpoint: TestConfiguration.AzureOpenAI.Endpoint,
                //apiKey: TestConfiguration.AzureOpenAI.ApiKey,
                credentials: new DefaultAzureCredential())
            .Build();

        // 範例 1：以提示詞呼叫 Kernel 並顯示結果
        Console.WriteLine(await kernel.InvokePromptAsync("What color is the sky?"));
        Console.WriteLine();

        // 範例 2：以範本提示詞呼叫 Kernel 並顯示結果
        KernelArguments arguments = new() { { "topic", "sea" } };
        Console.WriteLine(await kernel.InvokePromptAsync("What color is the {{$topic}}?", arguments));
        Console.WriteLine();

        // 範例 3：以範本提示詞呼叫 Kernel，並以串流方式輸出結果
        await foreach (var update in kernel.InvokePromptStreamingAsync("What color is the {{$topic}}? Provide a detailed explanation.", arguments))
        {
            Console.Write(update);
        }

        Console.WriteLine(string.Empty);

        // 範例 4：以範本提示詞與執行設定呼叫 Kernel
        arguments = new(new OpenAIPromptExecutionSettings { MaxTokens = 500, Temperature = 0.5 }) { { "topic", "dogs" } };
        Console.WriteLine(await kernel.InvokePromptAsync("Tell me a story about {{$topic}}", arguments));

        // 範例 5：以範本提示詞呼叫 Kernel，並設定執行參數以回傳 JSON
#pragma warning disable SKEXP0010
        arguments = new(new OpenAIPromptExecutionSettings { ResponseFormat = "json_object" }) { { "topic", "chocolate" } };
        Console.WriteLine(await kernel.InvokePromptAsync("Create a recipe for a {{$topic}} cake in JSON format", arguments));
    }
}
