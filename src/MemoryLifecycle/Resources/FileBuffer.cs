namespace MemoryLifecycle.Resources;

/// <summary>
/// Имитация файлового буфера: MemoryStream и нативный дескриптор.
/// </summary>
/// <remarks>
/// Класс sealed, поэтому Dispose(bool) объявлен private: наследников не будет.
/// </remarks>
public sealed class FileBuffer : IDisposable, IAsyncDisposable
{
    private static int counter;

    // «Нативный» дескриптор файла — закрывается всегда.
    private readonly int fileHandle = ++counter;

    // Управляемый ресурс. Nullable-поле обнуляется после освобождения,
    // чтобы повторный вызов не обращался к закрытому потоку.
    private MemoryStream? buffer = new();
    private bool disposed;

    public FileBuffer()
    {
        Console.WriteLine($"[FileBuffer {fileHandle}] буфер открыт");
    }

    public bool IsDisposed => disposed;

    public void Write(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        // После проверки disposed буфер гарантированно существует.
        buffer?.Write(data);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await DisposeAsyncCore().ConfigureAwait(false);
        }
        finally
        {
            Dispose(disposing: false);
            GC.SuppressFinalize(this);
        }
    }

    private void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            buffer?.Dispose();
            buffer = null;
            Console.WriteLine($"[FileBuffer {fileHandle}] буфер освобождён");
        }

        disposed = true;
        Console.WriteLine(
            $"[FileBuffer {fileHandle}] дескриптор {fileHandle} закрыт");
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (disposed)
        {
            return;
        }

        // Имитация асинхронного сброса буфера на диск.
        await Task.Delay(10).ConfigureAwait(false);
        buffer?.Dispose();
        buffer = null;
        Console.WriteLine(
            $"[FileBuffer {fileHandle}] буфер освобождён асинхронно");
    }

    ~FileBuffer()
    {
        Dispose(disposing: false);
    }
}
