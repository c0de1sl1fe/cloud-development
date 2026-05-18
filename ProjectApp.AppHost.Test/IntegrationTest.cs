using System.Text.Json;
using Aspire.Hosting;
using Microsoft.Extensions.Logging;
using ProjectApp.Domain.Entities;
using Xunit.Abstractions;

namespace ProjectApp.AppHost.Test;

/// <summary>
/// Интеграционные тесты для проверки микросервисного пайплайна:
/// API -> SQS -> FileStorage -> MinIO
/// </summary>
/// <param name="output">Служба журналирования юнит-тестов</param>
public class IntegrationTest(ITestOutputHelper output) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private DistributedApplication? _app;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        var cancellationToken = CancellationToken.None;
        Environment.SetEnvironmentVariable("DisableClient", "true");
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ProjectApp_AppHost>(cancellationToken);
        builder.Configuration["DcpPublisher:RandomizePorts"] = "false";
        builder.Services.AddLogging(logging =>
        {
            logging.AddXUnit(output);
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter("Aspire.Hosting.Dcp", LogLevel.Debug);
            logging.AddFilter("Aspire.Hosting", LogLevel.Debug);
        });
        _app = await builder.BuildAsync(cancellationToken);
        await _app.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Проверяет, что вызов гейтвея:
    /// <list type="bullet">
    /// <item><description>В ответ отдаёт сгенерированный проект</description></item>
    /// <item><description>Сериализует проект в MinIO через брокер SQS и файловый сервис</description></item>
    /// <item><description>Данные, отданные клиенту и положенные в объектное хранилище, идентичны</description></item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task ApiToStorageIntegrationTest()
    {
        var id = new Random().Next(1, 100);

        using var gatewayClient = _app!.CreateHttpClient("projectapp-apigateway");
        using var gatewayResponse = await gatewayClient.GetAsync($"/api/project?id={id}");
        var apiProject = JsonSerializer.Deserialize<SoftwareProject>(await gatewayResponse.Content.ReadAsStringAsync(), _jsonOptions);

        await Task.Delay(10_000);

        using var storageClient = _app!.CreateHttpClient("service-filestorage");
        using var listResponse = await storageClient.GetAsync("/api/files");
        var fileList = JsonSerializer.Deserialize<List<string>>(await listResponse.Content.ReadAsStringAsync());
        using var fileResponse = await storageClient.GetAsync($"/api/files/project_{id}.json");
        var s3Project = JsonSerializer.Deserialize<SoftwareProject>(await fileResponse.Content.ReadAsStringAsync(), _jsonOptions);

        Assert.NotNull(fileList);
        Assert.Single(fileList);
        Assert.Equal($"project_{id}.json", fileList[0]);
        Assert.NotNull(apiProject);
        Assert.NotNull(s3Project);
        Assert.Equal(id, s3Project.Id);
        Assert.Equivalent(apiProject, s3Project);
    }

    /// <summary>
    /// Проверяет, что повторный запрос проекта с тем же id обслуживается из кэша
    /// и не приводит к повторной публикации в брокер: файловый сервис получает ровно одно сообщение.
    /// </summary>
    [Fact]
    public async Task CacheHitDoesNotDuplicateStorageObjectTest()
    {
        var id = new Random().Next(1, 100);

        using var gatewayClient = _app!.CreateHttpClient("projectapp-apigateway");
        using var firstResponse = await gatewayClient.GetAsync($"/api/project?id={id}");
        var firstProject = JsonSerializer.Deserialize<SoftwareProject>(await firstResponse.Content.ReadAsStringAsync(), _jsonOptions);
        using var secondResponse = await gatewayClient.GetAsync($"/api/project?id={id}");
        var secondProject = JsonSerializer.Deserialize<SoftwareProject>(await secondResponse.Content.ReadAsStringAsync(), _jsonOptions);

        await Task.Delay(10_000);

        using var storageClient = _app!.CreateHttpClient("service-filestorage");
        using var countResponse = await storageClient.GetAsync("/api/files/upload-count");
        var uploadCount = JsonSerializer.Deserialize<int>(await countResponse.Content.ReadAsStringAsync());

        Assert.NotNull(firstProject);
        Assert.NotNull(secondProject);
        Assert.Equivalent(firstProject, secondProject);
        Assert.Equal(1, uploadCount);
    }

    /// <summary>
    /// Проверяет, что три запроса с разными идентификаторами создают три отдельных
    /// файла в бакете, содержимое которых совпадает с ответами гейтвея.
    /// </summary>
    [Fact]
    public async Task MultipleDistinctProjectsAreStoredTest()
    {
        var ids = new[] { 11, 22, 33 };
        var apiProjects = new Dictionary<int, SoftwareProject>();

        using var gatewayClient = _app!.CreateHttpClient("projectapp-apigateway");
        foreach (var id in ids)
        {
            using var response = await gatewayClient.GetAsync($"/api/project?id={id}");
            var project = JsonSerializer.Deserialize<SoftwareProject>(await response.Content.ReadAsStringAsync(), _jsonOptions);
            Assert.NotNull(project);
            apiProjects[id] = project!;
        }

        await Task.Delay(10_000);

        using var storageClient = _app!.CreateHttpClient("service-filestorage");
        using var listResponse = await storageClient.GetAsync("/api/files");
        var fileList = JsonSerializer.Deserialize<List<string>>(await listResponse.Content.ReadAsStringAsync());

        Assert.NotNull(fileList);
        Assert.Equal(ids.Length, fileList!.Count);
        foreach (var id in ids)
            Assert.Contains($"project_{id}.json", fileList);

        foreach (var id in ids)
        {
            using var fileResponse = await storageClient.GetAsync($"/api/files/project_{id}.json");
            var s3Project = JsonSerializer.Deserialize<SoftwareProject>(await fileResponse.Content.ReadAsStringAsync(), _jsonOptions);
            Assert.NotNull(s3Project);
            Assert.Equal(id, s3Project!.Id);
            Assert.Equivalent(apiProjects[id], s3Project);
        }
    }

    /// <summary>
    /// Проверяет, что запрос несуществующего объекта из бакета завершается ошибкой.
    /// </summary>
    [Fact]
    public async Task MissingStorageObjectReturnsErrorTest()
    {
        using var storageClient = _app!.CreateHttpClient("service-filestorage");
        using var response = await storageClient.GetAsync("/api/files/project_99999.json");

        Assert.False(response.IsSuccessStatusCode);
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _app!.StopAsync();
        await _app.DisposeAsync();
    }
}
