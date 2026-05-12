// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace GettingStarted;

/// <summary>
/// 此範例示範如何在 Semantic Kernel 中使用相依性注入。
/// </summary>
public sealed class Step4_Dependency_Injection(ITestOutputHelper output) : BaseTest(output)
{
    /// <summary>
    /// 示範如何建立可參與相依性注入的 <see cref="Kernel"/>。
    /// </summary>
    [Fact]
    public async Task GetKernelUsingDependencyInjection()
    {
        // 若應用程式遵循 DI 準則，則不需要下一行，因為 DI 會將 KernelClient 執行個體注入到相依類別中。
        // DI 容器準則 - https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines#recommendations
        var serviceProvider = BuildServiceProvider();
        var kernel = serviceProvider.GetRequiredService<Kernel>();

        // 以範本提示詞呼叫 Kernel，並以串流方式輸出結果
        KernelArguments arguments = new() { { "topic", "earth when viewed from space" } };
        await foreach (var update in
                       kernel.InvokePromptStreamingAsync("What color is the {{$topic}}? Provide a detailed explanation.", arguments))
        {
            Console.Write(update);
        }
    }

    /// <summary>
    /// 示範如何使用可參與相依性注入的外掛。
    /// </summary>
    [Fact]
    public async Task PluginUsingDependencyInjection()
    {
        // 若應用程式遵循 DI 準則，則不需要下一行，因為 DI 會將 Kernel 執行個體注入到相依類別中。
        // DI 容器準則 - https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines#recommendations
        var serviceProvider = BuildServiceProvider();
        var kernel = serviceProvider.GetRequiredService<Kernel>();

        // 呼叫提示詞；該提示詞依賴一個透過 DI 提供服務的外掛。
        PromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
        Console.WriteLine(await kernel.InvokePromptAsync("Greet the current user by name.", new(settings)));
    }

    /// <summary>
    /// 建立可用於解析服務的 ServiceProvider。
    /// </summary>
    private ServiceProvider BuildServiceProvider()
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<ILoggerFactory>(new XunitLogger(this.Output));
        collection.AddSingleton<IUserService>(new FakeUserService());

        // 加入使用 OpenAI 的 ChatClient
        collection.AddAzureOpenAIChatClient(
                deploymentName: TestConfiguration.AzureOpenAI.DeploymentName,
                endpoint: TestConfiguration.AzureOpenAI.Endpoint,
                credentials: new DefaultAzureCredential());

        var kernelBuilder = collection.AddKernel();
        kernelBuilder.Plugins.AddFromType<TimeInformation>();
        kernelBuilder.Plugins.AddFromType<UserInformation>();

        return collection.BuildServiceProvider();
    }

    /// <summary>
    /// 回傳目前時間的外掛。
    /// </summary>
    public class TimeInformation(ILoggerFactory loggerFactory)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<TimeInformation>();

        [KernelFunction]
        [Description("Retrieves the current time in UTC.")]
        public string GetCurrentUtcTime()
        {
            var utcNow = DateTime.UtcNow.ToString("R");
            this._logger.LogInformation("Returning current time {0}", utcNow);
            return utcNow;
        }
    }

    /// <summary>
    /// 回傳目前使用者名稱的外掛。
    /// </summary>
    public class UserInformation(IUserService userService)
    {
        [KernelFunction]
        [Description("Retrieves the current users name.")]
        public string GetUsername()
        {
            return userService.GetCurrentUsername();
        }
    }

    /// <summary>
    /// 取得目前使用者識別資訊之服務介面。
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// 回傳目前使用者的識別資訊。
        /// </summary>
        string GetCurrentUsername();
    }

    /// <summary>
    /// <see cref="IUserService"/> 的模擬實作。
    /// </summary>
    public class FakeUserService : IUserService
    {
        /// <inheritdoc/>
        public string GetCurrentUsername() => "Bob";
    }
}
