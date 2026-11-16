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
    default:
        Console.WriteLine("Использование: dotnet run --project src/MemoryLifecycle -c Release -- <команда>");
        Console.WriteLine("  gc-pressure          КТ №40: поколения и давление на кучу");
        Console.WriteLine("  gc-modes             КТ №41: многопоточная аллокация в текущем режиме GC");
        Console.WriteLine("  gc-modes --compare   КТ №41: запуск в Workstation и Server GC и сравнение");
        Console.WriteLine("  resources            КТ №42: финализатор, IDisposable, using");
        break;
}

return 0;
