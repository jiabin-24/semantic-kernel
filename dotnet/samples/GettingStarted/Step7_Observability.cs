// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace GettingStarted;

public sealed class Step7_Observability(ITestOutputHelper output) : BaseTest(output)
{
    /// <summary>
    /// 示範如何透過篩選器觀察 <see cref="KernelPlugin"/> 執行個體的執行過程。
    /// </summary>
    [Fact]
    public async Task ObservabilityWithFilters()
    {
        // 建立具備 OpenAI 聊天補全能力的 Kernel
        IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddAzureOpenAIChatClient(
                deploymentName: TestConfiguration.AzureOpenAI.DeploymentName,
                endpoint: TestConfiguration.AzureOpenAI.Endpoint,
                credentials: new DefaultAzureCredential());

        kernelBuilder.Plugins.AddFromType<TimeInformation>();

        // 使用 DI 加入篩選器
        kernelBuilder.Services.AddSingleton<ITestOutputHelper>(this.Output);
        kernelBuilder.Services.AddSingleton<IFunctionInvocationFilter, MyFunctionFilter>();

        Kernel kernel = kernelBuilder.Build();

        // 不透過 DI 加入篩選器
        kernel.PromptRenderFilters.Add(new MyPromptFilter(this.Output));

        // 以提示詞呼叫 Kernel，並允許 AI 自動呼叫函式
        OpenAIPromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
        Console.WriteLine(await kernel.InvokePromptAsync("How many days until Christmas? Explain your thinking.", new(settings)));
    }

    /// <summary>
    /// 回傳目前時間的外掛。
    /// </summary>
    private sealed class TimeInformation
    {
        [KernelFunction]
        [Description("Retrieves the current time in UTC.")]
        public string GetCurrentUtcTime() => DateTime.UtcNow.ToString("R");
    }

    /// <summary>
    /// 用於可觀測性的函式篩選器。
    /// </summary>
    private sealed class MyFunctionFilter(ITestOutputHelper output) : IFunctionInvocationFilter
    {
        private readonly ITestOutputHelper _output = output;

        public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
        {
            this._output.WriteLine($"Invoking {context.Function.Name}");

            await next(context);

            var metadata = context.Result?.Metadata;

            if (metadata is not null && metadata.ContainsKey("Usage"))
            {
                this._output.WriteLine($"Token usage: {metadata["Usage"]?.AsJson()}");
            }
        }
    }

    /// <summary>
    /// 用於可觀測性的提示詞篩選器。
    /// </summary>
    private sealed class MyPromptFilter(ITestOutputHelper output) : IPromptRenderFilter
    {
        private readonly ITestOutputHelper _output = output;

        public async Task OnPromptRenderAsync(PromptRenderContext context, Func<PromptRenderContext, Task> next)
        {
            this._output.WriteLine($"Rendering prompt for {context.Function.Name}");

            await next(context);

            this._output.WriteLine($"Rendered prompt: {context.RenderedPrompt}");
        }
    }
}
