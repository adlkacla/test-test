using MemoryLifecycle.Resources;

namespace MemoryLifecycle.Demos;

/// <summary>
/// Демонстрация полного паттерна Dispose и IAsyncDisposable (КТ №43).
/// </summary>
public static class DisposeDemos
{
    public static async Task RunAllAsync()
    {
        await RunSyncAndAsyncDemoAsync();
        await RunFileBufferDemoAsync();
        await RunRepeatedDisposeDemoAsync();
    }

    public static async Task RunSyncAndAsyncDemoAsync()
    {
        Console.WriteLine("1. Синхронный сценарий using");
        using (var connection = new DatabaseConnection())
        {
            connection.Execute("SELECT * FROM Students");
        }

        Console.WriteLine();
        Console.WriteLine("2. Асинхронный сценарий await using");
        await using (var connection = new DatabaseConnection())
        {
            connection.Execute("SELECT * FROM Topics");
            await Task.Delay(1);
        }

        VerifySuppressFinalize();

        Console.WriteLine();
        Console.WriteLine("4. Финализатор без Dispose");
        CreateConnectionWithoutDispose();
        // Только для демонстрации: принудительная сборка, чтобы увидеть финализатор.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Console.WriteLine(
            "После сборки: финализатор освободил только нативный ресурс");
    }

    public static async Task RunFileBufferDemoAsync()
    {
        Console.WriteLine();
        Console.WriteLine("5. FileBuffer: using и await using");

        using (var buffer = new FileBuffer())
        {
            buffer.Write(new byte[] { 1, 2, 3, 4 });
        }

        await using (var buffer = new FileBuffer())
        {
            buffer.Write(new byte[] { 5, 6, 7 });
            await Task.Delay(1);
        }
    }

    public static async Task RunRepeatedDisposeDemoAsync()
    {
        Console.WriteLine();
        Console.WriteLine("6. Повторное освобождение: Dispose(), Dispose(), DisposeAsync()");
        var connection = new DatabaseConnection();
        connection.Execute("SELECT COUNT(*) FROM Students");
        connection.Dispose();
        Console.WriteLine("Повторные вызовы Dispose() и DisposeAsync():");
        connection.Dispose();
        await connection.DisposeAsync();
        Console.WriteLine("  повторного освобождения нет, исключения нет");
        try
        {
            connection.Execute("SELECT 1");
        }
        catch (ObjectDisposedException ex)
        {
            Console.WriteLine($"  Execute после Dispose: {ex.GetType().Name}");
        }
    }

    public static void VerifySuppressFinalize()
    {
        Console.WriteLine();
        Console.WriteLine("3. Проверка GC.SuppressFinalize после using");
        using (var connection = new DatabaseConnection())
        {
            connection.Execute("SELECT 1");
        }

        // Если бы Dispose() не вызывал GC.SuppressFinalize, финализатор после этой
        // сборки ещё раз вывел бы «соединение закрыто». Тишина — доказательство.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Console.WriteLine("После сборки: сообщение финализатора отсутствует");
    }

    private static void CreateConnectionWithoutDispose()
    {
        // Ошибка намеренная: соединение не освобождается.
        var connection = new DatabaseConnection();
        connection.Execute("UPDATE Students SET Name = 'Test'");
    }
}
