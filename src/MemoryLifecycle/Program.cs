using System.Globalization;
using MemoryLifecycle.Gc;

Console.OutputEncoding = System.Text.Encoding.UTF8;
// Единый формат чисел (точка как разделитель) на любой машине,
// чтобы вывод совпадал с таблицами в README.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

switch (command)
{
    case "gc-pressure":
        PressureScenarios.RunAll();
        break;
    default:
        Console.WriteLine("Использование: dotnet run --project src/MemoryLifecycle -c Release -- <команда>");
        Console.WriteLine("  gc-pressure          КТ №40: поколения и давление на кучу");
        break;
}

return 0;
