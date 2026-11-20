namespace MemoryLifecycle.Resources;

/// <summary>
/// Имитация соединения с базой данных (КТ №43, вариант 1):
/// управляемый журнал запросов и «нативный» идентификатор соединения.
/// </summary>
/// <remarks>
/// Класс не sealed: по условию варианта Dispose(bool) объявлен protected virtual,
/// чтобы наследник мог освободить свои ресурсы. Наследник переопределяет
/// Dispose(bool) и DisposeAsyncCore(), вызывает base-реализацию и не дублирует
/// публичные Dispose() и DisposeAsync().
/// </remarks>
public class DatabaseConnection : IDisposable, IAsyncDisposable
{
    private static int counter;

    // «Нативный» ресурс: в реальном коде это был бы дескриптор ОС.
    // Он должен закрываться всегда — и из Dispose(), и из финализатора.
    private readonly int connectionId = ++counter;

    // Управляемый ресурс: обычный объект в управляемой куче.
    // Из финализатора его трогать нельзя — он мог быть уже собран.
    private readonly List<string> queryLog = new();

    private bool disposed;

    public DatabaseConnection()
    {
        Console.WriteLine(
            $"[DatabaseConnection {connectionId}] соединение открыто");
    }

    public bool IsDisposed => disposed;

    public void Execute(string query)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        queryLog.Add(query);
        Console.WriteLine(
            $"[DatabaseConnection {connectionId}] запрос: {query}");
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        // Всё уже освобождено — убираем объект из очереди финализации,
        // чтобы он не прожил лишнее поколение и финализатор не сработал повторно.
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            // Асинхронно освобождаем управляемые ресурсы...
            await DisposeAsyncCore().ConfigureAwait(false);
        }
        finally
        {
            // ...а нативный закрываем через Dispose(false) в finally:
            // даже если асинхронная часть упадёт, соединение будет закрыто.
            // disposing: false — управляемые ресурсы уже освобождены выше.
            Dispose(disposing: false);
            GC.SuppressFinalize(this);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        // Проверка флага делает освобождение идемпотентным.
        if (disposed)
        {
            return;
        }

        // disposing == true  — вызов из Dispose(): можно трогать управляемые поля.
        // disposing == false — вызов из финализатора или после DisposeAsyncCore():
        //                      управляемые поля не трогаем.
        if (disposing)
        {
            queryLog.Clear();
            Console.WriteLine(
                $"[DatabaseConnection {connectionId}] журнал запросов очищен");
        }

        // Нативный ресурс закрывается всегда.
        disposed = true;
        Console.WriteLine(
            $"[DatabaseConnection {connectionId}] соединение закрыто");
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        // Без этой проверки повторный DisposeAsync() выполнил бы очистку повторно.
        if (disposed)
        {
            return;
        }

        // Имитация асинхронного закрытия (сетевой обмен с сервером БД).
        await Task.Delay(10).ConfigureAwait(false);
        queryLog.Clear();
        Console.WriteLine(
            $"[DatabaseConnection {connectionId}] журнал запросов очищен асинхронно");
    }

    // Последний шанс, если Dispose забыли вызвать: освобождаем только нативный ресурс.
    ~DatabaseConnection()
    {
        Dispose(disposing: false);
    }
}
