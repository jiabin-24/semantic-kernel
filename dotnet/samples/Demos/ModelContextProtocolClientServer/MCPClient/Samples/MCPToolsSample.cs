// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Client;

namespace MCPClient.Samples;

/// <summary>
/// 此範例示範如何在 Semantic Kernel 中使用 Model Context Protocol (MCP) 工具。
/// </summary>
internal sealed class MCPToolsSample : BaseSample
{
    /// <summary>
    /// 示範如何在 Semantic Kernel 中使用 MCP 工具。
    /// 此方法中的程式碼流程：
    /// 1. 建立 MCP 用戶端。
    /// 2. 取得 MCP 伺服器提供的工具清單。
    /// 3. 建立 Kernel，並將 MCP 工具註冊為 Kernel 函式。
    /// 4. 將提示詞連同以 Kernel 函式表示的 MCP 工具一併送到 AI 模型。
    /// 5. AI 模型呼叫 DateTimeUtils-GetCurrentDateTimeInUtc 函式，取得下一個函式所需的 UTC 目前時間。
    /// 6. AI 模型呼叫 WeatherUtils-GetWeatherForCity 函式，使用目前時間與從提示詞擷取的 `Boston` 參數取得天氣資訊。
    /// 7. 取得函式呼叫回傳的天氣資訊後，AI 模型回覆提示詞答案。
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine($"Running the {nameof(MCPToolsSample)} sample.");

        // 建立 MCP 用戶端
        await using IMcpClient mcpClient = await CreateMcpClientAsync();

        // 取得並顯示 MCP 伺服器提供的工具清單
        IList<McpClientTool> tools = await mcpClient.ListToolsAsync();
        DisplayTools(tools);

        // 建立 Kernel 並註冊 MCP 工具
        Kernel kernel = CreateKernelWithChatCompletionService();
        kernel.Plugins.AddFromFunctions("Tools", tools.Select(aiFunction => aiFunction.AsKernelFunction()));

        // 啟用自動函式呼叫
        OpenAIPromptExecutionSettings executionSettings = new()
        {
            Temperature = 0,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new() { RetainArgumentTypes = true })
        };

        string prompt = "What is the likely color of the sky in Boston today?";
        Console.WriteLine(prompt);

        // 使用 MCP 工具執行提示詞。AI 模型會自動呼叫適當的 MCP 工具來回答。
        FunctionResult result = await kernel.InvokePromptAsync(prompt, new(executionSettings));

        Console.WriteLine(result);
        Console.WriteLine();

        // 預期輸出：The likely color of the sky in Boston today is gray, as it is currently rainy.
    }
}
