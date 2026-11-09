using System.Diagnostics;

namespace MemoryLifecycle.Gc;

/// <summary>
/// Сценарии, создающие разное давление на управляемую кучу
/// (КТ №40, вариант 2 «Измерение давления»).
/// </summary>
public static class PressureScenarios
{
    // По условию варианта 2: 100 000 мелких объектов.
    private const int SmallObjectCount = 100_000;
    private const int SmallObjectSize = 100;

    // 100 000 байт больше порога LOH (85 000 байт), поэтому массив сразу
    // размещается в Large Object Heap, минуя Gen0.
    private const int LargeObjectCount = 10;
    private const int LargeObjectSize = 100_000;

    // 512 байт заметно меньше порога LOH: объекты рождаются в Gen0
    // и продвигаются в старшие поколения, только переживая сборки.
    private const int LongLivedCount = 1_000;
    private const int LongLivedSize = 512;

    // Статический список — корень GC: всё, что в нём лежит, живёт
    // до конца процесса. Так моделируются долгоживущие данные (кэш, справочник).
    private static readonly List<byte[]> LongLivedStore = new(LongLivedCount);

    public static ScenarioResult RunSmallObjects(int count = SmallObjectCount)
    {
        var before = GcSnapshot.Capture();
        var stopwatch = Stopwatch.StartNew();

        // checksum не даёт JIT-компилятору выбросить цикл как бесполезный.
        // Ссылки на массивы не сохраняются: каждый массив становится мусором
        // на следующей итерации и умирает молодым в Gen0.
        long checksum = 0;
        for (var i = 0; i < count; i++)
        {
            var buffer = new byte[SmallObjectSize];
            buffer[0] = (byte)i;
            checksum += buffer[0];
        }

        stopwatch.Stop();
        var after = GcSnapshot.Capture();
        GC.KeepAlive(checksum);

        // Почему цифры такие: 100 000 * 128 байт (100 байт данных + 24 байта заголовка
        // массива, с выравниванием) ≈ 12,2 МБ аллокаций. Этот объём больше бюджета Gen0, поэтому
        // сборщик запускает одну или несколько сборок Gen0. Живых объектов почти нет,
        // переживать сборку нечему, поэтому до Gen2 дело не доходит: Gen2 = 0.
        return new ScenarioResult("Мелкие объекты (byte[100])",
            stopwatch.Elapsed, after.Diff(before));
    }

    public static ScenarioResult RunLargeObjects(int count = LargeObjectCount)
    {
        var before = GcSnapshot.Capture();
        var stopwatch = Stopwatch.StartNew();

        var retained = new List<byte[]>(count);
        for (var i = 0; i < count; i++)
        {
            retained.Add(new byte[LargeObjectSize]);
        }

        stopwatch.Stop();
        var after = GcSnapshot.Capture();
        GC.KeepAlive(retained);

        // Почему цифры такие: 10 * 100 000 байт ≈ 0,95 МБ аллокаций, но Gen0 не растёт —
        // объекты больше 85 000 байт попадают прямо в LOH и бюджет Gen0 не расходуют.
        // LOH логически относится к Gen2, однако счётчик Gen2 увеличивается только
        // при СБОРКЕ Gen2. Бюджета LOH (по умолчанию несколько МБ) хватило,
        // сборка не потребовалась, поэтому Gen2 = 0.
        return new ScenarioResult("Крупные объекты (byte[100 000])",
            stopwatch.Elapsed, after.Diff(before));
    }

    public static ScenarioResult RunLongLived(int count = LongLivedCount)
    {
        var before = GcSnapshot.Capture();
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < count; i++)
        {
            LongLivedStore.Add(new byte[LongLivedSize]);
        }

        stopwatch.Stop();
        var after = GcSnapshot.Capture();

        // Почему цифры такие: 1 000 * 536 байт ≈ 0,5 МБ — меньше бюджета Gen0, поэтому
        // сборки во время сценария обычно не происходит (Gen0 = 0, Gen2 = 0).
        // Разница с мелкими объектами не в числе сборок, а в судьбе объектов:
        // эти массивы достижимы из статического поля, поэтому при следующих сборках
        // они не умрут, а будут продвигаться Gen0 -> Gen1 -> Gen2. Это видно после
        // принудительной сборки: HeapSizeBytes остаётся больше нуля.
        return new ScenarioResult("Удерживаемые объекты (byte[512])",
            stopwatch.Elapsed, after.Diff(before));
    }

    public static ScenarioResult RunForcedGen2()
    {
        var before = GcSnapshot.Capture();
        var stopwatch = Stopwatch.StartNew();

        // Только для учебной демонстрации: в рабочем коде GC.Collect не вызывают,
        // момент сборки выбирает сам сборщик.
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        stopwatch.Stop();
        var after = GcSnapshot.Capture();

        // Почему цифры такие: полная сборка Gen2 включает сборку Gen0 и Gen1,
        // поэтому все три счётчика растут на единицу. Аллокаций почти нет —
        // сборка не выделяет управляемую память.
        return new ScenarioResult("Принудительный Gen2 (blocking)",
            stopwatch.Elapsed, after.Diff(before));
    }

    public static void RunAll()
    {
        // Точка отсчёта всей работы (шаг 1 варианта 2).
        var allocatedAtStart = GC.GetTotalAllocatedBytes(precise: true);

        Console.WriteLine("Сценарии давления на GC");
        GcObserver.PrintMemoryInfo("Состояние кучи до экспериментов");
        Console.WriteLine(new string('-', 92));
        Console.WriteLine(
            "{0,-34}{1,10}{2,6}{3,6}{4,6}{5,11}{6,12}",
            "Сценарий", "Время, мс", "Gen0", "Gen1", "Gen2",
            "Alloc, МБ", "Heap Δ, МБ");
        Console.WriteLine(new string('-', 92));

        // Каждый сценарий — отдельный метод со своими снимками до и после,
        // чтобы эффекты разных нагрузок не смешивались в одном измерении.
        var results = new[]
        {
            RunSmallObjects(),
            RunLargeObjects(),
            RunLongLived(),
            RunForcedGen2()
        };

        foreach (var result in results)
        {
            Console.WriteLine(
                "{0,-34}{1,10:F2}{2,6}{3,6}{4,6}{5,11:F2}{6,12:F2}",
                result.Name,
                result.Elapsed.TotalMilliseconds,
                result.Delta.Gen0,
                result.Delta.Gen1,
                result.Delta.Gen2,
                result.Delta.AllocatedMegabytes,
                result.Delta.HeapDeltaMegabytes);
        }

        Console.WriteLine(new string('-', 92));

        var allocatedTotal = GC.GetTotalAllocatedBytes(precise: true) - allocatedAtStart;
        Console.WriteLine(
            $"Всего аллоцировано с начала работы: {allocatedTotal / 1024d / 1024d:F2} МБ");
        Console.WriteLine(
            $"Удерживается в статическом списке: {LongLivedStore.Count} массивов");
        GcObserver.PrintMemoryInfo("Состояние кучи после экспериментов");
        Console.WriteLine(
            "Значения зависят от машины, режима GC и момента запуска. " +
            "Важна не абсолютная величина, а соотношение счётчиков.");
    }
}

public readonly record struct ScenarioResult(
    string Name, TimeSpan Elapsed, GcDelta Delta);
