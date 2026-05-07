using System.Text.Json.Nodes;

namespace Service.FileStorage.Storage;

/// <summary>
/// Интерфейс службы для манипуляции файлами в объектном хранилище.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Отправляет файл в хранилище.
    /// </summary>
    /// <param name="fileData">Строковая репрезентация сохраняемого файла</param>
    public Task<bool> UploadFile(string fileData);

    /// <summary>
    /// Получает список всех файлов из хранилища.
    /// </summary>
    /// <returns>Список ключей файлов</returns>
    public Task<List<string>> GetFileList();

    /// <summary>
    /// Получает строковую репрезентацию файла из хранилища.
    /// </summary>
    /// <param name="key">Ключ файла в бакете</param>
    /// <returns>JSON-репрезентация прочтенного файла</returns>
    public Task<JsonNode> DownloadFile(string key);

    /// <summary>
    /// Создает бакет при необходимости.
    /// </summary>
    public Task EnsureBucketExists();
}
