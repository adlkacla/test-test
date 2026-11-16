namespace MemoryLifecycle.Resources;

/// <summary>
/// Учебная обёртка над файлом журнала (КТ №42, вариант 2 «Лог-файл»).
/// Финализатор только сообщает об утечке, а не пытается закрыть управляемый StreamWriter.
/// </summary>
public sealed class LogFile : IDisposable
{
    private static int counter;
    private readonly int id = ++counter;
    private readonly StreamWriter writer;
    private bool disposed;

    public LogFile(string path)
    {
        Path = path;
        // AutoFlush: каждая запись сразу уходит в файл, иначе при забытом Dispose
        // данные остались бы в буфере StreamWriter и вывод выглядел бы противоречиво.
        writer = new StreamWriter(path, append: true) { AutoFlush = true };
        Console.WriteLine($"[LogFile {id}] файл открыт: {path}");
    }

    public string Path { get; }

    public bool IsDisposed => disposed;

    public void Write(string message)
    {
        // В закрытый файл писать нельзя: сообщаем об этом явным исключением.
        ObjectDisposedException.ThrowIf(disposed, this);
        writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [LogFile {id}] {message}");
    }

    public void Dispose()
    {
        // Повторный Dispose молча возвращается: без повторного закрытия и без исключения.
        if (disposed)
        {
            return;
        }

        writer.Dispose();
        disposed = true;
        // Файл уже закрыт явно — финализатор больше не нужен. Без этого вызова
        // финализатор позже вывел бы ложное сообщение об утечке.
        GC.SuppressFinalize(this);
        Console.WriteLine($"[LogFile {id}] файл закрыт");
    }

    // Финализатор срабатывает, только если Dispose забыли вызвать.
    // writer здесь не трогаем: это управляемый объект, к моменту финализации
    // он мог быть уже финализирован сам. В учебной модели достаточно сообщения.
    ~LogFile()
    {
        Console.WriteLine($"[LogFile {id}] финализатор! — файл не был закрыт явно");
    }
}
