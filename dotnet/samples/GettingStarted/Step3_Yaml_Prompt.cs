// Copyright (c) Microsoft. All rights reserved.

using Azure.Identity;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using Resources;

namespace GettingStarted;

/// <summary>
/// 此範例示範如何從 YAML 資源建立提示詞 <see cref="KernelFunction"/>。
/// </summary>
public sealed class Step3_Yaml_Prompt(ITestOutputHelper output) : BaseTest(output)
{
    /// <summary>
    /// 示範如何從 YAML 資源建立提示詞 <see cref="KernelFunction"/>。
    /// </summary>
    [Fact]
    public async Task CreatePromptFromYaml()
    {
        // 建立具備 OpenAI 聊天補全能力的 Kernel
        Kernel kernel = Kernel.CreateBuilder()
            .AddAzureOpenAIChatClient(
                deploymentName: TestConfiguration.AzureOpenAI.DeploymentName,
                endpoint: TestConfiguration.AzureOpenAI.Endpoint,
                credentials: new DefaultAzureCredential())
            .Build();

        // 從資源載入提示詞
        var generateStoryYaml = EmbeddedResource.Read("GenerateStory.yaml");
        var function = kernel.CreateFunctionFromPromptYaml(generateStoryYaml);

        // 呼叫提示詞函式並顯示結果
        Console.WriteLine(await kernel.InvokeAsync(function, arguments: new()
            {
                { "topic", "Dog" },
                { "length", "3" },
            }));

        // 從資源載入提示詞
        var generateStoryHandlebarsYaml = EmbeddedResource.Read("GenerateStoryHandlebars.yaml");
        function = kernel.CreateFunctionFromPromptYaml(generateStoryHandlebarsYaml, new HandlebarsPromptTemplateFactory());

        // 呼叫提示詞函式並顯示結果
        Console.WriteLine(await kernel.InvokeAsync(function, arguments: new()
            {
                { "topic", "Cat" },
                { "length", "3" },
            }));
    }
}
