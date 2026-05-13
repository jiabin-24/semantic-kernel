// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace MCPClient.Samples;

/// <summary>
/// 示範如何在 Semantic Kernel 中使用 Model Context Protocol (MCP) 取樣功能。
/// </summary>
internal sealed class MCPSamplingSample : BaseSample
{
    /// <summary>
    /// 示範如何在 Semantic Kernel 中使用 MCP 取樣功能。
    /// 此方法中的程式碼流程：
    /// 1. 建立 MCP 用戶端並註冊取樣請求處理常式。
    /// 2. 取得 MCP 伺服器提供的工具清單並註冊為 Kernel 函式。
    /// 3. 提示 AI 模型根據信箱最新未讀郵件建立行程。
    /// 4. AI 模型呼叫 `MailboxUtils-SummarizeUnreadEmails` 函式來彙整未讀郵件。
    /// 5. `MailboxUtils-SummarizeUnreadEmails` 函式建立數封含附件的範例郵件，
    ///    並送出取樣請求給用戶端以產生摘要：
    ///    5.1. 用戶端接收來自伺服器的取樣請求，並呼叫取樣請求處理常式。
    ///    5.2. SK 透過 `HumanInTheLoopFilter` 攔截取樣請求呼叫，以啟用人類在迴圈（HITL）流程。
    ///    5.3. `HumanInTheLoopFilter` 允許取樣請求處理常式繼續執行。
    ///    5.5. 取樣請求處理常式將取樣請求送至 AI 模型以摘要郵件。
    ///    5.6. AI 模型處理請求並將摘要回傳給處理常式，再由處理常式回送至伺服器。
    ///    5.7. `MailboxUtils-SummarizeUnreadEmails` 函式接收結果後回傳給 AI 模型。
    /// 7. AI 模型在收到摘要後，根據未讀郵件建立行程。
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine($"Running the {nameof(MCPSamplingSample)} sample.");

        // 建立 Kernel
        Kernel kernel = CreateKernelWithChatCompletionService();

        // 註冊人類在迴圈篩選器，攔截函式呼叫以供使用者檢視並核准或拒絕
        kernel.FunctionInvocationFilters.Add(new HumanInTheLoopFilter());

        // 建立含自訂取樣請求處理常式的 MCP 用戶端
        await using IMcpClient mcpClient = await CreateMcpClientAsync(kernel, SamplingRequestHandlerAsync);

        // 將 MCP 工具匯入為 Kernel 函式，讓 AI 模型可直接呼叫
        IList<McpClientTool> tools = await mcpClient.ListToolsAsync();
        kernel.Plugins.AddFromFunctions("Tools", tools.Select(aiFunction => aiFunction.AsKernelFunction()));

        // 啟用自動函式呼叫
        OpenAIPromptExecutionSettings executionSettings = new()
        {
            Temperature = 0,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new() { RetainArgumentTypes = true })
        };

        // 執行提示詞
        string prompt = "Create a schedule for me based on the latest unread emails in my inbox.";
        IChatCompletionService chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
        ChatMessageContent result = await chatCompletion.GetChatMessageContentAsync(prompt, executionSettings, kernel);

        Console.WriteLine(result);
        Console.WriteLine();

        // 預期輸出：
        // ### Today
        // - **Review Sales Report:**
        //   - **Task:** Provide feedback on the Carretera Sales Report for January to June 2014.
        //   - **Deadline:** End of the day.
        //   - **Details:** Check the attached spreadsheet for sales data.
        //
        // ### Tomorrow
        // - **Update Employee Information:**
        //   - **Task:** Update the list of employee birthdays and positions.
        //   - **Deadline:** By the end of the day.
        //   - **Details:** Refer to the attached table for employee details.
        //
        // ### Saturday
        // - **Attend BBQ:**
        //   - **Event:** BBQ Invitation
        //   - **Details:** Join the BBQ as mentioned in the sales report email.
        //
        // ### Sunday
        // - **Join Hike:**
        //   - **Event:** Hiking Invitation
        //   - **Details:** Participate in the hike as mentioned in the HR email.
    }

    /// <summary>
    /// 處理來自 MCP 用戶端的取樣請求。
    /// </summary>
    /// <param name="kernel">Kernel 執行個體。</param>
    /// <param name="request">取樣請求。</param>
    /// <param name="progress">進度通知。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>取樣請求的結果。</returns>
    private static async Task<CreateMessageResult> SamplingRequestHandlerAsync(Kernel kernel, CreateMessageRequestParams? request, IProgress<ProgressNotificationValue> progress, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        // 將 MCP 取樣請求對應到 Semantic Kernel 提示詞執行設定
        OpenAIPromptExecutionSettings promptExecutionSettings = new()
        {
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            StopSequences = request.StopSequences?.ToList(),
        };

        // 由 MCP 取樣請求建立聊天歷程
        ChatHistory chatHistory = [];
        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            chatHistory.AddSystemMessage(request.SystemPrompt);
        }
        chatHistory.AddRange(request.Messages.ToChatMessageContents());

        // 提示 AI 模型產生回應
        IChatCompletionService chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
        ChatMessageContent result = await chatCompletion.GetChatMessageContentAsync(chatHistory, promptExecutionSettings, cancellationToken: cancellationToken);

        return result.ToCreateMessageResult();
    }
}
