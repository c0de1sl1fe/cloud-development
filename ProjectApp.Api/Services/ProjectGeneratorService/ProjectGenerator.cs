using Bogus;
using ProjectApp.Domain.Entities;

namespace ProjectApp.Api.Services.ProjectGeneratorService;

/// <summary>
/// Генератор случайных программных проектов с использованием Bogus
/// </summary>
public class ProjectGenerator
{
    private readonly Faker<SoftwareProject> _faker;

    public ProjectGenerator()
    {
        _faker = new Faker<SoftwareProject>("ru")
            .RuleFor(p => p.Id, f => f.IndexFaker + 1)
            .RuleFor(p => p.ProjectName, f =>
                f.PickRandom(
                    f.Commerce.ProductName() + " " + f.Hacker.Abbreviation(),
                    "Project " + f.Hacker.Noun(),
                    f.Finance.AccountName() + " System",
                    f.Lorem.Word() + "-" + f.Lorem.Word()
                ))
            .RuleFor(p => p.Customer, f => f.Company.CompanyName())
            .RuleFor(p => p.ProjectManager, f => f.Name.FullName())
            .RuleFor(p => p.StartDate, f => f.Date.PastDateOnly(3))
            .RuleFor(p => p.PlannedEndDate, (f, p) => p.StartDate.AddDays(f.Random.Int(30, 730)))
            .RuleFor(p => p.Budget, f => Math.Round(f.Finance.Amount(500000, 50000000), 2))
            .RuleFor(p => p.ActualEndDate, (f, p) =>
            {
                var isCompleted = f.Random.Bool(0.4f);
                if (!isCompleted)
                {
                    return null;
                }

                var startDateTime = p.StartDate.ToDateTime(TimeOnly.MinValue);
                var minDate = startDateTime.AddDays(1);
                var maxDate = DateTime.Now;

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
                var minFactor = Math.Max(0.1m, p.CompletionPercentage / 100m * 0.8m);
                var maxFactor = Math.Min(1.2m, p.CompletionPercentage / 100m * 1.2m);

                var costFactor = f.Random.Decimal(minFactor, maxFactor);
                return Math.Round(p.Budget * costFactor, 2);
            });
    }

    /// <summary>
    /// Генерирует один случайный программный проект
    /// </summary>
    public SoftwareProject Generate() => _faker.Generate();
}