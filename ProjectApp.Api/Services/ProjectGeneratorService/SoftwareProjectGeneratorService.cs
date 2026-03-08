using Bogus;
using ProjectApp.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace ProjectApp.Api.Services.ProjectGeneratorService;

public class SoftwareProjectGeneratorService(
    IDistributedCache cache,
    IConfiguration configuration,
    ILogger<SoftwareProjectGeneratorService> logger)
{
    private readonly int _expirationMinutes = configuration.GetValue("CacheSettings:ExpirationMinutes", 10);

    public async Task<SoftwareProject> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"software-project-{id}";

            logger.LogInformation("Attempting to retrieve software project {Id} from cache", id);

            var cachedData = await cache.GetStringAsync(cacheKey, cancellationToken);

            if (!string.IsNullOrEmpty(cachedData))
            {
                var deserializedProject = JsonSerializer.Deserialize<SoftwareProject>(cachedData);

                if (deserializedProject != null)
                {
                    logger.LogInformation("Software project {Id} found in cache", id);
                    return deserializedProject;
                }

                logger.LogWarning("Project {Id} was found in cache but could not be deserialized. Generating a new one", id);
            }

            logger.LogInformation("Project {Id} not found in cache, generating a new one", id);

            var project = GenerateProject(id);

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

            return project;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while retrieving/generating project {Id}", id);
            throw;
        }
    }

    /// <summary>
    /// Генерация программного проекта с указанным ID
    /// </summary>
    private static SoftwareProject GenerateProject(int id)
    {
        var faker = new Faker<SoftwareProject>("ru")
            .RuleFor(p => p.Id, _ => id)
            .RuleFor(p => p.ProjectName, f =>
                $"{f.Commerce.ProductName()} {f.Hacker.Noun()} {f.Finance.AccountName()} {f.Lorem.Word()}")
            .RuleFor(p => p.Customer, f => f.Company.CompanyName())
            .RuleFor(p => p.ProjectManager, f =>
                $"{f.Name.LastName()} {f.Name.FirstName()} {f.Name.FirstName()}")
            .RuleFor(p => p.StartDate, f => f.Date.PastDateOnly(3))
            .RuleFor(p => p.PlannedEndDate, (f, p) => p.StartDate.AddDays(f.Random.Int(30, 730)))
            .RuleFor(p => p.Budget, f => Math.Round(f.Finance.Amount(500000, 50000000), 2))
            .RuleFor(p => p.ActualEndDate, (f, p) =>
            {
                var isCompleted = f.Random.Bool(0.4f);
                if (!isCompleted) return null;

                var startDateTime = p.StartDate.ToDateTime(TimeOnly.MinValue);
                var minDate = startDateTime.AddDays(1);
                var maxDate = DateTime.Now;

                // Защита от случая, когда проект начался сегодня
                if (minDate > maxDate)
                {
                    return DateOnly.FromDateTime(minDate);
                }

                var endDate = f.Date.Between(minDate, maxDate);
                return DateOnly.FromDateTime(endDate);
            })
            .RuleFor(p => p.CompletionPercentage, (f, p) => p.ActualEndDate.HasValue ? 100 : f.Random.Int(0, 99))
            .RuleFor(p => p.ActualCost, (f, p) =>
            {
                // Затраты пропорциональны бюджету и проценту выполнения
                // Минимум 10% от пропорции, максимум 120% от пропорции
                var minFactor = Math.Max(0.1m, p.CompletionPercentage / 100m * 0.8m);
                var maxFactor = Math.Min(1.2m, p.CompletionPercentage / 100m * 1.2m);

                var costFactor = f.Random.Decimal(minFactor, maxFactor);
                return Math.Round(p.Budget * costFactor, 2);
            });

        return faker.Generate();
    }
}