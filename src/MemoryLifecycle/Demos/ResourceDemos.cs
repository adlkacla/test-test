using MemoryLifecycle.Resources;

namespace MemoryLifecycle.Demos;

/// <summary>
/// Демонстрация финализатора, IDisposable и using (КТ №42).
/// </summary>
public static class ResourceDemos
{
    public static void RunAll()
    {
        RunFinalizerDemo();
        RunUsingDemo();
        RunForgottenDisposeDemo();
        // Каталог logs создаётся рядом с местом запуска и исключён в .gitignore.
        RunLogFileDemo("logs");
    }

    public static void RunFinalizerDemo()
    {
        Console.WriteLine("1. Финализатор: момент вызова не контролируется");
        CreateFakeResources();
        Console.WriteLine("Метод завершён, финализаторы ещё не выполнялись");
        // Только для демонстрации: без явной сборки финализаторы не обязаны
        // выполниться до конца программы. WaitForPendingFinalizers ждёт поток
        // финализации, иначе сообщения могли бы появиться позже или не появиться.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Console.WriteLine("После GC.Collect + WaitForPendingFinalizers");
    }

    public static void RunUsingDemo()
    {
        Console.WriteLine();
        Console.WriteLine("2. using: детерминированное освобождение");
        using (var resource = new ManagedResource())
        {
            Console.WriteLine("Работа внутри using-блока");
        }

        Console.WriteLine("Строка после using-блока");

        // using разворачивается в try/finally, поэтому Dispose вызывается
        // и при исключении — раньше, чем исключение дойдёт до catch.
        try
        {
            using var resource = new ManagedResource();
            Console.WriteLine("Внутри using выбрасывается исключение");
            throw new InvalidOperationException("сбой внутри using");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Исключение перехвачено после Dispose: {ex.Message}");
        }
    }

    public static void RunForgottenDisposeDemo()
    {
        Console.WriteLine();
        Console.WriteLine("3. Забытый Dispose: ресурс живёт до сборки");
        var weak = CreateManagedResourceWithoutDispose();
        Console.WriteLine($"Метод завершён, Dispose не вызывался, объект жив: {weak.IsAlive}");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Console.WriteLine($"После сборки (ресурс уже недостижим), объект жив: {weak.IsAlive}");
    }

    public static void RunLogFileDemo(string directory)
    {
        Console.WriteLine();
        Console.WriteLine("4. LogFile: корректный и некорректный сценарии");

        // Разные файлы для разных сценариев, чтобы не смешивать содержимое.
        var usingPath = PrepareFile(directory, "memory-lifecycle-using.log");
        var leakPath = PrepareFile(directory, "memory-lifecycle-leak.log");
        var declarationPath = PrepareFile(directory, "memory-lifecycle-declaration.log");

        Console.WriteLine("4.1. Корректный сценарий: using-блок");
        using (var log = new LogFile(usingPath))
        {
            log.Write("Корректная запись через using");
            Console.WriteLine("Запись выполнена внутри using-блока");
        }

        Console.WriteLine("После корректного using-блока");

        Console.WriteLine();
        Console.WriteLine("4.2. Некорректный сценарий: без using и без Dispose");
        CreateLogFileWithoutDispose(leakPath);
        Console.WriteLine("Метод с забытым Dispose завершён");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Console.WriteLine("После сборки: сработал финализатор");

        Console.WriteLine();
        Console.WriteLine("4.3. using-декларация без скобок");
        WriteWithUsingDeclaration(declarationPath);
        Console.WriteLine("После выхода из метода: файл уже закрыт");

        Console.WriteLine();
        Console.WriteLine("4.4. Повторный Dispose и запись после закрытия");
        var closed = new LogFile(declarationPath);
        closed.Dispose();
        Console.WriteLine("Повторный вызов Dispose():");
        closed.Dispose();
        Console.WriteLine("  повторного сообщения нет, исключения нет");
        try
        {
            closed.Write("Эта строка не должна попасть в файл");
        }
        catch (ObjectDisposedException ex)
        {
            Console.WriteLine($"  Write после Dispose: {ex.GetType().Name}");
        }

        Console.WriteLine();
        Console.WriteLine("Содержимое файлов (AutoFlush: все записи дошли до диска):");
        PrintFile(usingPath);
        PrintFile(leakPath);
        PrintFile(declarationPath);
    }

    private static void CreateFakeResources()
    {
        _ = new FakeResource();
        _ = new FakeResource();
    }

    private static WeakReference CreateManagedResourceWithoutDispose()
    {
        var resource = new ManagedResource();
        Console.WriteLine($"Ресурс создан, IsDisposed = {resource.IsDisposed}");
        var weak = new WeakReference(resource);
        // Удерживаем объект до конца метода: в Release JIT мог бы
        // считать его недостижимым раньше.
        GC.KeepAlive(resource);
        return weak;
    }

    private static void CreateLogFileWithoutDispose(string path)
    {
        // Ошибка намеренная: нет ни using, ни Dispose().
        var log = new LogFile(path);
        log.Write("Запись без using: Dispose не вызван");
        GC.KeepAlive(log);
    }

    private static void WriteWithUsingDeclaration(string path)
    {
        using var log = new LogFile(path);
        log.Write("Запись через using-декларацию");
        Console.WriteLine("Последняя строка метода: Dispose ещё не вызван");
        // Dispose() вызывается здесь — при выходе переменной log из области видимости.
    }

    private static string PrepareFile(string directory, string fileName)
    {
        // Журнал дописывается (append: true), поэтому старый файл удаляем,
        // чтобы в выводе было только содержимое текущего запуска.
        Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(directory, fileName);
        File.Delete(path);
        return path;
    }

    private static void PrintFile(string path)
    {
        var lines = File.ReadAllLines(path);
        Console.WriteLine($"  {System.IO.Path.GetFileName(path)}: {lines.Length} стр.");
        foreach (var line in lines)
        {
            Console.WriteLine($"    {line}");
        }
    }
}
