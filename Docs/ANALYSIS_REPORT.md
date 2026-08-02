# ANALYSIS REPORT — NalogParserWPF

**Дата анализа:** 2026-08-02
**TargetFramework:** net8.0-windows
**Тип проекта:** WPF (WinExe), SDK-style csproj

---

## 1. Структура проекта (папки и файлы)

```
NalogParserWPF.sln                      — файл решения
NalogParserWPF/
├── App.xaml                            — точка входа приложения (StartupUri="MainWindow.xaml")
├── App.xaml.cs                         — code-behind Application, пустой (нет конфигурации, нет DI)
├── AssemblyInfo.cs                     — атрибуты сборки
├── money.ico                           — иконка приложения
├── NalogParserWPF.csproj               — файл проекта (SDK-style, net8.0-windows, UseWPF)
├── NalogParserWPF.csproj.user          — пользовательские настройки VS
│
├── MainWindow.xaml                     — главное окно: вкладки «Поиск» и «История», стили, разметка
├── MainWindow.xaml.cs                  — code-behind: вся бизнес-логика (~775 строк)
│                                       • поиск юр/физ лиц, wildcard-поиск
│                                       • отображение результата (формирование текста вручную)
│                                       • сохранение в историю, автопроверка активных УНП
│                                       • экспорт в PDF (через HTML), экспорт истории CSV/JSON/TXT
│                                       • фильтрация истории, повторный поиск
│
├── BatchSearchWindow.xaml              — окно группового поиска (разметка)
├── BatchSearchWindow.xaml.cs           — code-behind: пакетный поиск УНП, экспорт CSV
│                                         + модель BatchResultItem (объявлена в этом же файле)
├── HistoryDetailWindow.xaml            — окно деталей записи истории (разметка)
├── HistoryDetailWindow.xaml.cs         — code-behind: отображение сохранённого результата,
│                                         экспорт в PDF (через HTML)
├── StatusChangeWindow.xaml             — окно результатов автопроверки (разметка)
├── StatusChangeWindow.xaml.cs          — code-behind + модель StatusChangeItem (в этом же файле)
│
├── Models/
│   └── Models.cs                       — все модели в одном файле:
│                                        NalogResponse, CompanyRow, SearchHistory,
│                                        HistoryItem, LegalSearchParams, PhysicalSearchParams
│
├── Services/
│   └── NalogApiService.cs              — HTTP-клиент для API grp.nalog.gov.by
│                                        (SearchLegalEntity, SearchPhysicalPerson, GetCompanyInfo,
│                                         WildcardToRegex)
│
├── Database/
│   └── DatabaseHelper.cs               — SQLite-доступ (Microsoft.Data.Sqlite)
│                                        • создание таблицы SearchHistory + индексы
│                                        • CRUD: Add/Get/Delete/Clear/UpdateStatus/GetActiveUnps
│                                        • маппинг записей и логика цвета статуса
│
├── bin/                                — сборка (в т.ч. search_history.db — файл БД создаётся рядом)
├── obj/                                — промежуточные файлы сборки
└── .vs/                                — служебные файлы Visual Studio
```

## 2. NuGet-пакеты (NalogParserWPF.csproj)

| Пакет | Версия | Назначение |
|---|---|---|
| Microsoft.Data.Sqlite.Core | 10.0.9 | ADO.NET-провайдер SQLite |
| SQLitePCLRaw.bundle_e_sqlite3 | 3.0.3 | Нативный bundle для SQLitePCLRaw |
| Stub.System.Data.SQLite.Core.NetStandard | 1.0.119 | Обёртка System.Data.SQLite (лишний, дублирует нижестоящий) |
| System.Data.SQLite | 2.0.3 | ADO.NET SQLite (используется в DatabaseHelper) |
| System.Data.SQLite.Core | 1.0.119 | Ядро System.Data.SQLite (дублирует Microsoft.Data.Sqlite) |
| Newtonsoft.Json | 13.0.4 | JSON-сериализация |
| QuestPDF | 2026.6.1 | PDF-генерация (установлен, но НЕ используется — экспорт сделан через HTML) |

> Примечание: в коде фактически используются только `Microsoft.Data.Sqlite`, `Newtonsoft.Json` и шрифты Lato (из QuestPDF, но сама библиотека в коде не задействована). В проекте три SQLite-провайдера одновременно — конфликт/избыточность.

## 3. Проверка обязательных компонентов

| Компонент | Статус | Комментарий |
|---|---|---|
| appsettings.json | ❌ Отсутствует | Нет конфигурации приложения |
| App.config | ❌ Отсутствует | |
| DI-контейнер | ❌ Отсутствует | `App.xaml.cs` пустой; сервисы создаются через `new` прямо в окнах |
| HttpClient | ⚠️ Частично | Есть в `NalogApiService`, но создаётся через `new HttpClient()` без `IHttpClientFactory`; URL захардкожен |
| Сервисы для API | ✅ Есть | `NalogApiService` — единственный сервис |
| Сервисы для БД | ✅ Есть | `DatabaseHelper` (SQLite) |
| Логирование | ❌ Отсутствует | Вместо логов — `Debug.WriteLine` и `MessageBox` |
| MVVM (ViewModels) | ❌ Отсутствует | Папки `ViewModels/`, `Views/`, `Helpers/`, `Converters/` нет |

## 4. Текущая архитектура (схема слоёв)

```
┌─────────────────────────────────────────────────────────────┐
│                        UI Layer (WPF)                        │
│  MainWindow / BatchSearchWindow / HistoryDetailWindow /      │
│  StatusChangeWindow                                          │
│  — code-behind содержит ВСЮ логику (поиск, фильтрация,       │
│    экспорт, валидация, работа с БД, вёрстка текста результата)│
├─────────────────────────────────────────────────────────────┤
│                    Services Layer                            │
│  NalogApiService (HTTP к grp.nalog.gov.by)                   │
│  DatabaseHelper (SQLite, таблица SearchHistory)              │
├─────────────────────────────────────────────────────────────┤
│                        Models Layer                          │
│  Models.cs — NalogResponse, CompanyRow, SearchHistory,       │
│  HistoryItem, LegalSearchParams, PhysicalSearchParams        │
│  + BatchResultItem, StatusChangeItem (внутри файлов окон)    │
├─────────────────────────────────────────────────────────────┤
│                     External Dependencies                    │
│  HTTP API: http://grp.nalog.gov.by/api/grp-public/data       │
│  SQLite: search_history.db (в каталоге приложения)           │
└─────────────────────────────────────────────────────────────┘
```

### Оценка соответствия MVVM

**Соответствие: 2/10 (фактически отсутствует).**

- ❌ Нет слоя ViewModels — окна наследуют `Window`, логика в code-behind
- ❌ Нет команд (`ICommand`), привязок `Binding`, `INotifyPropertyChanged` в UI
- ❌ UI-элементы (`TextBox`, `Button`, `DataGrid`) обращаются напрямую по имени
- ❌ Управление видимостью панелей через код (`Visibility`), а не через DataTriggers
- ❌ Формирование текста результата вручную (`AppendText`), а не DataTemplates
- ⚠️ Только модели данных объявлены отдельно (частичное соответствие)
- ⚠️ `BatchResultItem` и `StatusChangeItem` реализуют `INotifyPropertyChanged`, но без изменяемых свойств с вызовом `OnPropertyChanged` — бессмысленно

## 5. Выявленные проблемы

### Нарушение SOLID
1. **SRP нарушен** — `MainWindow.xaml.cs` (~775 строк): поиск, валидация, отображение, история, экспорт, автопроверка в одном классе
2. **DIP нарушен** — сервисы создаются через `new` внутри окон; нет интерфейсов (`INalogApiService`, `IHistoryRepository`)
3. **OCP нарушен** — расширение логики (новые форматы экспорта, новые источники) требует правки существующих методов
4. **ISP/интерфейсы** — интерфейсов нет вообще

### Жёсткая связанность
5. Окна зависят от конкретных классов `NalogApiService` и `DatabaseHelper` напрямую
6. `DatabaseHelper` создаёт себе строку подключения и файл БД в конструкторе (не тестируемо)
7. URL API захардкожен константой в сервисе
8. UI напрямую работает с `DatabaseHelper` (LoadHistory, GetHistoryById, UpdateStatus и т.д.)
9. Модели-дубликаты `BatchResultItem`/`StatusChangeItem` живут внутри файлов окон

### Дублирование
10. Метод `DisplayCompanyInfo` продублирован в `MainWindow` и `HistoryDetailWindow`
11. Экспорт PDF через HTML-строку продублирован в `MainWindow.ExportToPdf` и `HistoryDetailWindow.ExportToPdf`
12. Логика «цвет статуса» (Green/Red/Orange) продублирована в `DatabaseHelper`, `HistoryDetailWindow`, `StatusChangeWindow`, `BatchSearchWindow`
13. `SQLitePCL.Batteries.Init()` вызывается в `DatabaseHelper` и в `MainWindow`
14. Методы чтения БД (`GetHistoryById`, `GetHistoryByUnp`, `GetActiveUnps`) содержат одинаковый маппинг записи в `SearchHistory`
15. Маппинг «тип поиска → русская подпись» (`"Legal" → "Юрлицо/ИП"`) разбросан по `DatabaseHelper`, `HistoryDetailWindow`, `BatchSearchWindow`

### Прочее
16. Отсутствует логирование (только `Debug.WriteLine` и `MessageBox`)
17. Отсутствует обработка ошибок на слое БД — исключения всплывают в UI
18. QuestPDF установлен, но не используется; вместо него HTML → открытие браузера (не создаёт реальный PDF-файл)
19. Ошибка в `BatchSearchWindow`: `else` ветка для недействующего статуса инкрементирует `successCount`
20. `catch (Exception ex)` в `NalogApiService` перехватывает всё и перекидывает в `throw new Exception` — теряет тип исключения и контекст
21. Смешение трёх SQLite-провайдеров (`Microsoft.Data.Sqlite`, `System.Data.SQLite`, `Stub.System.Data.SQLite`) — избыточность и риск конфликтов
22. `HistoryItem` содержит UI-свойство `StatusColor` — нарушение разделения моделей
23. Нет валидации УНП (формат 9 цифр) ни в UI, ни в сервисе
24. HTTP-запросы без отмены (`CancellationToken`), при закрытии окна задачи продолжаются
25. Не задействован `App.xaml` для ресурсов/стилей (всё в `MainWindow.xaml`)

## 6. Что отсутствует для работы с API

- **Конфигурация** — `appsettings.json` + класс `AppConfig`/`AppOptions` для URL и таймаутов API (сейчас URL захардкожен)
- **DI-контейнер** — `Microsoft.Extensions.Hosting` + регистрация сервисов в `App.xaml.cs` (сейчас `new NalogApiService()` в каждом окне)
- **`IHttpClientFactory`** — управляемый жизненный цикл `HttpClient` вместо `new HttpClient()`
- **Интерфейсы сервисов** — `INalogApiService`, `IHistoryRepository` для тестируемости и заменяемости
- **DTO/результат-обёртка** — типизированный `ApiResult<T>`/`OperationResult<T>` с кодом ошибки вместо исключений
- **Обработка ошибок API** — корректная обработка HTTP-статусов (400, 429, 5xx), таймаутов, ретраев
- **`CancellationToken`** — поддержка отмены запросов
- **Логирование** — `ILogger` (Microsoft.Extensions.Logging) для запросов/ошибок API
- **Сериализация** — `System.Text.Json` или единый подход (сейчас Newtonsoft.Json только из-за атрибутов)
- **Тесты** — тестовый проект для сервисов (мокаемый HttpClient, фикстуры ответов API)

## 7. План на Шаг 2 (задачи)

1. Создать структуру MVVM: папки `ViewModels/`, `Views/`, `Helpers/`, `Converters/`
2. Подключить `Microsoft.Extensions.Hosting` и настроить DI-контейнер в `App.xaml.cs`
3. Добавить `appsettings.json` (URL API, таймауты, путь к БД) + класс `AppConfig` с привязкой конфигурации
4. Реализовать интерфейсы `INalogApiService` и `IHistoryRepository`; перевести `NalogApiService` и `DatabaseHelper` на них
5. Перевести `NalogApiService` на `IHttpClientFactory` + `CancellationToken`; вынести URL в конфигурацию
6. Ввести `ViewModelBase` (INotifyPropertyChanged), команды `RelayCommand`
7. Создать `MainViewModel` (поиск, результат, статусы) и привязать `MainWindow` через DataContext
8. Создать `HistoryViewModel` (список, фильтрация, экспорт, повторный поиск)
9. Создать `BatchSearchViewModel`, `HistoryDetailViewModel`, `StatusChangeViewModel`
10. Вынести форматирование результата в сервис/helper (`IResultFormatter`) вместо `AppendText`
11. Вынести экспорт PDF в отдельный сервис (`IPdfExportService`) на базе QuestPDF
12. Вынести логику «цвет статуса» в единый helper/конвертер (`StatusToBrushConverter`)
13. Устранить дублирование маппинга БД (общий метод в `DatabaseHelper`)
14. Очистить лишние SQLite-пакеты (`System.Data.SQLite`, `Stub.System.Data.SQLite`)
15. Добавить логирование (`ILogger<...>`) во все сервисы и ViewModel
16. Добавить валидацию УНП (формат, длина) через `IDataErrorInfo`/атрибуты
