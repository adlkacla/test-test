# MemoryLifecycle

Консольный проект на .NET 10 для исследования сборки мусора и управления временем жизни объектов:
от наблюдения за поколениями GC до корректного освобождения управляемых и нативных ресурсов.
Выполнен в рамках контрольной точки по дисциплине «Язык программирования C#» (КТ №40–43).

## Стек и окружение

- .NET 10 SDK (проверено на 10.0.403, проверка: `dotnet --version`);
- C# 14, включены nullable-ссылочные типы;
- Git, GitHub;
- дополнительные NuGet-пакеты не требуются.

## Структура

```text
MemoryLifecycle/
├── .gitignore
├── MemoryLifecycle.sln
├── README.md
├── docs/
│   ├── 01-gc.md
│   ├── 02-gc-modes.md
│   ├── 03-disposable.md
│   ├── 04-dispose-pattern.md
│   └── screenshots/
└── src/
    └── MemoryLifecycle/
        ├── MemoryLifecycle.csproj
        ├── Program.cs
        ├── Demos/
        │   ├── DisposeDemos.cs
        │   └── ResourceDemos.cs
        ├── Gc/
        │   ├── GcModeScenarios.cs
        │   ├── GcObserver.cs
        │   ├── GcSnapshot.cs
        │   └── PressureScenarios.cs
        └── Resources/
            ├── DatabaseConnection.cs
            ├── FakeResource.cs
            ├── FileBuffer.cs
            ├── LogFile.cs
            └── ManagedResource.cs
```

- `Gc` — снимки счётчиков GC и сценарии нагрузки;
- `Resources` — классы, управляющие ресурсами;
- `Demos` — консольные сценарии;
- `docs` — отчёты по темам и скриншоты запусков.

## Сборка и запуск

Все команды выполняются из корня репозитория.

```bash
dotnet build src/MemoryLifecycle/MemoryLifecycle.csproj -c Release
```

| Тема | Команда | Что делает |
|---|---|---|
| КТ №40 | `dotnet run --project src/MemoryLifecycle -c Release -- gc-pressure` | Поколения и давление на кучу |
| КТ №41 | `dotnet run --project src/MemoryLifecycle -c Release -- gc-modes` | Многопоточная аллокация в текущем режиме GC |
| КТ №41 | `dotnet run --project src/MemoryLifecycle -c Release -- gc-modes --compare` | Запуск в Workstation и Server GC и сравнение |
| КТ №42 | `dotnet run --project src/MemoryLifecycle -c Release -- resources` | Финализатор, IDisposable, using |
| КТ №43 | `dotnet run --project src/MemoryLifecycle -c Release -- dispose` | Паттерн Dispose, IAsyncDisposable |
| Все | `dotnet run --project src/MemoryLifecycle -c Release -- all` | Итоговая демонстрация |

```bash
dotnet run --project src/MemoryLifecycle -c Release -- gc-pressure
dotnet run --project src/MemoryLifecycle -c Release -- gc-modes
dotnet run --project src/MemoryLifecycle -c Release -- gc-modes --compare
dotnet run --project src/MemoryLifecycle -c Release -- resources
dotnet run --project src/MemoryLifecycle -c Release -- dispose
dotnet run --project src/MemoryLifecycle -c Release -- all
```

Сравнение режимов GC вручную (PowerShell; в cmd.exe — `set DOTNET_gcServer=1`, в bash — `DOTNET_gcServer=1 dotnet run ...`):

```powershell
$env:DOTNET_gcServer=0
dotnet run --project src/MemoryLifecycle -c Release --no-build -- gc-modes
$env:DOTNET_gcServer=1
dotnet run --project src/MemoryLifecycle -c Release --no-build -- gc-modes
Remove-Item Env:DOTNET_gcServer
```

## Варианты заданий

| Тема | Вариант | Модули |
|---|---|---|
| КТ №40 | 2 — «Измерение давления» | `GcSnapshot`, `GcObserver`, `PressureScenarios` |
| КТ №41 | 2 — «Многопоточная аллокация» | `GcModeScenarios` |
| КТ №42 | 2 — «Лог-файл» | `FakeResource`, `ManagedResource`, `LogFile`, `ResourceDemos` |
| КТ №43 | 1 — «Соединение с БД» | `DatabaseConnection`, `FileBuffer`, `DisposeDemos` |

## Результаты

Значения получены на одной машине и при повторном запуске будут немного отличаться: важна не абсолютная величина, а соотношение показателей. Текстовый вывод каждой команды приведён в отчётах `docs`, скриншоты — дополнение к нему.

### КТ №40. Поколения и давление на кучу

Команда: `dotnet run --project src/MemoryLifecycle -c Release -- gc-pressure` (Workstation GC по умолчанию).
Условия запуска: Intel Core i7-9750H (6 ядер / 12 потоков), 16 ГБ ОЗУ, Windows 11 (10.0.26100), .NET SDK 10.0.403, конфигурация Release, без отладчика.

| Сценарий | Время, мс | Gen0 | Gen1 | Gen2 | Alloc, МБ | Heap Δ, МБ |
|---|---:|---:|---:|---:|---:|---:|
| Мелкие объекты (byte[100], 100 000 шт.) | 9.52 | 2 | 0 | 0 | 12.21 | 0.06 |
| Крупные объекты (byte[100 000], 10 шт.) | 0.10 | 0 | 0 | 0 | 0.95 | 0.00 |
| Удерживаемые объекты (byte[512], 1 000 шт., статический список) | 0.17 | 0 | 0 | 0 | 0.52 | 0.00 |
| Принудительный Gen2 (blocking) | 0.88 | 1 | 1 | 1 | 0.00 | 0.52 |

Всего аллоцировано с начала работы: 13.69 МБ. Состояние кучи до экспериментов: `HeapSizeBytes = 0.00 МБ`
(сборок ещё не было, а `GetGCMemoryInfo` описывает последнюю завершённую сборку); после — `HeapSizeBytes = 0.58 МБ`,
`TotalCommittedBytes = 6.84 МБ`, `PauseTimePercentage = 2.10 %`.

Выводы:

1. **Gen0 нагружают мелкие короткоживущие объекты**: 12.21 МБ аллокаций дали 2 сборки Gen0, а Gen2 не потребовался —
   объекты умирают молодыми.
2. **Gen2 нагружает только полная сборка**, и её переживают долгоживущие данные: после принудительного Gen2
   в куче остались 0.52 МБ из статического списка (`Heap Δ`), а крупные массивы из локального списка были собраны.
3. **LOH не всегда приводит к немедленной сборке**: крупные массивы (> 85 000 байт) сразу попадают в LOH и не
   расходуют бюджет Gen0, но счётчик Gen2 растёт только при сборке Gen2, а бюджета LOH хватило.

Подробный отчёт: [docs/01-gc.md](docs/01-gc.md).

![gc-pressure](docs/screenshots/01-gc-pressure.png)

### КТ №41. Workstation и Server GC

Команды: `gc-modes` с `DOTNET_gcServer=0` и `DOTNET_gcServer=1` (см. «Сборка и запуск»), затем `gc-modes --compare`.
Условия запуска: Intel Core i7-9750H (6 ядер / 12 потоков), 16 ГБ ОЗУ, Windows 11 (10.0.26100), .NET SDK 10.0.403, конфигурация Release, без отладчика. Нагрузка: 4 потока × 100 000 объектов `byte[256]`; прогрев + 5 замеров, в таблицах — среднее.

**Workstation GC** (`DOTNET_gcServer=0`):

| Показатель | Значение |
|---|---|
| Server GC | False |
| Latency mode | Interactive |
| Потоков / объектов на поток | 4 / 100 000 |
| Принудительный блокирующий Gen2 | 0.29 мс |
| Время параллельной аллокации | 11.69 мс (min 10.17, max 12.44) |
| Gen0 / Gen1 / Gen2 за замер | 18 / 0 / 0 |
| Суммарная пауза GC за замер | 3.57 мс |
| Аллоцировано | 106.81 МБ |
| Контрольная сумма | 50 969 280 |

**Server GC** (`DOTNET_gcServer=1`):

| Показатель | Значение |
|---|---|
| Server GC | True |
| Latency mode | Interactive |
| Потоков / объектов на поток | 4 / 100 000 |
| Принудительный блокирующий Gen2 | 0.89 мс |
| Время параллельной аллокации | 21.02 мс (min 17.92, max 28.00) |
| Gen0 / Gen1 / Gen2 за замер | 43.4 / 0.2 / 0.2 |
| Суммарная пауза GC за замер | 9.48 мс |
| Аллоцировано | 106.81 МБ |
| Контрольная сумма | 50 969 280 |

Повторное сравнение `gc-modes --compare` (оба режима в отдельных процессах): Workstation — 10.47 мс, Gen0 18.0,
пауза 3.15 мс; Server — 20.82 мс, Gen0 43.4, пауза 8.84 мс; аллокации и контрольные суммы совпали.

| Режим | Server GC | Время, мс | Gen0 | Gen2 | Пауза GC, мс |
|---|---|---:|---:|---:|---:|
| Workstation | false | 11.69 | 18.0 | 0.0 | 3.57 |
| Server | true | 21.02 | 43.4 | 0.2 | 9.48 |

Выводы:

1. Режим действительно переключился (`GCSettings.IsServerGC` = False / True), а работа одинакова
   (106.81 МБ и одинаковая контрольная сумма) — сравнение корректно.
2. **Больше сборок Gen0 в Server GC — не деградация**: у каждой кучи свой бюджет Gen0, а включённый по умолчанию
   DATAS держит бюджет пропорциональным объёму живых данных (здесь он почти нулевой). С выключенным DATAS
   (`DOTNET_GCDynamicAdaptationMode=0`) тот же сценарий в Server GC дал ≈1.2 сборки Gen0 за замер.
3. **Server GC не всегда быстрее**: на короткой нагрузке (~107 МБ за десятки миллисекунд) накладные расходы
   нескольких куч и потоков сборки перевесили выигрыш; пауза принудительного Gen2 тоже больше, так как обходятся
   все кучи. Вывод о преимуществе режима делается по нескольким замерам и длительной нагрузке, а не по одному запуску.

Подробный отчёт: [docs/02-gc-modes.md](docs/02-gc-modes.md).

![gc-modes workstation](docs/screenshots/02-gc-modes-workstation.png)
![gc-modes server](docs/screenshots/02-gc-modes-server.png)
![gc-modes compare](docs/screenshots/02-gc-modes-compare.png)

### КТ №42. Финализатор и IDisposable

Команда: `dotnet run --project src/MemoryLifecycle -c Release -- resources`. Фрагмент вывода (полный — в отчёте):

```text
1. Финализатор: момент вызова не контролируется
[FakeResource 1] открыт
[FakeResource 2] открыт
Метод завершён, финализаторы ещё не выполнялись
[FakeResource 1] финализатор
[FakeResource 2] финализатор
После GC.Collect + WaitForPendingFinalizers

2. using: детерминированное освобождение
[ManagedResource 1] открыт
Работа внутри using-блока
[ManagedResource 1] Dispose вызван
Строка после using-блока
...
4.1. Корректный сценарий: using-блок
[LogFile 1] файл открыт: logs\memory-lifecycle-using.log
Запись выполнена внутри using-блока
[LogFile 1] файл закрыт
После корректного using-блока

4.2. Некорректный сценарий: без using и без Dispose
[LogFile 2] файл открыт: logs\memory-lifecycle-leak.log
Метод с забытым Dispose завершён
[LogFile 2] финализатор! — файл не был закрыт явно
После сборки: сработал финализатор

4.3. using-декларация без скобок
[LogFile 3] файл открыт: logs\memory-lifecycle-declaration.log
Последняя строка метода: Dispose ещё не вызван
[LogFile 3] файл закрыт
После выхода из метода: файл уже закрыт
```

Выводы:

1. **Финализатор недетерминирован**: после выхода из метода он не вызвался; сообщения появились только после
   `GC.Collect()` + `GC.WaitForPendingFinalizers()`, поток финализации работает отдельно.
2. **`Dispose()` детерминирован**: `using` вызывает его ровно на закрывающей скобке (и при исключении — `using`
   разворачивается в `try/finally`); `using`-декларация — в конце области видимости переменной.
3. **Забытый `Dispose()`** оставляет файл открытым до сборки; финализатор `LogFile` только сообщает об утечке
   и не трогает управляемый `StreamWriter`, а `GC.SuppressFinalize` в `Dispose()` исключает ложные сообщения.
   Повторный `Dispose()` ничего не делает, `Write()` после закрытия даёт `ObjectDisposedException`.

Подробный отчёт: [docs/03-disposable.md](docs/03-disposable.md).

![resources](docs/screenshots/03-resources.png)

### КТ №43. Паттерн Dispose и IAsyncDisposable

Команда: `dotnet run --project src/MemoryLifecycle -c Release -- dispose`. Фрагмент вывода (полный — в отчёте):

```text
1. Синхронный сценарий using
[DatabaseConnection 1] соединение открыто
[DatabaseConnection 1] запрос: SELECT * FROM Students
[DatabaseConnection 1] журнал запросов очищен
[DatabaseConnection 1] соединение закрыто

2. Асинхронный сценарий await using
[DatabaseConnection 2] соединение открыто
[DatabaseConnection 2] запрос: SELECT * FROM Topics
[DatabaseConnection 2] журнал запросов очищен асинхронно
[DatabaseConnection 2] соединение закрыто

3. Проверка GC.SuppressFinalize после using
[DatabaseConnection 3] соединение открыто
[DatabaseConnection 3] запрос: SELECT 1
[DatabaseConnection 3] журнал запросов очищен
[DatabaseConnection 3] соединение закрыто
После сборки: сообщение финализатора отсутствует

4. Финализатор без Dispose
[DatabaseConnection 4] соединение открыто
[DatabaseConnection 4] запрос: UPDATE Students SET Name = 'Test'
[DatabaseConnection 4] соединение закрыто
После сборки: финализатор освободил только нативный ресурс
```

Выводы:

1. **`Dispose(bool disposing)` разделяет ресурсы**: при `true` очищается управляемый журнал запросов и закрывается
   соединение, при `false` (финализатор) — только соединение. В сценарии 4 строки «журнал запросов очищен» нет:
   финализатор не трогает управляемые объекты.
2. **`GC.SuppressFinalize`** вызывается и в `Dispose()`, и в `DisposeAsync()`: в сценарии 3 после сборки
   повторного сообщения финализатора нет.
3. **`await using` → `DisposeAsync()`** выполняет асинхронную очистку в `DisposeAsyncCore()`, затем в `finally`
   закрывает нативный ресурс через `Dispose(false)`. Реализованы оба интерфейса, поэтому работает и `using`,
   и `await using`; повторные `Dispose()`/`DisposeAsync()` ничего не делают.

Подробный отчёт: [docs/04-dispose-pattern.md](docs/04-dispose-pattern.md).

![dispose](docs/screenshots/04-dispose.png)

## Выводы

1. Основная нагрузка на Gen0 создаётся короткоживущими объектами; они умирают молодыми и не доходят до Gen2.
2. Крупные объекты попадают в LOH, но счётчик Gen2 растёт только при сборке Gen2, а не при каждой аллокации.
3. `GC.GetGCMemoryInfo()` описывает последнюю завершённую сборку, поэтому до первой сборки `HeapSizeBytes = 0`.
4. Server GC не всегда быстрее: на короткой многопоточной нагрузке он проиграл Workstation GC по времени и паузам;
   большее число сборок Gen0 объясняется несколькими кучами и DATAS, а не деградацией.
5. Финализатор вызывается недетерминированно и служит последним шансом; `Dispose()` через `using` освобождает
   ресурс детерминированно, в том числе при исключении.
6. Полный паттерн Dispose разделяет управляемые и нативные ресурсы через `disposing`, а `GC.SuppressFinalize`
   убирает объект из очереди финализации после явного освобождения.
7. `IAsyncDisposable` и `await using` позволяют освобождать ресурсы асинхронно; реализуются оба интерфейса,
   нативный ресурс в `DisposeAsync()` закрывается в `finally`.

## Проверка готовности

| Что проверяется | Состояние |
|---|---|
| Сборка | `dotnet build src/MemoryLifecycle/MemoryLifecycle.csproj -c Release` — 0 ошибок, 0 предупреждений |
| Команды | `gc-pressure`, `gc-modes`, `resources`, `dispose`, `all` запускаются из корня репозитория |
| docs | `01-gc.md`, `02-gc-modes.md`, `03-disposable.md`, `04-dispose-pattern.md` + скриншоты |
| История | отдельный коммит на каждый этап |
| Чистота | `bin`, `obj`, `.vs`, `logs` исключены в `.gitignore` |

![build](docs/screenshots/00-build.png)

## История коммитов

| № | Сообщение коммита | Что входит |
|---|---|---|
| 0 | `chore: initialize .NET 10 console project` | Решение, проект, .gitignore, первый README |
| 1 | `feat(gc): add generations and pressure measurement` | КТ №40: GcSnapshot, GcObserver, PressureScenarios, команда gc-pressure |
| 2 | `feat(gc): compare workstation and server gc modes` | КТ №41: GcModeScenarios, измерение пауз, команда gc-modes |
| 3 | `feat(resources): add finalizer and IDisposable examples` | КТ №42: FakeResource, ManagedResource, LogFile, команда resources |
| 4 | `feat(dispose): implement full dispose pattern and IAsyncDisposable` | КТ №43: DatabaseConnection, FileBuffer, DisposeDemos, команда dispose |
| 5 | `docs: add measurement results and conclusions` | README, docs, таблицы результатов, выводы |

Полная история: `git log --oneline --graph`.
## Автор

adlkalca, группа 10924-1.
