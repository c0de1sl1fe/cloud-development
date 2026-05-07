using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Minio;
using Minio.DataModel.Args;

namespace Service.FileStorage.Storage;

/// <summary>
/// Служба для манипуляции файлами в объектном хранилище MinIO.
/// </summary>
/// <param name="client">MinIO клиент</param>
/// <param name="configuration">Конфигурация</param>
/// <param name="logger">Логгер</param>
public class MinioFileStorageService(IMinioClient client, IConfiguration configuration, ILogger<MinioFileStorageService> logger) : IFileStorageService
{
    private readonly string _bucketName = configuration["AWS:Resources:MinioBucketName"]
        ?? throw new KeyNotFoundException("Minio bucket name was not found in configuration");

    /// <inheritdoc/>
    public async Task<List<string>> GetFileList()
    {
        var list = new List<string>();
        var request = new ListObjectsArgs()
            .WithBucket(_bucketName)
            .WithPrefix("")
            .WithRecursive(true);
        logger.LogInformation("Запрашиваем список файлов в бакете {bucket}", _bucketName);
        var responseList = client.ListObjectsEnumAsync(request);

        if (responseList == null)
            logger.LogWarning("Получен пустой ответ от бакета {bucket}", _bucketName);

        await foreach (var response in responseList!)
            list.Add(response.Key);
        return list;
    }

    /// <inheritdoc/>
    public async Task<bool> UploadFile(string fileData)
    {
        var rootNode = JsonNode.Parse(fileData) ?? throw new ArgumentException("Passed string is not a valid JSON");
        var id = rootNode["id"]?.GetValue<int>() ?? throw new ArgumentException("Passed JSON has invalid structure");

        var bytes = Encoding.UTF8.GetBytes(fileData);
        using var stream = new MemoryStream(bytes);
        stream.Seek(0, SeekOrigin.Begin);

        logger.LogInformation("Начинаем загрузку проекта {file} в бакет {bucket}", id, _bucketName);
        var prefix = configuration["FilePrefix"] ?? "project_";
        var request = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithStreamData(stream)
            .WithObjectSize(bytes.Length)
            .WithObject($"{prefix}{id}.json");

        var response = await client.PutObjectAsync(request);

        if (response.ResponseStatusCode != HttpStatusCode.OK)
        {
            logger.LogError("Не удалось загрузить проект {file}: {code}", id, response.ResponseStatusCode);
            return false;
        }
        logger.LogInformation("Проект {file} успешно загружен в бакет {bucket}", id, _bucketName);
        return true;
    }

    /// <inheritdoc/>
    public async Task<JsonNode> DownloadFile(string key)
    {
        logger.LogInformation("Начинаем скачивание файла {file} из бакета {bucket}", key, _bucketName);

        try
        {
            var memoryStream = new MemoryStream();

            var request = new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(key)
                .WithCallbackStream(async (stream, cancellationToken) =>
                {
                    await stream.CopyToAsync(memoryStream, cancellationToken);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                });

            var response = await client.GetObjectAsync(request);

            if (response == null)
            {
                logger.LogError("Не удалось скачать файл {file}", key);
                throw new InvalidOperationException($"Error occurred downloading {key} - object is null");
            }
            using var reader = new StreamReader(memoryStream, Encoding.UTF8);
            return JsonNode.Parse(reader.ReadToEnd()) ?? throw new InvalidOperationException("Downloaded document is not a valid JSON");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при скачивании файла {file}", key);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task EnsureBucketExists()
    {
        logger.LogInformation("Проверяем существование бакета {bucket}", _bucketName);
        try
        {
            var request = new BucketExistsArgs()
                .WithBucket(_bucketName);

            var exists = await client.BucketExistsAsync(request);
            if (!exists)
            {
                logger.LogInformation("Создаём бакет {bucket}", _bucketName);
                var createRequest = new MakeBucketArgs()
                    .WithBucket(_bucketName);
                await client.MakeBucketAsync(createRequest);
                return;
            }
            logger.LogInformation("Бакет {bucket} уже существует", _bucketName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Необработанная ошибка при проверке бакета {bucket}", _bucketName);
            throw;
        }
    }
}
