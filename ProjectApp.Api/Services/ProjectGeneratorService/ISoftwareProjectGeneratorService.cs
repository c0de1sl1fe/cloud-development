using ProjectApp.Domain.Entities;

namespace ProjectApp.Api.Services.ProjectGeneratorService;

/// <summary>
/// Сервис получения программного проекта
/// </summary>
public interface ISoftwareProjectGeneratorService
{
    public Task<SoftwareProject> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}