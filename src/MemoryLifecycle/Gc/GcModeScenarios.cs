using System.Diagnostics;
using System.Globalization;
using System.Runtime;

namespace MemoryLifecycle.Gc;

/// <summary>
/// Многопоточная аллокация для сравнения Workstation и Server GC
/// (КТ №41, вариант 2 «Многопоточная аллокация»).
/// </summary>
public static class GcModeScenarios
{
    // Параметры одинаковы для обоих режимов: иначе сравнение теряет смысл.
    public const int DefaultWorkerCount = 4;
    public const int DefaultObjectsPerWorker = 100_000;
    private const int ObjectSize = 256;

    // Один короткий запуск может дать случайный результат (прогрев JIT,
    // пул потоков, фоновые процессы), поэтому делаем прогон-прогрев
    // и несколько замеров, а в выводах опираемся на среднее.
    public const int DefaultMeasurements = 5;

    private const string MachinePrefix = "#GC-MODES;";

    public static void RunParallelAllocation(
        int workerCount = DefaultWorkerCount,
        int objectsPerWorker = DefaultObjectsPerWorker,
        int measurements = DefaultMeasurements)
    {
        Console.WriteLine("Многопоточная аллокация");
        Console.WriteLine($"Server GC: {GCSettings.IsServerGC}");
        Console.WriteLine($"Latency mode: {GCSettings.LatencyMode}");
        Console.WriteLine($"Логических процессоров: {Environment.ProcessorCount}");
        Console.WriteLine(
            $"Потоков: {workerCount}, объектов на поток: {objectsPerWorker:N0}, " +
            $"размер объекта: {ObjectSize} байт");
        WarnIfMeasurementInvalid();
        MeasureForcedGen2Pause();
        Console.WriteLine();

        var report = Measure(workerCount, objectsPerWorker, measurements);

        Console.WriteLine($"Прогрев: 1 запуск (в замеры не входит), замеров: {measurements}");
        Console.WriteLine(new string('-', 70));
        Console.WriteLine("{0,-9}{1,12}{2,7}{3,7}{4,7}{5,12}{6,16}",
            "Замер", "Время, мс", "Gen0", "Gen1", "Gen2", "Alloc, МБ", "Пауза GC, мс");
        Console.WriteLine(new string('-', 70));
        for (var i = 0; i < report.Runs.Count; i++)
        {
            var run = report.Runs[i];
            Console.WriteLine("{0,-9}{1,12:F2}{2,7}{3,7}{4,7}{5,12:F2}{6,16:F2}",
                i + 1, run.ElapsedMs, run.Delta.Gen0, run.Delta.Gen1, run.Delta.Gen2,
                run.Delta.AllocatedMegabytes, run.PauseMs);
        }

        Console.WriteLine(new string('-', 70));
        Console.WriteLine("{0,-9}{1,12:F2}{2,7:F1}{3,7:F1}{4,7:F1}{5,12:F2}{6,16:F2}",
            "Среднее", report.AverageElapsedMs, report.AverageGen0, report.AverageGen1,
            report.AverageGen2, report.AverageAllocatedMegabytes, report.AveragePauseMs);
        Console.WriteLine();
        Console.WriteLine($"Время: {report.AverageElapsedMs:F2} мс " +
                          $"(min {report.MinElapsedMs:F2}, max {report.MaxElapsedMs:F2})");
        Console.WriteLine(
            $"Gen0: {report.AverageGen0:F1}, Gen1: {report.AverageGen1:F1}, Gen2: {report.AverageGen2:F1} (за один замер)");
        Console.WriteLine($"Аллоцировано: {report.AverageAllocatedMegabytes:F2} МБ (за один замер)");
        Console.WriteLine($"Контрольная сумма: {report.Checksum:N0}");
        Console.WriteLine();
    }

    public static void MeasureForcedGen2Pause()
    {
        Console.WriteLine(
            $"Принудительный блокирующий Gen2: {ForcedGen2PauseMs():F2} мс");
    }

    /// <summary>
    /// Запускает текущую программу дважды — с DOTNET_gcServer=0 и DOTNET_gcServer=1 —
    /// и печатает сравнение. Режим GC выбирается при старте процесса, поэтому
    /// переключить его внутри одного процесса нельзя: нужны два отдельных запуска.
    /// </summary>
    public static int RunComparison()
    {
        Console.WriteLine("Сравнение Workstation GC и Server GC");
        Console.WriteLine("Каждый режим запускается отдельным процессом этой же сборки:");
        Console.WriteLine("  DOTNET_gcServer=0 MemoryLifecycle gc-modes --machine");
        Console.WriteLine("  DOTNET_gcServer=1 MemoryLifecycle gc-modes --machine");
        WarnIfMeasurementInvalid();
        Console.WriteLine();

        var workstation = RunChild(serverGc: false);
        var server = RunChild(serverGc: true);
        if (workstation is null || server is null)
        {
            Console.WriteLine("Не удалось получить результат дочернего процесса.");
            return 1;
        }

        Console.WriteLine(
            $"Логических процессоров: {Environment.ProcessorCount}; " +
            $"замеров в каждом режиме: {workstation.Measurements} (после прогрева)");
        Console.WriteLine(new string('-', 74));
        Console.WriteLine("{0,-38}{1,18}{2,18}", "Показатель", "Workstation", "Server");
        Console.WriteLine(new string('-', 74));
        PrintRow("GCSettings.IsServerGC", workstation.IsServerGc, server.IsServerGc);
        PrintRow("Latency mode", workstation.LatencyMode, server.LatencyMode);
        PrintRow("Потоков / объектов на поток",
            $"{workstation.Workers} / {workstation.ObjectsPerWorker:N0}",
            $"{server.Workers} / {server.ObjectsPerWorker:N0}");
        PrintRow("Принудительный блокирующий Gen2, мс",
            F(workstation.ForcedGen2PauseMs), F(server.ForcedGen2PauseMs));
        PrintRow("Время аллокации, мс (среднее)",
            F(workstation.AverageElapsedMs), F(server.AverageElapsedMs));
        PrintRow("Время аллокации, мс (min / max)",
            $"{F(workstation.MinElapsedMs)} / {F(workstation.MaxElapsedMs)}",
            $"{F(server.MinElapsedMs)} / {F(server.MaxElapsedMs)}");
        PrintRow("Gen0 / Gen1 / Gen2 (среднее)",
            $"{F1(workstation.Gen0)} / {F1(workstation.Gen1)} / {F1(workstation.Gen2)}",
            $"{F1(server.Gen0)} / {F1(server.Gen1)} / {F1(server.Gen2)}");
        PrintRow("Аллоцировано за замер, МБ",
            F(workstation.AllocatedMegabytes), F(server.AllocatedMegabytes));
        PrintRow("Пауза GC за замер, мс (среднее)",
            F(workstation.PauseMs), F(server.PauseMs));
        PrintRow("Контрольная сумма",
            workstation.Checksum.ToString("N0"), server.Checksum.ToString("N0"));
        Console.WriteLine(new string('-', 74));

        var sameWork = workstation.Checksum == server.Checksum
            && Math.Abs(workstation.AllocatedMegabytes - server.AllocatedMegabytes) < 0.5;
        Console.WriteLine(sameWork
            ? "Контрольные суммы и объём аллокаций совпали: оба режима выполнили одинаковую работу."
            : "ВНИМАНИЕ: объём работы в режимах различается, сравнение некорректно.");
        Console.WriteLine(
            "Больше сборок Gen0 в Server GC — не деградация: у каждой кучи свой бюджет Gen0,");
        Console.WriteLine(
            "поэтому сборки инициируются по-другому. Вывод делается по времени, паузам и аллокациям.");
        return 0;
    }

    /// <summary>
    /// Режим для дочернего процесса: те же замеры, но результат печатается
    /// одной машиночитаемой строкой для RunComparison.
    /// </summary>
    public static void RunMachineReadable(
        int workerCount = DefaultWorkerCount,
        int objectsPerWorker = DefaultObjectsPerWorker,
        int measurements = DefaultMeasurements)
    {
        var forcedPause = ForcedGen2PauseMs();
        var report = Measure(workerCount, objectsPerWorker, measurements);
        var c = CultureInfo.InvariantCulture;
        Console.WriteLine(string.Join(';',
            MachinePrefix.TrimEnd(';'),
            GCSettings.IsServerGC.ToString(),
            GCSettings.LatencyMode.ToString(),
            workerCount.ToString(c),
            objectsPerWorker.ToString(c),
            measurements.ToString(c),
            forcedPause.ToString("R", c),
            report.AverageElapsedMs.ToString("R", c),
            report.MinElapsedMs.ToString("R", c),
            report.MaxElapsedMs.ToString("R", c),
            report.AverageGen0.ToString("R", c),
            report.AverageGen1.ToString("R", c),
            report.AverageGen2.ToString("R", c),
            report.AverageAllocatedMegabytes.ToString("R", c),
            report.AveragePauseMs.ToString("R", c),
            report.Checksum.ToString(c)));
    }

    private static AllocationReport Measure(int workerCount, int objectsPerWorker, int measurements)
    {
        // Прогрев: JIT-компиляция лямбды, запуск потоков пула. В замеры не входит.
        _ = AllocateInParallel(workerCount, Math.Max(1, objectsPerWorker / 10));

        var runs = new List<AllocationRun>(measurements);
        long checksum = 0;
        for (var i = 0; i < measurements; i++)
        {
            var before = GcSnapshot.Capture();
            var pauseBefore = GC.GetTotalPauseDuration();
            var stopwatch = Stopwatch.StartNew();

            checksum = AllocateInParallel(workerCount, objectsPerWorker);

            stopwatch.Stop();
            var pause = GC.GetTotalPauseDuration() - pauseBefore;
            var delta = GcSnapshot.Capture().Diff(before);
            runs.Add(new AllocationRun(
                stopwatch.Elapsed.TotalMilliseconds, delta, pause.TotalMilliseconds));
        }

        return new AllocationReport(runs, checksum);
    }

    private static long AllocateInParallel(int workerCount, int objectsPerWorker)
    {
        var tasks = new Task<long>[workerCount];
        for (var worker = 0; worker < workerCount; worker++)
        {
            tasks[worker] = Task.Run(() =>
            {
                // Контрольная сумма не даёт JIT выбросить аллокации и доказывает,
                // что в обоих режимах выполнена одна и та же работа.
                long checksum = 0;
                for (var i = 0; i < objectsPerWorker; i++)
                {
                    var buffer = new byte[ObjectSize];
                    buffer[0] = (byte)i;
                    checksum += buffer[0];
                }

                return checksum;
            });
        }

        var checksums = Task.WhenAll(tasks).GetAwaiter().GetResult();
        return checksums.Sum();
    }

    private static double ForcedGen2PauseMs()
    {
        // Только для диагностики: полная блокирующая сборка останавливает все
        // управляемые потоки, в рабочем коде так делать нельзя.
        var stopwatch = Stopwatch.StartNew();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private static ModeSummary? RunChild(bool serverGc)
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Не удалось определить путь к процессу.");
        var startInfo = new ProcessStartInfo(processPath)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        // Под «dotnet MemoryLifecycle.dll» процессом является dotnet.exe,
        // тогда первым аргументом нужно передать путь к сборке.
        if (Path.GetFileNameWithoutExtension(processPath)
            .Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(typeof(GcModeScenarios).Assembly.Location);
        }

        startInfo.ArgumentList.Add("gc-modes");
        startInfo.ArgumentList.Add("--machine");
        startInfo.Environment["DOTNET_gcServer"] = serverGc ? "1" : "0";

        Console.WriteLine($"Запуск дочернего процесса: DOTNET_gcServer={(serverGc ? 1 : 0)} ...");
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Не удалось запустить дочерний процесс.");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var line = output
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(l => l.StartsWith(MachinePrefix, StringComparison.Ordinal));
        return line is null ? null : ModeSummary.Parse(line);
    }

    private static void WarnIfMeasurementInvalid()
    {
#if DEBUG
        Console.WriteLine("ВНИМАНИЕ: сборка Debug. Сравнивать режимы GC нужно в Release (-c Release).");
#endif
        if (Debugger.IsAttached)
        {
            Console.WriteLine("ВНИМАНИЕ: подключён отладчик, измерения времени недостоверны.");
        }
    }

    private static void PrintRow(string name, object left, object right) =>
        Console.WriteLine("{0,-38}{1,18}{2,18}", name, left, right);

    private static string F(double value) => value.ToString("F2");

    private static string F1(double value) => value.ToString("F1");

    private readonly record struct AllocationRun(double ElapsedMs, GcDelta Delta, double PauseMs);

    private sealed record AllocationReport(IReadOnlyList<AllocationRun> Runs, long Checksum)
    {
        public double AverageElapsedMs => Runs.Average(r => r.ElapsedMs);
        public double MinElapsedMs => Runs.Min(r => r.ElapsedMs);
        public double MaxElapsedMs => Runs.Max(r => r.ElapsedMs);
        public double AverageGen0 => Runs.Average(r => r.Delta.Gen0);
        public double AverageGen1 => Runs.Average(r => r.Delta.Gen1);
        public double AverageGen2 => Runs.Average(r => r.Delta.Gen2);
        public double AverageAllocatedMegabytes => Runs.Average(r => r.Delta.AllocatedMegabytes);
        public double AveragePauseMs => Runs.Average(r => r.PauseMs);
    }

    private sealed record ModeSummary(
        bool IsServerGc,
        string LatencyMode,
        int Workers,
        int ObjectsPerWorker,
        int Measurements,
        double ForcedGen2PauseMs,
        double AverageElapsedMs,
        double MinElapsedMs,
        double MaxElapsedMs,
        double Gen0,
        double Gen1,
        double Gen2,
        double AllocatedMegabytes,
        double PauseMs,
        long Checksum)
    {
        public static ModeSummary Parse(string line)
        {
            var p = line.Split(';');
            var c = CultureInfo.InvariantCulture;
            return new ModeSummary(
                bool.Parse(p[1]), p[2],
                int.Parse(p[3], c), int.Parse(p[4], c), int.Parse(p[5], c),
                double.Parse(p[6], c), double.Parse(p[7], c), double.Parse(p[8], c),
                double.Parse(p[9], c), double.Parse(p[10], c), double.Parse(p[11], c),
                double.Parse(p[12], c), double.Parse(p[13], c), double.Parse(p[14], c),
                long.Parse(p[15], c));
        }
    }
}
