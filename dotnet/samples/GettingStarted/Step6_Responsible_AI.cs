// Copyright (c) Microsoft. All rights reserved.

using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace GettingStarted;

public sealed class Step6_Responsible_AI(ITestOutputHelper output) : BaseTest(output)
{
    /// <summary>
    /// 示範如何使用提示詞篩選器，確保提示詞以負責任的方式呈現。
    /// </summary>
    [Fact]
    public async Task AddPromptFilter()
    {
        // 建立具備 OpenAI 聊天補全能力的 Kernel
        var builder = Kernel.CreateBuilder()
            .AddAzureOpenAIChatClient(
                deploymentName: TestConfiguration.AzureOpenAI.DeploymentName,
                endpoint: TestConfiguration.AzureOpenAI.Endpoint,
                credentials: new DefaultAzureCredential());

        builder.Services.AddSingleton<ITestOutputHelper>(this.Output);

        // 將提示詞篩選器加入 Kernel
        builder.Services.AddSingleton<IPromptRenderFilter, PromptFilter>();

        var kernel = builder.Build();

        KernelArguments arguments = new() { { "card_number", "4444 3333 2222 1111" } };

        var result = await kernel.InvokePromptAsync("Tell me some useful information about this credit card number {{$card_number}}?", arguments);

        Console.WriteLine(result);

        // 輸出：Sorry, but I can't assist with that.
    }

    private sealed class PromptFilter(ITestOutputHelper output) : IPromptRenderFilter
    {
        private readonly ITestOutputHelper _output = output;

        /// <summary>
        /// 在提示詞渲染前非同步呼叫的方法。
        /// </summary>
        /// <param name="context">包含提示詞渲染細節的 <see cref="PromptRenderContext"/> 執行個體。</param>
        /// <param name="next">指向管線中下一個篩選器或渲染作業本身的委派。若未呼叫，後續篩選器或渲染作業不會執行。</param>
        public async Task OnPromptRenderAsync(PromptRenderContext context, Func<PromptRenderContext, Task> next)
        {
            if (context.Arguments.ContainsName("card_number"))
            {
                context.Arguments["card_number"] = "**** **** **** ****";
            }

            await next(context);

            context.RenderedPrompt += " NO SEXISM, RACISM OR OTHER BIAS/BIGOTRY";

            this._output.WriteLine(context.RenderedPrompt);
        }
    }
}
