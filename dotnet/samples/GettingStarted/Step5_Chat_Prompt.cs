// Copyright (c) Microsoft. All rights reserved.

using Azure.Identity;
using Microsoft.SemanticKernel;

namespace GettingStarted;

public sealed class Step5_Chat_Prompt(ITestOutputHelper output) : BaseTest(output)
{
    /// <summary>
    /// 示範如何建構聊天提示詞並呼叫它。
    /// </summary>
    [Fact]
    public async Task InvokeChatPrompt()
    {
        // 建立具備 OpenAI 聊天補全能力的 Kernel
        Kernel kernel = Kernel.CreateBuilder()
            .AddAzureOpenAIChatClient(
                deploymentName: TestConfiguration.AzureOpenAI.DeploymentName,
                endpoint: TestConfiguration.AzureOpenAI.Endpoint,
                credentials: new DefaultAzureCredential())
            .Build();

        // 以聊天提示詞呼叫 Kernel 並顯示結果
        string chatPrompt = """
            <message role="user">What is Seattle?</message>
            <message role="system">Respond with JSON.</message>
            """;

        Console.WriteLine(await kernel.InvokePromptAsync(chatPrompt));
    }
}
