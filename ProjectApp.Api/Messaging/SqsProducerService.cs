using System.Net;
using System.Text.Json;
using Amazon.SQS;
using ProjectApp.Domain.Entities;

namespace ProjectApp.Api.Messaging;

/// <summary>
/// Служба для отправки сообщений в SQS.
/// </summary>
/// <param name="client">Клиент SQS</param>
/// <param name="configuration">Конфигурация</param>
/// <param name="logger">Логгер</param>
public class SqsProducerService(IAmazonSQS client, IConfiguration configuration, ILogger<SqsProducerService> logger) : IProjectPublisherService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _queueName = configuration["AWS:Resources:SQSQueueName"]
        ?? throw new KeyNotFoundException("SQS queue name was not found in configuration");

    /// <inheritdoc/>
    public async Task SendMessage(SoftwareProject project)
    {
        try
        {
            var json = JsonSerializer.Serialize(project, _jsonOptions);
            var response = await client.SendMessageAsync(_queueName, json);
            if (response.HttpStatusCode == HttpStatusCode.OK)
                logger.LogInformation("Проект {id} отправлен в файловый сервис через SQS", project.Id);
            else
                throw new Exception($"SQS вернул статус {response.HttpStatusCode}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось отправить проект в очередь SQS");
        }
    }
}
