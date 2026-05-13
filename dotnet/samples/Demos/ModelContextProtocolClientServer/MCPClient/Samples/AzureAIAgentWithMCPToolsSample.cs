// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.AzureAI;
using ModelContextProtocol.Client;

namespace MCPClient.Samples;

/// <summary>
/// 示範如何使用 <see cref="AzureAIAgent"/> 搭配以 Kernel 函式表示的 MCP 工具。
/// </summary>
internal sealed class AzureAIAgentWithMCPToolsSample : BaseSample
{
    /// <summary>
    /// 示範如何使用 <see cref="AzureAIAgent"/> 搭配以 Kernel 函式表示的 MCP 工具。
    /// 此方法中的程式碼流程：
    /// 1. 建立 MCP 用戶端。
    /// 2. 取得 MCP 伺服器提供的工具清單。
    /// 3. 建立 Kernel，並將 MCP 工具註冊為 Kernel 函式。
    /// 4. 定義 Azure AI Agent（含指示、名稱、Kernel 與參數）。
    /// 5. 以提示詞呼叫 Agent。
    /// 6. Agent 將提示詞與以 Kernel 函式表示的 MCP 工具一併送到 AI 模型。
    /// 7. AI 模型呼叫 DateTimeUtils-GetCurrentDateTimeInUtc 函式，取得下一個函式所需的 UTC 目前時間。
    /// 8. AI 模型呼叫 WeatherUtils-GetWeatherForCity 函式，使用目前時間與從提示詞擷取的 `Boston` 參數取得天氣資訊。
    /// 9. AI 模型收到函式回傳的天氣資訊後回覆 Agent，再由 Agent 回覆使用者。
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine($"Running the {nameof(AzureAIAgentWithMCPToolsSample)} sample.");

        // 建立 MCP 用戶端
        await using IMcpClient mcpClient = await CreateMcpClientAsync();

        // 取得並顯示 MCP 伺服器提供的工具清單
        IList<McpClientTool> tools = await mcpClient.ListToolsAsync();
        DisplayTools(tools);

        // 建立 Kernel 並將 MCP 工具註冊為 Kernel 函式
        Kernel kernel = new();
        kernel.Plugins.AddFromFunctions("Tools", tools.Select(aiFunction => aiFunction.AsKernelFunction()));

        // 使用已註冊 MCP 工具的 Kernel 定義 Agent
        AzureAIAgent agent = await CreateAzureAIAgentAsync(
            name: "WeatherAgent",
            instructions: "Answer questions about the weather.",
            kernel: kernel
        );

        // 以提示詞呼叫 Agent
        string prompt = "What is the likely color of the sky in Boston today?";
        Console.WriteLine(prompt);

        AgentResponseItem<ChatMessageContent> response = await agent.InvokeAsync(message: prompt).FirstAsync();
        Console.WriteLine(response.Message);
        Console.WriteLine();

        // 預期輸出：Today in Boston, the weather is 61°F and rainy. Due to the rain, the likely color of the sky will be gray.

        // 使用後刪除 Agent 執行緒
        await response!.Thread.DeleteAsync();

        // 使用後刪除 Agent
        await agent.Client.Administration.DeleteAgentAsync(agent.Id);
    }

    /// <summary>
    /// 依指定名稱與指示建立 <see cref="AzureAIAgent"/> 執行個體。
    /// </summary>
    /// <param name="kernel">Kernel 執行個體。</param>
    /// <param name="name">Agent 名稱。</param>
    /// <param name="instructions">Agent 指示內容。</param>
    /// <returns><see cref="AzureAIAgent"/> 執行個體。</returns>
    private static async Task<AzureAIAgent> CreateAzureAIAgentAsync(Kernel kernel, string name, string instructions)
    {
        // 載入並驗證設定
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build();

        if (config["AzureAI:Endpoint"] is not { } endpoint)
        {
            const string Message = "Please provide a valid `AzureAI:ConnectionString` secret to run this sample. See the associated README.md for more details.";
            Console.Error.WriteLine(Message);
            throw new InvalidOperationException(Message);
        }

        string modelId = config["AzureAI:ChatModelId"] ?? "gpt-4o-mini";

        // 使用 Service Principal 登入
        //var tenantId = config["AzureAI:TenantId"];
        //var clientId = config["AzureAI:ClientId"];
        //var clientSecret = config["AzureAI:ClientSecret"];
        //var credential = new Azure.Identity.ClientSecretCredential(tenantId, clientId, clientSecret);

        // 建立 Azure AI Agent
        PersistentAgentsClient agentsClient = AzureAIAgent.CreateAgentsClient(endpoint, new DefaultAzureCredential());
        PersistentAgent agent = await agentsClient.Administration.CreateAgentAsync(modelId, name, null, instructions);

        return new AzureAIAgent(agent, agentsClient)
        {
            Kernel = kernel
        };
    }
}
