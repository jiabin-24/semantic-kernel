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
/// 示範如何在 Semantic Kernel 中使用 Model Context Protocol (MCP) 資源。
/// </summary>
internal sealed class MCPResourcesSample : BaseSample
{
    /// <summary>
    /// 示範如何在 Semantic Kernel 中使用 MCP 資源。
    /// 此方法中的程式碼流程：
    /// 1. 建立 MCP 用戶端。
    /// 2. 取得 MCP 伺服器提供的資源清單。
    /// 3. 從 MCP 伺服器取得 `image://cat.jpg` 資源內容。
    /// 4. 將影像加入聊天歷程，並要求 AI 模型描述影像內容。
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine($"Running the {nameof(MCPResourcesSample)} sample.");

        // 建立 MCP 用戶端
        await using IMcpClient mcpClient = await CreateMcpClientAsync();

        // 取得並顯示 MCP 伺服器提供的資源清單
        IList<McpClientResource> resources = await mcpClient.ListResourcesAsync();
        DisplayResources(resources);

        // 建立 Kernel
        Kernel kernel = CreateKernelWithChatCompletionService();

        // 啟用自動函式呼叫
        OpenAIPromptExecutionSettings executionSettings = new()
        {
            Temperature = 0,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new() { RetainArgumentTypes = true })
        };

        // 從 MCP 伺服器取得 `image://cat.jpg` 資源
        ReadResourceResult resource = await mcpClient.ReadResourceAsync("image://cat.jpg");

        // 將資源加入聊天歷程，並提示 AI 模型描述影像內容
        ChatHistory chatHistory = [];
        chatHistory.AddUserMessage(resource.ToChatMessageContentItemCollection());
        chatHistory.AddUserMessage("Describe the content of the image?");

        // 使用加入聊天歷程的 MCP 資源與提示詞執行對話
        IChatCompletionService chatCompletion = kernel.GetRequiredService<IChatCompletionService>();

        ChatMessageContent result = await chatCompletion.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);

        Console.WriteLine(result);
        Console.WriteLine();

        // 預期輸出：The image features a fluffy cat sitting in a lush, colorful garden.
        // The garden is filled with various flowers and plants, creating a vibrant and serene atmosphere...
    }

    /// <summary>
    /// 顯示 MCP 伺服器提供的資源清單。
    /// </summary>
    /// <param name="resources">要顯示的資源清單。</param>
    private static void DisplayResources(IList<McpClientResource> resources)
    {
        Console.WriteLine("Available MCP resources:");
        foreach (var resource in resources)
        {
            Console.WriteLine($"- Name: {resource.Name}, Uri: {resource.Uri}, Description: {resource.Description}");
        }
        Console.WriteLine();
    }
}
