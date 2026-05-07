using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Service.FileStorage.Storage;

namespace Service.FileStorage.Controllers;

/// <summary>
/// Контроллер для взаимодействия с объектным хранилищем.
/// </summary>
/// <param name="storageService">Служба для работы с хранилищем</param>
/// <param name="logger">Логгер</param>
[ApiController]
[Route("api/files")]
public class FileStorageController(IFileStorageService storageService, ILogger<FileStorageController> logger) : ControllerBase
{
    /// <summary>
    /// Получает список хранящихся в бакете файлов.
    /// </summary>
    /// <returns>Список ключей файлов</returns>
    [HttpGet]
    [ProducesResponseType(200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<List<string>>> ListFiles()
    {
        logger.LogInformation("Вызван метод {method} контроллера {controller}", nameof(ListFiles), nameof(FileStorageController));
        try
        {
            var list = await storageService.GetFileList();
            logger.LogInformation("Получен список из {count} файлов из бакета", list.Count);
            return Ok(list);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при выполнении метода {method} контроллера {controller}", nameof(ListFiles), nameof(FileStorageController));
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Получает JSON-представление хранящегося в бакете файла.
    /// </summary>
    /// <param name="key">Ключ файла</param>
    /// <returns>JSON-представление файла</returns>
    [HttpGet("{key}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<JsonNode>> GetFile(string key)
    {
        logger.LogInformation("Вызван метод {method} контроллера {controller}", nameof(GetFile), nameof(FileStorageController));
        try
        {
            var node = await storageService.DownloadFile(key);
            logger.LogInformation("Получен JSON размером {size} байт", Encoding.UTF8.GetByteCount(node.ToJsonString()));
            return Ok(node);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при выполнении метода {method} контроллера {controller}", nameof(GetFile), nameof(FileStorageController));
            return BadRequest(ex.Message);
        }
    }
}
