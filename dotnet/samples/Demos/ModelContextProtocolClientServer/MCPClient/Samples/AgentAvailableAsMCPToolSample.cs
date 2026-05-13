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
/// 示範如何使用可作為 MCP 工具的 SK Agent。
/// </summary>
internal sealed class AgentAvailableAsMCPToolSample : BaseSample
{
    /// <summary>
    /// 示範如何使用可作為 MCP 工具的 SK Agent。
    /// 此方法中的程式碼流程：
    /// 1. 建立 MCP 用戶端。
    /// 2. 取得 MCP 伺服器提供的工具清單。
    /// 3. 建立 Kernel，並將 MCP 工具註冊為 Kernel 函式。
    /// 4. 將提示詞連同以 Kernel 函式表示的 MCP 工具一併送到 AI 模型。
    /// 5. AI 模型呼叫 `Agents_SalesAssistant` 函式，該函式會呼叫 MCP 工具，再由工具呼叫伺服器上的 SK Agent。
    /// 6. Agent 呼叫 `OrderProcessingUtils-PlaceOrder` 函式，為 `Grande Mug` 下訂單。
    /// 7. Agent 呼叫 `OrderProcessingUtils-ReturnOrder` 函式，退回 `Wide Rim Mug`。
    /// 8. Agent 彙整交易內容，並將結果作為 `Agents_SalesAssistant` 函式呼叫的一部分回傳。
    /// 9. AI 模型收到 `Agents_SalesAssistant` 的結果後，回覆提示詞答案。
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine($"Running the {nameof(AgentAvailableAsMCPToolSample)} sample.");

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

        string prompt = "I'd like to order the 'Grande Mug' and return the 'Wide Rim Mug' bought last week.";
        Console.WriteLine(prompt);

        // 使用 MCP 工具執行提示詞。AI 模型會自動呼叫適當的 MCP 工具來回答。
        FunctionResult result = await kernel.InvokePromptAsync(prompt, new(executionSettings));

        Console.WriteLine(result);
        Console.WriteLine();

        // 預期輸出：The order for the "Grande Mug" has been successfully placed.
        // Additionally, the return process for the "Wide Rim Mug" has been successfully initiated.
        // If you have any further questions or need assistance with anything else, feel free to ask!
    }
}
