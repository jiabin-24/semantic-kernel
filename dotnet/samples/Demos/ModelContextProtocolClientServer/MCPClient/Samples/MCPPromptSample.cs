// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace MCPClient.Samples;

/// <summary>
/// 示範如何在 Semantic Kernel 中使用 Model Context Protocol (MCP) 提示詞。
/// </summary>
internal sealed class MCPPromptSample : BaseSample
{
    /// <summary>
    /// 示範如何在 Semantic Kernel 中使用 MCP 提示詞。
    /// 此方法中的程式碼流程：
    /// 1. 建立 MCP 用戶端。
    /// 2. 取得 MCP 伺服器提供的提示詞清單。
    /// 3. 使用 `GetCurrentWeatherForCity` 提示詞取得 Boston 與 Sydney 的目前天氣。
    /// 4. 將 MCP 伺服器提示詞加入聊天歷程，並要求 AI 模型比較兩地天氣、建議較適合散步的地點。
    /// 5. AI 模型在接收並處理兩地天氣資料與提示詞後回傳答案。
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine($"Running the {nameof(MCPPromptSample)} sample.");

        // 建立 MCP 用戶端
        await using IMcpClient mcpClient = await CreateMcpClientAsync();

        // 取得並顯示 MCP 伺服器提供的提示詞清單
        IList<McpClientPrompt> prompts = await mcpClient.ListPromptsAsync();
        DisplayPrompts(prompts);

        // 建立 Kernel
        Kernel kernel = CreateKernelWithChatCompletionService();

        // 透過 MCP 伺服器的 `GetCurrentWeatherForCity` 提示詞取得 Boston 天氣
        GetPromptResult bostonWeatherPrompt;
        // 透過 MCP 伺服器的 `GetCurrentWeatherForCity` 提示詞取得 Sydney 天氣
        GetPromptResult sydneyWeatherPrompt;
        try
        {
            bostonWeatherPrompt = await mcpClient.GetPromptAsync("GetCurrentWeatherForCity", new Dictionary<string, object?>() { ["city"] = "Boston", ["time"] = DateTime.UtcNow.ToString("O") });
            sydneyWeatherPrompt = await mcpClient.GetPromptAsync("GetCurrentWeatherForCity", new Dictionary<string, object?>() { ["city"] = "Sydney", ["time"] = DateTime.UtcNow.ToString("O") });
        }
        catch (ModelContextProtocol.McpException ex)
        {
            Console.Error.WriteLine($"MCP error while retrieving prompt for Boston: {ex.Message}");
            Console.Error.WriteLine($"Inner exception: {ex.InnerException}");
            throw;
        }

        // 將提示詞加入聊天歷程
        ChatHistory chatHistory = [];
        chatHistory.AddRange(bostonWeatherPrompt.ToChatMessageContents());
        chatHistory.AddRange(sydneyWeatherPrompt.ToChatMessageContents());
        chatHistory.AddUserMessage("Compare the weather in the two cities and suggest the best place to go for a walk.");

        // 使用 MCP 工具與提示詞執行對話
        IChatCompletionService chatCompletion = kernel.GetRequiredService<IChatCompletionService>();

        ChatMessageContent result = await chatCompletion.GetChatMessageContentAsync(chatHistory, kernel: kernel);

        Console.WriteLine(result);
        Console.WriteLine();

        // 預期輸出：Given these conditions, Sydney would be the better choice for a pleasant walk, as the sunny and warm weather is ideal for outdoor activities.
        // The rain in Boston could make walking less enjoyable and potentially inconvenient.
    }

    /// <summary>
    /// 顯示可用的 MCP 提示詞清單。
    /// </summary>
    /// <param name="prompts">要顯示的提示詞清單。</param>
    private static void DisplayPrompts(IList<McpClientPrompt> prompts)
    {
        Console.WriteLine("Available MCP prompts:");
        foreach (var prompt in prompts)
        {
            Console.WriteLine($"- Name: {prompt.Name}, Description: {prompt.Description}");
        }
        Console.WriteLine();
    }
}
