// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace MCPClient.Samples;

/// <summary>
/// 示範如何在 Semantic Kernel 中使用 Model Context Protocol (MCP) 資源範本。
/// </summary>
internal sealed class MCPResourceTemplatesSample : BaseSample
{
    /// <summary>
    /// 示範如何在 Semantic Kernel 中使用 MCP 資源範本。
    /// 此方法中的程式碼流程：
    /// 1. 建立 MCP 用戶端。
    /// 2. 取得 MCP 伺服器提供的資源範本清單。
    /// 3. 從 `vectorStore://records/{prompt}` MCP 資源範本讀取與提示詞相關的記錄。
    /// 4. 將記錄加入聊天歷程，並要求 AI 模型說明 SK 是什麼。
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine($"Running the {nameof(MCPResourceTemplatesSample)} sample.");

        // 建立 MCP 用戶端
        await using IMcpClient mcpClient = await CreateMcpClientAsync();

        // 取得並顯示 MCP 伺服器提供的資源範本清單
        IList<McpClientResourceTemplate> resourceTemplates = await mcpClient.ListResourceTemplatesAsync();
        DisplayResourceTemplates(resourceTemplates);

        // 建立 Kernel
        Kernel kernel = CreateKernelWithChatCompletionService();

        // 啟用自動函式呼叫
        OpenAIPromptExecutionSettings executionSettings = new()
        {
            Temperature = 0,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new() { RetainArgumentTypes = true })
        };

        string prompt = "What is the Semantic Kernel?";

        // 透過 MCP 資源範本取得與提示詞相關的記錄
        ReadResourceResult resource = await mcpClient.ReadResourceAsync($"vectorStore://records/{prompt}");

        // 將資源內容／記錄加入聊天歷程，並要求 AI 模型說明 SK 是什麼
        ChatHistory chatHistory = [];
        chatHistory.AddUserMessage(resource.ToChatMessageContentItemCollection());
        chatHistory.AddUserMessage(prompt);

        // 使用加入聊天歷程的 MCP 資源與提示詞執行對話
        IChatCompletionService chatCompletion = kernel.GetRequiredService<IChatCompletionService>();

        ChatMessageContent result = await chatCompletion.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);

        Console.WriteLine(result);
        Console.WriteLine();

        // 預期輸出：The Semantic Kernel (SK) is a lightweight software development kit (SDK) designed for use in .NET applications.
        // It acts as an orchestrator that facilitates interaction between AI models and available plugins, enabling them to work together to produce desired outputs.
    }

    /// <summary>
    /// 顯示 MCP 伺服器提供的資源範本清單。
    /// </summary>
    /// <param name="resourceTemplates">要顯示的資源範本清單。</param>
    private static void DisplayResourceTemplates(IList<McpClientResourceTemplate> resourceTemplates)
    {
        Console.WriteLine("Available MCP resource templates:");
        foreach (var template in resourceTemplates)
        {
            Console.WriteLine($"- Name: {template.Name}, Description: {template.Description}");
        }
        Console.WriteLine();
    }
}
