// Copyright (c) Microsoft. All rights reserved.

using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using Resources;

namespace GettingStarted;

/// <summary>
/// 此範例示範如何載入 Open API <see cref="KernelPlugin"/> 執行個體。
/// </summary>
public sealed class Step9_OpenAPI_Plugins(ITestOutputHelper output) : BaseTest(output)
{
    private const bool UseRemoteApiSwagger = false;

    /// <summary>
    /// 示範如何載入 Open API <see cref="KernelPlugin"/> 執行個體。
    /// </summary>
    [Fact]
    public async Task AddOpenAPIPlugins()
    {
        // 建立具備 OpenAI 聊天補全能力的 Kernel
        IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddAzureOpenAIChatClient(
                deploymentName: TestConfiguration.AzureOpenAI.DeploymentName,
                endpoint: TestConfiguration.AzureOpenAI.Endpoint,
                credentials: new DefaultAzureCredential());
        Kernel kernel = kernelBuilder.Build();

        // 載入 OpenAPI 外掛
        var stream = EmbeddedResource.ReadStream("repair-service.json");
        var plugin = UseRemoteApiSwagger ? await kernel.ImportPluginFromOpenApiAsync("RepairService", stream!) : await createPluginFromLocal(kernel);
        kernel.Plugins.Add(TransformPlugin(plugin));

        PromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
        Console.WriteLine(await kernel.InvokePromptAsync("List all of the repairs.", new(settings)));
    }

    /// <summary>
    /// 示範如何轉換 Open API <see cref="KernelPlugin"/> 執行個體，使其支援搭配 ChatClient 的相依性注入。
    /// </summary>
    [Fact]
    public async Task TransformOpenAPIPlugins()
    {
        // 建立包含 ChatClient 與相依性注入的 Kernel
        var serviceProvider = BuildServiceProvider();
        var kernel = serviceProvider.GetRequiredService<Kernel>();

        // 載入 OpenAPI 外掛
        var stream = EmbeddedResource.ReadStream("repair-service.json");
        var plugin = UseRemoteApiSwagger ? await kernel.CreatePluginFromOpenApiAsync("RepairService", stream!) : await createPluginFromLocal(kernel);
        // 轉換外掛，透過相依性注入使用 IMechanicService
        kernel.Plugins.Add(TransformPlugin(plugin));

        PromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
        Console.WriteLine(await kernel.InvokePromptAsync("Book an appointment to drain the old engine oil and replace it with fresh oil.", new(settings)));
    }

    private async Task<KernelPlugin> createPluginFromLocal(Kernel kernel)
    {
        return await kernel.CreatePluginFromOpenApiAsync(
            pluginName: "RepairService",
            uri: new Uri("http://localhost:5277/swagger/v1/swagger.json"),
            executionParameters: new OpenApiFunctionExecutionParameters
            {
                EnablePayloadNamespacing = true
            });
    }

    /// <summary>
    /// 建立可用於解析服務的 ServiceProvider。
    /// </summary>
    private ServiceProvider BuildServiceProvider()
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<IMechanicService>(new FakeMechanicService());

        // 加入使用 OpenAI 的 ChatClient
        collection.AddAzureOpenAIChatClient(
            deploymentName: TestConfiguration.AzureOpenAI.DeploymentName,
            endpoint: TestConfiguration.AzureOpenAI.Endpoint,
            credentials: new DefaultAzureCredential());

        var kernelBuilder = collection.AddKernel();

        return collection.BuildServiceProvider();
    }

    /// <summary>
    /// 轉換外掛以變更 createRepair 函式的行為。
    /// </summary>
    public static KernelPlugin TransformPlugin(KernelPlugin plugin)
    {
        List<KernelFunction>? functions = [];

        foreach (KernelFunction function in plugin)
        {
            if (function.Name == "createRepair")
            {
                functions.Add(CreateRepairFunction(function));
            }
            else
            {
                functions.Add(function);
            }
        }

        return KernelPluginFactory.CreateFromFunctions(plugin.Name, plugin.Description, functions);
    }

    /// <summary>
    /// 為 createRepair 作業建立 <see cref="KernelFunction"/> 執行個體，僅接收
    /// title 與 description 參數，並透過委派使用 IMechanicService 取得
    /// assignedTo。
    /// </summary>
    private static KernelFunction CreateRepairFunction(KernelFunction function)
    {
        var method = (
            Kernel kernel,
            KernelFunction currentFunction,
            KernelArguments arguments,
            [FromKernelServices] IMechanicService mechanicService,
            CancellationToken cancellationToken) =>
        {
            arguments.Add("assignedTo", mechanicService.GetMechanic());
            arguments.Add("date", DateTime.UtcNow.ToString("R"));

            return function.InvokeAsync(kernel, arguments, cancellationToken);
        };

        var options = new KernelFunctionFromMethodOptions()
        {
            FunctionName = function.Name,
            Description = function.Description,
            Parameters = function.Metadata.Parameters.Where(p => p.Name == "title" || p.Name == "description").ToList(),
            ReturnParameter = function.Metadata.ReturnParameter,
        };

        return KernelFunctionFactory.CreateFromMethod(method, options);
    }

    /// <summary>
    /// 取得下一個工作指派技師之服務介面。
    /// </summary>
    public interface IMechanicService
    {
        /// <summary>
        /// 回傳下一個工作要指派的技師名稱。
        /// </summary>
        string GetMechanic();
    }

    /// <summary>
    /// <see cref="IMechanicService"/> 的模擬實作。
    /// </summary>
    public class FakeMechanicService : IMechanicService
    {
        /// <inheritdoc/>
        public string GetMechanic() => "Bob";
    }
}
