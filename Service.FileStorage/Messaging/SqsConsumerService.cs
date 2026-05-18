using Amazon.SQS;
using Amazon.SQS.Model;
using Service.FileStorage.Storage;

namespace Service.FileStorage.Messaging;

/// <summary>
/// Клиентская служба для приема сообщений из очереди SQS.
/// </summary>
/// <param name="sqsClient">Клиент SQS</param>
/// <param name="scopeFactory">Фабрика контекста</param>
/// <param name="configuration">Конфигурация</param>
/// <param name="logger">Логгер</param>
public class SqsConsumerService(IAmazonSQS sqsClient,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<SqsConsumerService> logger) : BackgroundService
{
    private readonly string _queueName = configuration["AWS:Resources:SQSQueueName"]
        ?? throw new KeyNotFoundException("SQS queue name was not found in configuration");

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Служба потребителя SQS запущена");

        while (!stoppingToken.IsCancellationRequested)
        {
            var response = await sqsClient.ReceiveMessageAsync(
                new ReceiveMessageRequest
                {
                    QueueUrl = _queueName,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 5
                }, stoppingToken);

            if (response == null)
            {
                logger.LogWarning("Получен пустой ответ из очереди {queue}", _queueName);
                continue;
            }

            logger.LogInformation("Получено {count} сообщений из очереди", response.Messages?.Count ?? 0);

            if (response.Messages != null)
            {
                foreach (var message in response.Messages)
                {
                    try
                    {
                        logger.LogInformation("Обработка сообщения {messageId}", message.MessageId);

                        using var scope = scopeFactory.CreateScope();
                        var storageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
                        await storageService.UploadFile(message.Body);

                        await sqsClient.DeleteMessageAsync(_queueName, message.ReceiptHandle, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Ошибка при обработке сообщения {messageId}", message.MessageId);
                    }
                }
                logger.LogInformation("Пачка из {count} сообщений обработана", response.Messages.Count);
            }
        }
    }
}
