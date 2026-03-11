using ProjectApp.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace ProjectApp.Api.Services.ProjectGeneratorService;

/// <summary>
/// Сервис получения программного проекта: сначала ищет в кэше, при промахе — генерирует новый и сохраняет
/// </summary>
public class SoftwareProjectGeneratorService(
    IDistributedCache cache,
    ProjectGenerator generator,
    IConfiguration configuration,
    ILogger<SoftwareProjectGeneratorService> logger) : ISoftwareProjectGeneratorService
{
    private readonly int _expirationMinutes = configuration.GetValue("CacheSettings:ExpirationMinutes", 10);

    /// <summary>
    /// Возвращает программный проект по идентификатору.
    /// Если проект найден в кэше — возвращается из него; иначе генерируется, сохраняется в кэш и возвращается.
    /// </summary>
    /// <param name="id">Идентификатор проекта</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Программный проект</returns>
    public async Task<SoftwareProject> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Attempting to retrieve software project {Id} from cache", id);

        var cacheKey = $"software-project-{id}";

        // Получаем проект из кэша
        SoftwareProject? project = null;
        try
        {
            var cachedData = await cache.GetStringAsync(cacheKey, cancellationToken);

            if (!string.IsNullOrEmpty(cachedData))
            {
                project = JsonSerializer.Deserialize<SoftwareProject>(cachedData);

                if (project != null)
                {
                    logger.LogInformation("Software project {Id} found in cache", id);
                    return project;
                }

                logger.LogWarning("Project {Id} was found in cache but could not be deserialized. Generating a new one", id);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve project {Id} from cache (error ignored)", id);
        }

        // Если в кэше нет или ошибка — генерируем новый проект
        logger.LogInformation("Project {Id} not found in cache or cache unavailable, generating a new one", id);
        project = generator.Generate();
        project.Id = id;

        // Попытка сохранить в кэш
        try
        {
            logger.LogInformation("Saving project {Id} to cache", id);

            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_expirationMinutes)
            };

            await cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(project),
                cacheOptions,
                cancellationToken);

            logger.LogInformation(
                "Software project generated and cached: Id={Id}, Name={ProjectName}, Customer={Customer}, Budget={Budget}, Completion={CompletionPercent}",
                project.Id,
                project.ProjectName,
                project.Customer,
                project.Budget,
                project.CompletionPercentage);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save project {Id} to cache (error ignored)", id);
        }

        return project;
    }
}