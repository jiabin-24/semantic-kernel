// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using System.Text.Json.Serialization;
using Azure.Identity;
using Microsoft.OpenApi.Extensions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace GettingStarted;

/// <summary>
/// 此範例示範如何搭配 ChatClient 載入 <see cref="KernelPlugin"/> 執行個體。
/// </summary>
public sealed class Step2_Add_Plugins(ITestOutputHelper output) : BaseTest(output)
{
    /// <summary>
    /// 示範使用 ChatClient 載入 <see cref="KernelPlugin"/> 執行個體的不同方式。
    /// </summary>
    [Fact]
    public async Task AddPlugins()
    {
        // 建立包含 ChatClient 與外掛的 Kernel
        IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddAzureOpenAIChatClient(
                deploymentName: TestConfiguration.AzureOpenAI.DeploymentName,
                endpoint: TestConfiguration.AzureOpenAI.Endpoint,
                credentials: new DefaultAzureCredential());

        kernelBuilder.Plugins.AddFromType<TimeInformation>();
        kernelBuilder.Plugins.AddFromType<WidgetFactory>();
        Kernel kernel = kernelBuilder.Build();

        // 範例 1：用提示詞詢問 AI 無法提供、可能產生幻覺的資訊
        Console.WriteLine("Example 1: Asking the AI for information it cannot provide:");
        Console.WriteLine(await kernel.InvokePromptAsync("How many days until Christmas?"));

        // 範例 2：使用可直接呼叫外掛的範本提示詞
        Console.WriteLine("\nExample 2: Using templated prompts that invoke plugins directly:");
        Console.WriteLine(await kernel.InvokePromptAsync("The current time is {{TimeInformation.GetCurrentUtcTime}}. How many days until Christmas?"));

        // 範例 3：搭配函式呼叫，讓外掛自動被呼叫
        OpenAIPromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
        Console.WriteLine("\nExample 3: Using function calling for automatic plugin invocation:");
        Console.WriteLine(await kernel.InvokePromptAsync("How many days until Christmas? Explain your thinking.", new(settings)));

        // 範例 4：搭配函式呼叫與列舉型別處理較複雜情境
        Console.WriteLine("\nExample 4: Using function calling for complex scenarios with enumerations:");
        Console.WriteLine(await kernel.InvokePromptAsync("Create a handy lime colored widget for me.", new(settings)));
        Console.WriteLine(await kernel.InvokePromptAsync("Create a beautiful scarlet colored widget for me.", new(settings)));
        Console.WriteLine(await kernel.InvokePromptAsync("Create an attractive maroon and navy colored widget for me.", new(settings)));
    }

    /// <summary>
    /// 回傳目前時間的外掛。
    /// </summary>
    public class TimeInformation
    {
        [KernelFunction]
        [Description("Retrieves the current time in UTC.")]
        public string GetCurrentUtcTime() => DateTime.UtcNow.ToString("R");
    }

    /// <summary>
    /// 建立小工具（Widget）的外掛。
    /// </summary>
    public class WidgetFactory
    {
        [KernelFunction]
        [Description("Creates a new widget of the specified type and colors")]
        public WidgetDetails CreateWidget([Description("The type of widget to be created")] WidgetType widgetType, [Description("The colors of the widget to be created")] WidgetColor[] widgetColors)
        {
            var colors = string.Join('-', widgetColors.Select(c => c.GetDisplayName()).ToArray());
            return new()
            {
                SerialNumber = $"{widgetType}-{colors}-{Guid.NewGuid()}",
                Type = widgetType,
                Colors = widgetColors
            };
        }
    }

    /// <summary>
    /// 需要 <see cref="JsonConverter"/> 才能正確轉換列舉值。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum WidgetType
    {
        [Description("A widget that is useful.")]
        Useful,

        [Description("A widget that is decorative.")]
        Decorative
    }

    /// <summary>
    /// 需要 <see cref="JsonConverter"/> 才能正確轉換列舉值。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum WidgetColor
    {
        [Description("Use when creating a red item.")]
        Red,

        [Description("Use when creating a green item.")]
        Green,

        [Description("Use when creating a blue item.")]
        Blue
    }

    public class WidgetDetails
    {
        public string SerialNumber { get; init; }
        public WidgetType Type { get; init; }
        public WidgetColor[] Colors { get; init; }
    }
}
