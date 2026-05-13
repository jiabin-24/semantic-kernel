// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Client;

namespace MCPClient.Samples;

/// <summary>
/// 示範如何使用 <see cref="ChatCompletionAgent"/> 搭配以 Kernel 函式表示的 MCP 工具。
/// </summary>
internal sealed class ChatCompletionAgentWithMCPToolsSample : BaseSample
{
    /// <summary>
    /// 示範如何使用 <see cref="ChatCompletionAgent"/> 搭配以 Kernel 函式表示的 MCP 工具。
    /// 此方法中的程式碼流程：
    /// 1. 建立 MCP 用戶端。
    /// 2. 取得 MCP 伺服器提供的工具清單。
    /// 3. 建立 Kernel，並將 MCP 工具註冊為 Kernel 函式。
    /// 4. 定義聊天補全 Agent（含指示、名稱、Kernel 與參數）。
    /// 5. 以提示詞呼叫 Agent。
    /// 6. Agent 將提示詞與以 Kernel 函式表示的 MCP 工具一併送到 AI 模型。
    /// 7. AI 模型呼叫 DateTimeUtils-GetCurrentDateTimeInUtc 函式，取得下一個函式所需的 UTC 目前時間。
    /// 8. AI 模型呼叫 WeatherUtils-GetWeatherForCity 函式，使用目前時間與從提示詞擷取的 `Boston` 參數取得天氣資訊。
    /// 9. AI 模型收到函式回傳的天氣資訊後回覆 Agent，再由 Agent 回覆使用者。
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine($"Running the {nameof(ChatCompletionAgentWithMCPToolsSample)} sample.");

        // 建立 MCP 用戶端
        await using IMcpClient mcpClient = await CreateMcpClientAsync();

        // 取得並顯示 MCP 伺服器提供的工具清單
        IList<McpClientTool> tools = await mcpClient.ListToolsAsync();
        DisplayTools(tools);

        // 建立 Kernel 並將 MCP 工具註冊為 Kernel 函式
        Kernel kernel = CreateKernelWithChatCompletionService();
        kernel.Plugins.AddFromFunctions("Tools", tools.Select(aiFunction => aiFunction.AsKernelFunction()));

        // 啟用自動函式呼叫
        OpenAIPromptExecutionSettings executionSettings = new()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new() { RetainArgumentTypes = true })
        };

        string prompt = "What is the likely color of the sky in Boston today?";
        Console.WriteLine(prompt);

        // 定義 Agent
        ChatCompletionAgent agent = new()
        {
            Instructions = "Answer questions about the weather.",
            Name = "WeatherAgent",
            Kernel = kernel,
            Arguments = new KernelArguments(executionSettings),
        };

        // 以提示詞呼叫 Agent
        ChatMessageContent response = await agent.InvokeAsync(prompt).FirstAsync();

        Console.WriteLine(response);
        Console.WriteLine();

        // 預期輸出：The sky in Boston today is likely gray due to rainy weather.
    }
}
