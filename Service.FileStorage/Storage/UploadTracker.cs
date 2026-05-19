namespace Service.FileStorage.Storage;

/// <summary>
/// Синглтон-счётчик успешных загрузок файлов в объектное хранилище.
/// </summary>
public class UploadTracker
{
    private int _count;

    public int Count => _count;

    public void Increment() => Interlocked.Increment(ref _count);
}
