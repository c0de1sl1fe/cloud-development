using ProjectApp.Api.Services.ProjectGeneratorService;
using ProjectApp.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ProjectApp.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProjectController(ISoftwareProjectGeneratorService generatorService, ILogger<ProjectController> logger) : ControllerBase
{
    /// <summary>
    /// Получить программный проект по ID, если не найден в кэше — сгенерировать новый
    /// </summary>
    /// <param name="id">ID проекта</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Программный проект</returns>
    [HttpGet]
    public async Task<ActionResult<SoftwareProject>> GetById([FromQuery] int id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Received request to retrieve/generate project {Id}", id);

        var project = await generatorService.GetByIdAsync(id, cancellationToken);

        return Ok(project);
    }
}
