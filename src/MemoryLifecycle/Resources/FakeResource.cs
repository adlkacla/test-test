namespace MemoryLifecycle.Resources;

/// <summary>
/// Ресурс без IDisposable: освобождается только финализатором.
/// </summary>
public sealed class FakeResource
{
    private static int counter;
    private readonly int id = ++counter;

    public FakeResource()
    {
        Console.WriteLine($"[FakeResource {id}] открыт");
    }

    // Финализатор вызывает сборщик мусора в потоке финализации, когда объект
    // стал недостижим. Момент вызова не определён — освободить ресурс
    // досрочно у такого класса невозможно.
    ~FakeResource()
    {
        Console.WriteLine($"[FakeResource {id}] финализатор");
    }
}
