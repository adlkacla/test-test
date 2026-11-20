using System.Globalization;
using MemoryLifecycle.Demos;
using MemoryLifecycle.Gc;

Console.OutputEncoding = System.Text.Encoding.UTF8;
// Единый формат чисел (точка как разделитель) на любой машине,
// чтобы вывод совпадал с таблицами в README.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
var options = args.Skip(1).Select(a => a.ToLowerInvariant()).ToHashSet();

switch (command)
{
    case "gc-pressure":
        PressureScenarios.RunAll();
        break;
    case "gc-modes" when options.Contains("--machine"):
        GcModeScenarios.RunMachineReadable();
        break;
    case "gc-modes" when options.Contains("--compare"):
        return GcModeScenarios.RunComparison();
    case "gc-modes":
        GcModeScenarios.RunParallelAllocation();
        break;
    case "resources":
        ResourceDemos.RunAll();
        break;
    case "dispose":
        await DisposeDemos.RunAllAsync();
        break;
    case "all":
        PrintHeader("КТ №40. Поколения и давление на кучу (gc-pressure)");
        PressureScenarios.RunAll();
        PrintHeader("КТ №41. Многопоточная аллокация (gc-modes)");
        GcModeScenarios.RunParallelAllocation();
        PrintHeader("КТ №42. Финализатор, IDisposable, using (resources)");
        ResourceDemos.RunAll();
        PrintHeader("КТ №43. Паттерн Dispose, IAsyncDisposable (dispose)");
        await DisposeDemos.RunAllAsync();
        break;
    default:
        Console.WriteLine("Использование: dotnet run --project src/MemoryLifecycle -c Release -- <команда>");
        Console.WriteLine("  gc-pressure          КТ №40: поколения и давление на кучу");
        Console.WriteLine("  gc-modes             КТ №41: многопоточная аллокация в текущем режиме GC");
        Console.WriteLine("  gc-modes --compare   КТ №41: запуск в Workstation и Server GC и сравнение");
        Console.WriteLine("  resources            КТ №42: финализатор, IDisposable, using");
        Console.WriteLine("  dispose              КТ №43: паттерн Dispose, IAsyncDisposable, await using");
        Console.WriteLine("  all                  все сценарии подряд");
        break;
}

return 0;

static void PrintHeader(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 92));
    Console.WriteLine(title);
    Console.WriteLine(new string('=', 92));
}
