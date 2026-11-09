namespace MemoryLifecycle.Gc;

/// <summary>
/// Вспомогательный вывод состояния кучи по данным GC.GetGCMemoryInfo().
/// </summary>
public static class GcObserver
{
    public static void PrintMemoryInfo(string label)
    {
        // GetGCMemoryInfo описывает последнюю ЗАВЕРШЁННУЮ сборку, а не текущий момент.
        // Если сборок ещё не было, HeapSizeBytes и TotalCommittedBytes равны нулю —
        // это не ошибка измерения, а отсутствие данных.
        var info = GC.GetGCMemoryInfo();
        Console.WriteLine($"{label}:");
        Console.WriteLine($"  HeapSizeBytes:       {info.HeapSizeBytes / 1024d / 1024d:F2} МБ");
        Console.WriteLine($"  TotalCommittedBytes: {info.TotalCommittedBytes / 1024d / 1024d:F2} МБ");
        Console.WriteLine($"  MemoryLoadBytes:     {info.MemoryLoadBytes / 1024d / 1024d:F2} МБ");
        Console.WriteLine($"  PauseTimePercentage: {info.PauseTimePercentage:F2} %");
    }
}
