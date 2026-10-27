# PROJECT CONTEXT — NalogParserWPF

## ОПИСАНИЕ
WPF-приложение для проверки контрагентов по УНП через API grp.nalog.gov.by, получения курсов валют от НБ РБ и статистических данных от Белстата.

## СТЕК
- .NET 8, WPF, MVVM
- DI: Microsoft.Extensions.Hosting
- БД: SQLite (Microsoft.Data.Sqlite.Core)
- HTTP: IHttpClientFactory + Polly
- PDF: QuestPDF
- Excel: EPPlus
- Логирование: ILogger
- Нечёткий поиск: FuzzySharp 2.0.2 (только вкладка «📊 Госстатистика», «живой» поиск показателей)
- API НБ РБ: https://api.nbrb.by/exrates/ (раздел NbrbSettings в appsettings.json)
- API Белстата: https://dataportal.belstat.gov.by/osids-public-api/ (раздел BelstatSettings в appsettings.json, формат SDMX-JSON, без авторизации)

## СТРУКТУРА ПРОЕКТА
- /Converters — конвертеры (BooleanToVisibility, NullToVisibility, InvertedBoolToVisibility, StatusToBrush, EmptyStringToVisibility)
- /Database — DatabaseHelper (SQLite), ExchangeHistoryRepository (история курсов валют), BelstatHistoryRepository (история запросов статистики Белстата)
- /Helpers — RelayCommand, StatusHelper, StringHelper, UnpValidator, DataGridHelper (копирование ячейки DataGrid), DateTimeHelper (время РБ, UTC+3), HttpRetryDelegatingHandler, HttpLoggingMessageHandler
- /Models — модели данных (Models.cs, NBRBModels.cs, BelstatModels.cs, BatchResultItem.cs, StatusChangeItem.cs)
- /Services — NalogApiService, NBRBApiService (НБ РБ), BelstatApiService (Белстат, SDMX-JSON), PdfExportService, InMemoryCacheService, NotificationService, ResultFormatter, NalogApiException
- /ViewModels — MainViewModel, BatchSearchViewModel, HistoryDetailViewModel, StatusChangeViewModel, SettingsViewModel, AboutViewModel, ExchangeRateViewModel, BelstatViewModel
- /Views — SettingsWindow, AboutWindow, BatchSearchView (разметка вкладки и окна массового поиска), BelstatDataWindow (отдельное окно таблицы результатов Белстата)
- /Configuration — AppConfig (ApiSettings, NbrbSettings, BelstatSettings, DatabaseSettings)
- /Docs — ANALYSIS_REPORT.md, BELSTAT_API_RESEARCH.md (исследование публичного API Белстата)

## ЧТО УЖЕ СДЕЛАНО
- Поиск по УНП с автодополнением
- Массовый поиск с загрузкой файлов (.txt, .csv, .xlsx)
- История поисков с автоперепроверкой
- Экспорт в PDF, Excel, CSV
- Настройки (URL, таймаут, тема)
- Брендинг СК, окно "О программе"
- Вкладка "💱 Курсы валют": три режима (курс одной валюты на дату, курсы всех валют на дату, динамика за период), экспорт в PDF/Excel/CSV, история запросов курсов
- Вкладка «📊 Госстатистика» (ШАГ 9): каталог рубрик и показателей Белстата, значения показателя (или всех показателей рубрики) за диапазон лет, таблица результата, экспорт в PDF/Excel/CSV, история запросов статистики
- Вкладка «📊 Госстатистика» (ШАГ 13): разделитель высоты между каталогом показателей и таблицей результатов (карточка каталога со ScrollViewer, ограничения MinHeight 96/220), кнопка «Открыть в окне» и отдельное окно «Госстатистика — таблица результатов» (крупная таблица + экспорт PDF/Excel/CSV + закрытие по Esc)
- ЭТАП 1 — критические баги и структура:
  * Иконка приложения — zaglav.ico (money.ico исключён из проекта)
  * «Массовый поиск УНП» перенесён из шапки в отдельную вкладку главного окна (3-я вкладка, между «История проверок УНП» и «Курсы валют»)
  * Исправлен «тёмный прямоугольник» в окне «Просмотр записи из истории» (пустая плашка статуса) и наложение строк в блоке реквизитов
  * Размеры окон увеличены (MainWindow 1200×900, HistoryDetail 900×700, BatchSearch 1000×700, StatusChange 900×600, Settings 700×600, About 550×500)
  * Все временные метки — локальное время РБ (UTC+3), единая точка — DateTimeHelper
  * Ошибка невалидности УНП выводится в StatusMessage (без MessageBox)
- ЭТАП 1 (доработка) — исправления перед ЭТАПОМ 2:
  * Ошибки API 400 (включая «УНП не найден в базе») выводятся в StatusMessage главного окна; модальный MessageBox для 400 убран (BatchSearchViewModel ошибки по строкам и так показывает в таблице)
  * Восстановлена стандартная оконная рамка: у всех окон явно указан WindowStyle="SingleBorderWindow" (диалоги — с прежним ResizeMode), главное окно открывается WindowState="Maximized" и снова называется «Проверка контрагентов по УНП»
  * Убран хардкод цветов: 163 литерала (#hex/White/Gray/LightGray/#666/#888) в 7 XAML-файлах заменены на {DynamicResource ...} ключи; в App.xaml добавлены 10 кистей (DangerSoftBrush, DangerBrush, ExportButtonBrush, ExcelButtonBrush, SecondaryButtonBrush, NeutralButtonBrush, TextMutedBrush, OnAccentBrush, OverlayDisabledBrush, OverlayPressedBrush)
  * App.ApplyTheme расширен: тёмная тема теперь перекрашивает не только фон/акцент, но и карточки, поля ввода, сетки, разделители, предупреждения и оверлеи кнопок
- ШАГ 5 — UX вкладки «💱 Курсы валют» (уточнение интерфейса):
  * Переключатели режимов — ToggleButton-«плитки» с явными подписями («Курс одной валюты», «Все валюты на дату», «Динамика за период»); активный режим подсвечивается акцентом, снять активный режим нельзя (вкладка всегда имеет выбранный режим)
  * Поиск по справочнику валют: отдельное поле «🔎 Поиск валюты» (фильтр по коду ISO / цифровому коду / наименованию) + ComboBox стал редактируемым (IsEditable, TextSearch по DisplayName); выбранная валюта всегда остаётся в списке
  * Двойной клик по ячейке таблиц «Все валюты на дату» и «Динамика» — копирование значения в буфер обмена (DataGridHelper.CopyCellContent, с уведомлением)
  * Кнопки «Получить …» — акцентный стиль AccentButton, MinWidth=170; кнопки экспорта PDF/Excel/CSV в одном ряду, MinWidth=100, различаются цветом (ExportButtonBrush / ExcelButtonBrush / SecondaryButtonBrush)
  * Тёмная тема: базовым стилям TextBox/ComboBox добавлены Foreground и CaretBrush (TextPrimaryBrush) — текст и курсор полей ввода читаемы на тёмном фоне
  * Дублирование истории запросов курсов проверено и отсутствует: история — единственный список в правой колонке вкладки («История запросов курсов»)
- ШАГ 6 — поиск по валютам и вёрстка PDF курсов:
  * Кнопка «🔍 Найти» и подсказка-плейсхолдер в поле поиска валют; фильтр применяется по Enter (KeyDown → CurrencySearchBox_KeyDown) и по кнопке (`ApplyCurrencyFilterCommand`)
  * PDF отчётов по курсам: шапка (логотип + заголовок) — только на первой странице (`page.Header().ShowOnce()`); подпись «Проверил следователь» — внизу последней страницы (Шаг 10, см. «ШАГ 10 — ДЕТАЛИ»); номер страницы — на каждой
  * Подзаголовок PDF курсов изменён на «Информация о курсах валют по данным НБ РБ» (был «Проверка контрагентов по данным grp.nalog.gov.by»)
  * PDF карточки контрагента/записи истории (ExportCompanyInfoAsync, ExportHistoryEntryAsync) — шапка тоже показывается только на первой странице
- ШАГ 10 — подпись «Проверил следователь» прижата к нижней кромке последней страницы во всех PDF-отчётах (без лишних страниц), данные Белстата показываются на русском (справочник структуры данных API + резервный словарь BelstatLocalizationHelper)
- ШАГ 11 — улучшенный поиск показателей во вкладке «📊 Госстатистика»: «живой» фильтр по мере ввода (без задержки, до 20 совпадений), нечёткий поиск FuzzySharp при отсутствии точных совпадений, сужение дерева рубрик по строке поиска, кнопка «Очистить», обработка краевых случаев (подробности — «ШАГ 11 — ДЕТАЛИ РЕАЛИЗАЦИИ»)
- ШАГ 12 — устранена критическая ошибка загрузки главного окна («Не удается вызвать MarkupExtension.ProvideValue из-за циклической зависимости»): привязки двух контекстных меню переведены с `{x:Reference …}` на `PlacementTarget` + `RelativeSource AncestorType=ContextMenu`, команды истории УНП получили проверку параметра (подробности — «ШАГ 12 — ДЕТАЛИ»)
- ЭТАП 3 — фирменные цвета СК (васильковый #1A3A8F + жёлтый #F5C518) и видимые жёлтые акценты:
  * App.xaml / App.xaml.cs: ключ `SecondaryBrush` (#F5C518) и `OnSecondaryBrush` (#0D1B3E); акцент `AccentBrush` = #1A3A8F (тёмная тема #4A6FD4), текст на акценте `OnAccentBrush` = #FFFFFF в обеих темах
  * MainWindow.xaml: у активной вкладки — жёлтая полоса 3px по нижней кромке (BorderBrush=SecondaryBrush); цвет заголовка вкладки переведён на уровень TabItem (Style.Triggers), поэтому текст и иконка активной вкладки — белые на васильковом фоне и в светлой, и в тёмной теме
  * MainWindow.xaml: под шапкой окна добавлена фирменная жёлтая линия (Border Height=2, Background=SecondaryBrush)
  * Views/SettingsWindow.xaml: у шапки окна настроек — жёлтая нижняя рамка (BorderBrush=SecondaryBrush, BorderThickness=0,0,0,3), логотип sk-logo-2.png
  * Проверка выполнена снимком окна (UIA-границы + скриншот + гистограмма цветов): светлая тема — фон #1A3A8F, текст/иконка #FFFFFF, полоса #F5C518; тёмная — фон #4A6FD4, текст #FFFFFF, полоса #F5C518
- ШАГ 7 — читаемость тёмной темы и рабочий выбор источника API:
  * App.xaml: новые ключи `ComboBoxBackgroundBrush`, `ComboBoxForegroundBrush`, `ComboBoxBorderBrush` (в светлой теме совпадают с карточками/полями ввода, в тёмной — #3F3F3F / #E0E0E0 / #555555)
  * App.xaml: собственные шаблоны `AppComboBoxItemStyle` (пункт списка) и `AppComboBoxTemplate` (сам ComboBox с Popup) вместо системных — белый системный Popup и системная подсветка были причиной нечитаемого списка в тёмной теме; шаблон поддерживает оба режима (обычный и `IsEditable` с PART_EditableTextBox)
  * App.xaml: добавлены стили `DataGridCell` (цвет текста из палитры, выделенная ячейка — акцент), `ListBox`/`ListBoxItem` (фон карточки + собственная подсветка), `DatePicker`/`DatePickerTextBox` (цвет даты и курсора), `ProgressBar`; в неявный стиль `DataGrid` добавлен Foreground
  * MainWindow.xaml: стиль `WatermarkComboBox` теперь наследует неявный стиль ComboBox (`BasedOn`), поэтому поле УНП использует новый шаблон; сохранён прежний вид — Margin=0 и фон CardBackgroundBrush (в тёмной теме поле остаётся цвета карточки, как раньше)
  * Views/SettingsWindow.xaml: список «Источник API» привязан к каталогу `AvailableApis` (DisplayMemberPath=DisplayTitle, SelectedValuePath=Key), поле «Адрес API» стало только для чтения и привязано к `ApiUrl`; убран локальный Background у ComboBox (цвета берёт стиль)
  * ViewModels/SettingsViewModel.cs: каталог `ApiOption` (Nalog / Nbrb / Belstat), свойство `ApiUrl` (пересчитывается при смене `SelectedApi`), сохранение адреса выбранного источника в его секцию appsettings.json; адрес ApiSettings.BaseUrl гарантированно остаётся адресом Налоговой службы, чтобы поиск по УНП не сломался
  * Проверено UI-автоматизацией (UIA + скриншоты) в обеих темах: выпадающий список и его пункты читаемы, дата в DatePicker видна, строки и заголовки DataGrid читаемы, история курсов (ListBox) читаема; смена API меняет URL, сохраняется в appsettings.json и применяется после перезапуска

  * Шаг 8, баг №1 — столбец «Действия» истории УНП: кнопки-иконки без подписи использовали базовый стиль кнопок с MinWidth=100 и Padding=8,4 и рисовались как пустые синие прямоугольники ~100x36 внутри строки DataGrid (RowHeight=34). Добавлен стиль `IconButton` (App.xaml: BasedOn AppButtonBase, MinWidth=0, Width=Height=28, Padding=0, Margin=2), у обеих кнопок в MainWindow.xaml задан Style="{StaticResource IconButton}"
  * Шаг 8, баг №2 — окно «Настройки», список «Тема оформления»: `AppComboBoxItemStyle` заливал акцентом и наведение, и выбор, поэтому оба пункта (Светлая / Тёмная) выглядели выбранными. Теперь наведение — LightAccentBrush + TextPrimaryBrush, выбор — AccentBrush + OnAccentBrush; добавлена кисть `LightAccentBrush` (App.xaml #DCE6FA, App.xaml.cs: светлая #DCE6FA / тёмная #324A7D)
- ШАГ 9 — новая вкладка «📊 Госстатистика» (публичный API Белстата, SDMX-JSON) — ВЫПОЛНЕНО:
  * Каталог: дерево рубрик, показатели выбранной рубрики, поиск по каталогу показателей; период — диапазон лет (до 25 лет, начиная с 1990)
  * Результат — «плоская» таблица значений: колонки строятся по измерениям ответа SDMX-JSON (названия измерений становятся именами колонок DataTable)
  * Экспорт PDF/Excel/CSV, копирование значения ячейки по двойному клику, история запросов в локальной базе приложения
  * Ленивая инициализация вкладки (BelstatTab_Loaded → InitializeAsync, защита флагом _isInitialized) — по образцу вкладки «💱 Курсы валют»
  * Источник «Белстат» в окне «Настройки» больше не помечается «в разработке» (ApiOption.IsImplemented = true)
## ТЕКУЩИЙ ЭТАП
ЭТАП 1 (критические баги и структура) — выполнен; проект собирается без ошибок и предупреждений (dotnet build).
Предыдущий шаг 3 — вкладка "💱 Курсы валют" (НБ РБ): реализация завершена, выполнена проверка headless-прогоном (реальный API НБ РБ + SQLite + экспортёры) — 24 из 24 проверок пройдено.
Шаг 4 — три правки перед ЭТАПОМ 2: сообщения API 400, стандартная рамка окон, полный перевод цветов на ключи темы.
Шаг 5 — UX вкладки «💱 Курсы валют»: ToggleButton-переключатели режимов, поиск по справочнику валют, копирование ячейки по двойному клику, единый ряд кнопок экспорта, контраст тёмной темы для TextBox/ComboBox. Проект собирается без ошибок и предупреждений.
Шаг 6 — доработка поиска валют (кнопка «🔍 Найти», подсказка-плейсхолдер, Enter, ApplyCurrencyFilterCommand) и вёрстка PDF отчётов по курсам (шапка на первой странице, подпись убрана из подвала, подзаголовок про НБ РБ, корректные заголовки; позже подпись перенесена вниз последней страницы — Шаг 10). Проект собирается без ошибок и предупреждений.
ЭТАП 3 — фирменный стиль СК: жёлтый индикатор активной вкладки, жёлтая линия под шапкой главного окна, жёлтая нижняя рамка шапки окна настроек; цвет заголовка вкладки задаётся на уровне TabItem, поэтому активная вкладка читается в обеих темах (подробности — «ЭТАП 3 — ДЕТАЛИ РЕАЛИЗАЦИИ»). Проект собирается без ошибок и предупреждений.
Шаг 7 — читаемость тёмной темы (ComboBox с собственным Popup-шаблоном, DataGridCell, ListBox, DatePicker) и рабочий выбор источника API в настройках (каталог источников, поле «Адрес API» меняется при смене API, значение сохраняется и применяется после перезапуска). Проверено UI-автоматизацией в обеих темах (подробности — «ШАГ 7 — ДЕТАЛИ РЕАЛИЗАЦИИ»). Проект собирается без ошибок и предупреждений.

Шаг 8 — два визуальных бага: кнопки-иконки в столбце «Действия» истории УНП (новый стиль `IconButton`) и подсветка обоих пунктов списка «Тема оформления» в окне «Настройки» (наведение и выбор теперь различаются). Проект собирается без ошибок и предупреждений.
Шаг 9 — новая вкладка «📊 Госстатистика»: каталог рубрик и показателей Белстата, значения за диапазон лет, таблица результата с автогенерируемыми колонками, экспорт PDF/Excel/CSV, история запросов; источник «Белстат» в настройках больше не помечается «в разработке» (подробности — «ШАГ 9 — ДЕТАЛИ РЕАЛИЗАЦИИ»). Проект собирается без ошибок и предупреждений.
Шаг 10 — подпись «Проверил следователь» прижата к низу последней страницы во всех PDF-отчётах (без лишних страниц), данные Белстата показываются на русском (справочник структуры данных API + резервный словарь BelstatLocalizationHelper); проверено многстраничными пробниками. Проект собирается без ошибок и предупреждений.

Шаг 11 — поиск показателей во вкладке «📊 Госстатистика»: «живой» фильтр по мере ввода, нечёткий поиск FuzzySharp (порог 70 %) при отсутствии точных совпадений, фильтрация дерева рубрик по строке поиска, кнопка «Очистить», краевые случаи (пустой запрос, короткий запрос, числовой запрос, нет результатов). Экспорт, загрузка данных и обращения к API не изменялись (подробности — «ШАГ 11 — ДЕТАЛИ РЕАЛИЗАЦИИ»). Проект собирается без ошибок и предупреждений.
Шаг 13 — UX вкладки «📊 Госстатистика» (5 улучшений): разделитель высоты между каталогом показателей и таблицей результатов (GridSplitter + карточка каталога в ScrollViewer), кнопка «Открыть в окне» и новое окно `Views/BelstatDataWindow` для крупного просмотра таблицы (экспорт PDF/Excel/CSV работает и из окна), увеличенная по умолчанию область результатов на вкладке, проверка CanExecute команды «Отмена» (подробности — «ШАГ 13 — ДЕТАЛИ РЕАЛИЗАЦИИ»). Проект собирается без ошибок и предупреждений; окно результатов проверено запуском приложения.

## ЧТО ДЕЛАЕМ ДАЛЬШЕ
- Ручная проверка вкладки в UI (диалоги сохранения файлов, светлая/тёмная тема, переключение режимов)
- Тёмная тема автоматически проверена в ШАГЕ 7 (UIA + скриншоты по всем вкладкам, окну настроек и выпадающему списку); при ручной проверке — смотреть на прогресс-бары и подсказки-плейсхолдеры, они проверены только по цветам палитры
- Проверка, что при 400 (несуществующий УНП) появляется только запись в статусной строке, без модального окна
- Опционально: график динамики курса вместо/вместе с таблицей
- Опционально: калькулятор пересчёта суммы по курсу, сохранение избранных валют в настройках
- Опционально: тестовый проект (xUnit) для NBRBApiService и ExchangeRateViewModel
- Ручная проверка вкладки «📊 Госстатистика» в UI: реальные запросы к API Белстата (нужна сеть), диалоги сохранения PDF/Excel/CSV, обе темы, длинные таблицы (горизонтальная прокрутка, ширина колонок)
- Ручная проверка подписи «Проверил следователь» в PDF: длинные отчёты (несколько страниц) — подпись только на последней странице внизу, над «Страница N из M», без пустых страниц
- Ручная проверка локализации данных Белстата на русский (названия измерений и расшифровки значений) на реальных запросах при доступной сети
- Ручная проверка поиска показателей (ШАГ 11): ввод по буквам в «Поиск показателя», появление списка результатов, клик по результату (автовыбор рубрики + показателя), кнопка «Очистить», сужение списка рубрик, поведение при опечатке и при отсутствии совпадений
- Ручная проверка ШАГА 13: перетаскивание разделителя высоты (каталог ↔ таблица результатов, упор в MinHeight 96/220), кнопка «Открыть в окне» (то же содержимое и экспорт, Esc, тёмная тема), доступность кнопки «Отмена» только во время загрузки

## ВАЖНЫЕ РЕШЕНИЯ
- API Налоговой поддерживает ТОЛЬКО поиск по УНП
- Все конвертеры находятся в /Converters
- При добавлении новых вкладок — не трогать существующие
- /exrates/currencies содержит архивные дубли: актуальная запись — максимальный Cur_ID по Cur_Abbreviation (иначе rates/dynamics по старому Cur_ID возвращают пусто)
- rates/{id} для устаревшего Cur_ID возвращает null → fallback на rates/{ISO}?parammode=2
- rates?ondate=&periodicity=0 отдаёт только валюты с ежедневной периодичностью (~30), а не весь справочник
- НБ РБ не публикует курс на выходные/праздники (404) — в UI это предупреждение, а не ошибка
- EPPlus: перед созданием документа обязателен ExcelPackage.License.SetNonCommercialPersonal("NalogParserWPF")
- CSV пишется в UTF-8 с BOM и разделителем ";", PDF — QuestPDF (A4, цвета #1A5C5C / #1A8B8B)
- Вкладка «📊 Госстатистика»: таблица результата — WPF DataGrid с AutoGenerateColumns="True", ItemsSource="{Binding ResultTable.DefaultView}" (сам DataTable как источник не годится — колонки не строятся); заголовки авто-колонок берутся из имени колонки DataTable (ColumnName, не Caption), а имена колонок уже содержат расшифровки измерений Белстата
- КЛЮЧЕВЫЕ ГРАБЛИ DataGrid + DataTable: имена измерений содержат точку («г. Минск»), а WPF читает точку в пути привязки как разделитель шагов, поэтому ячейка молча остаётся пустой. Решение — в code-behind (BelstatDataGrid_AutoGeneratingColumn) путь привязки переводится в индексную форму "[имя колонки]" (Mode=OneWay, MinWidth=90), которая работает для DataRowView с любыми именами; заголовку явно ставится e.PropertyName
- Колонки DataGrid генерируются не в момент присвоения ItemsSource, а на первом проходе разметки (Measure/Arrange) — читать Columns.Count сразу после ItemsSource бессмысленно (всегда 0)
- API Белстата (SDMX-JSON, без авторизации): rubric/rubricTreeNodeGet/0 — дерево рубрик, rubric/rubricIndicatorsGet/{code} — показатели рубрики, indicator/indicatorSearch (POST с пустым телом) — каталог и поиск показателей, sdmx-api/indicator/values/SDMX-JSON/{code} — значения показателя, sdmx-api/rubric/indicators/values/SDMX-JSON/{code} — значения всех показателей рубрики (ответ может быть ZIP с файлами *_sdmx_values.json). Кэш: справочники — 1 час, значения — 5 минут
- Экспорт вкладки «Госстатистика» использует общие сервисы проекта (PDF — QuestPDF, Excel — EPPlus с ExcelPackage.License.SetNonCommercialPersonal("NalogParserWPF"), CSV — UTF-8 с BOM и разделителем ";")
- ContextMenu (DataGrid/ListBox/Border) живёт в отдельном дереве (Popup), поэтому привязки внутри него НЕ должны ссылаться на владельца через `{x:Reference}` — это даёт циклическую зависимость MarkupExtension при загрузке окна; правильный способ — `RelativeSource={RelativeSource AncestorType=ContextMenu}` + `PlacementTarget`/`PlacementTarget.DataContext`. Для элементов внутри обычного визуального дерева (шаблоны ячеек, кнопки в строках) `RelativeSource AncestorType=DataGrid` работает как раньше

## РУЧНЫЕ ПРАВКИ (НЕ ТРОГАТЬ)
[Пользователь вписывает сюда всё, что сделал вручную — агент обязан это сохранять]
Пользователь: [я подправил только внешний вид - записи сделал "Информация из официальных источников" на всех страницах, сделал также  для других страниц, и в окне "о программе" внес нужную информацию, там ничего тоже не надо менять - потом только версия при релизе поменяется"]
- Титул программы: изменён с "Проверка УНП" на "ОПЦИОН" (MainWindow.xaml → Window.Title="ОПЦИОН") — это ручная правка пользователя, менять нельзя
- Агент: правки пользователя сохранены — плашки «Информация из официальных источников», содержимое окна «О программе» (Views/AboutWindow.xaml) и титул "ОПЦИОН" не изменяются; ранее титул был ошибочно возвращён к «Проверка контрагентов по УНП», теперь восстановлен в "ОПЦИОН"
- Кнопка «Очистить историю» в блоке истории запросов статистики: скорректирована вручную, чтобы влезала в отведённую область (не сжимать и не менять размер без запроса).

## ПЛАН РАБОТ (текущий)
- ЭТАП 1: Критические баги и структура — ВЫПОЛНЕНО:
  * ✅ Заменить money.ico на zaglav.ico (иконка приложения)
  * ✅ Перенести кнопку "Массовый поиск УНП" в вкладку MainWindow (третья вкладка)
  * ✅ Исправить тёмный прямоугольник в окне "Просмотр записи из истории"
  * ✅ Настроить размеры всех окон (чтобы всё влезало)
  * ✅ Перевести время на локальное РБ (UTC+3)
  * ✅ Ошибка невалидности УНП — показывать в StatusMessage, а не MessageBox
- ЭТАП 2: UI/UX и единый стиль:
  * Единая шапка на всех вкладках (кроме "О программе"):
    - Строка 1: "Следственный комитет Республики Беларусь"
    - Строка 2: "Информация из официальных источников"
  * Кнопки "Настройки" и "О программе" на ВСЕХ вкладках
  * Поля даты — увеличить ширину — ВЫПОЛНЕНО: DatePicker на вкладке «Курсы валют» — MinWidth=180, MinHeight=32, Language=ru-RU (дата видна полностью в формате dd.MM.yyyy), правая колонка расширена до 500
  * Размеры окон — достаточные для отображения всей информации
- ЭТАП 3: Дизайн и иконки:
  * Bootstrap Icons — ВЫПОЛНЕНО: шрифтовые иконки `iconPacks:PackIconBootstrapIcons` (неявный стиль 16×16 в App.xaml, шаблоны «иконка + подпись» Icon*Template) на вкладках, кнопках, в шапке и окнах
  * Цветовая схема: васильковый (#1A3A8F) + жёлтые вкрапления (#F5C518) — ВЫПОЛНЕНО: жёлтая полоса под активной вкладкой, жёлтая линия под шапкой главного окна, жёлтая нижняя рамка шапки окна настроек; контраст заголовков вкладок исправлен в обеих темах
  * Окно настроек — ВЫПОЛНЕНО: выбор API (НБ РБ), логотип СК (sk-logo-2.png), уточнённые надписи
  * Опционально (не входит в текущий объём): жёлтая линия-акцент под шапками диалогов «Просмотр записи», «Изменение статуса», «Массовый поиск» — сейчас акцент есть только в главном окне и в окне настроек
- Шаг 7: Читаемость тёмной темы и источник API — ВЫПОЛНЕНО:
  * ✅ ComboBox: собственные шаблоны списка и пунктов (палитра вместо системных цветов), читаемы в обеих темах — все ComboBox проекта (УНП, валюта, «Источник API», «Тема оформления»)
  * ✅ DatePicker: дата и курсор — из палитры (в тёмной теме дата была не видна)
  * ✅ DataGrid: цвет текста ячеек из палитры + акцентная подсветка выделенной строки
  * ✅ ListBox истории курсов: фон карточки вместо белого системного, своя подсветка пунктов
  * ✅ Окно настроек: выбор API меняет поле «Адрес API», значение сохраняется в appsettings.json и применяется при следующем запуске
  * ℹ️ Календарь DatePicker (всплывающая панель) остался в системном оформлении — отдельная задача, если потребуется
- Шаг 9: Вкладка «📊 Госстатистика» (Белстат) — ВЫПОЛНЕНО:
  * ✅ Каталог: дерево рубрик (`rubric/rubricTreeNodeGet/0`), показатели рубрики (`rubric/rubricIndicatorsGet/{code}`), поиск по каталогу показателей (`indicator/indicatorSearch`)
  * ✅ Значения: одного показателя (`sdmx-api/indicator/values/SDMX-JSON/{code}`) или всех показателей рубрики (`sdmx-api/rubric/indicators/values/SDMX-JSON/{code}`, ответ может быть ZIP)
  * ✅ Период — диапазон лет (два ComboBox, до 25 лет, начиная с 1990), отмена долгого запроса, прогресс-бар
  * ✅ Таблица результата (DataGrid: измерения из ответа + «Значение»), копирование значения по двойному клику
  * ✅ Экспорт PDF/Excel/CSV, история запросов в локальной БД SQLite
  * ✅ Источник «Белстат» в окне «Настройки» больше не помечается «в разработке»

## ЭТАП 1 — ДЕТАЛИ РЕАЛИЗАЦИИ
- NalogParserWPF.csproj: `ApplicationIcon` = zaglav.ico; элемент `Content money.ico` заменён на `Resource zaglav.ico`
- Views/BatchSearchView.xaml (+ .xaml.cs) — единый UserControl с разметкой массового поиска (получен переносом содержимого BatchSearchWindow.xaml); собственная шапка с логотипом удалена (шапка в приложении одна — в MainWindow), заголовок вкладки — «🔍 ПОИСК ПО НЕСКОЛЬКИМ УНП»
- BatchSearchWindow.xaml — тонкая оболочка: хостит BatchSearchView + кнопка «Закрыть» (или Esc)
- MainWindow.xaml — 4 вкладки в порядке: Поиск по УНП / Массовый поиск (BatchSearchView, DataContext={Binding BatchSearch}) / История проверок УНП / Курсы валют; из шапки убрана кнопка массового поиска; исправлена сетка (шапка — строка 0, вкладки — строка 2, футер — строка 3); DatePicker на вкладке «Курсы валют» — MinWidth=180, MinHeight=32, Language=ru-RU (дата dd.MM.yyyy видна полностью), ширина правой колонки 500
- MainViewModel: добавлено свойство `BatchSearch` (BatchSearchViewModel); ошибка невалидного УНП → `StatusMessage = "УНП должен состоять из 9 цифр"` (уведомление-MessageBox убрано); окно «О программе» получает Owner
- Converters/EmptyStringToVisibilityConverter.cs — скрывает пустые плашки статуса (артефакт «тёмный прямоугольник»)
- Helpers/DateTimeHelper.cs — `NowBelarus` / `TodayBelarus` (UTC+3); применён в DatabaseHelper, ExchangeHistoryRepository, PdfExportService, NBRBApiService, BatchSearchViewModel, ExchangeRateViewModel, HistoryDetailViewModel, MainViewModel
- DatabaseHelper.AddAsync: `SearchDate` пишется явно (ранее `DEFAULT CURRENT_TIMESTAMP` давал время UTC)
- InMemoryCacheService оставлен на DateTime.UtcNow — кэш измеряет абсолютные интервалы, часовой пояс не важен

## ШАГ 5 — ДЕТАЛИ (UX вкладки «Курсы валют»)
- MainWindow.xaml: новый стиль `ModeToggleButton` (TargetType=ToggleButton, кастомный ControlTemplate) — используется тремя переключателями режимов вместо RadioButton; в шаблоне триггеры IsChecked (акцентный фон + OnAccentBrush), IsMouseOver (Opacity 0.9), IsEnabled (Opacity 0.5)
- ViewModels/ExchangeRateViewModel.cs: приватный `SetMode(single, all, dynamics)` — взаимоисключающий выбор режима; сеттеры IsSingleRateMode/IsAllRatesMode/IsDynamicsMode при `false` для активного режима вызывают только OnPropertyChanged (снятие активного режима игнорируется)
- ViewModels/ExchangeRateViewModel.cs: `FilteredCurrencies` (ObservableCollection<Currency>) + `CurrencyFilter` (string) + `ApplyCurrencyFilter()` / `MatchesCurrencyFilter()` — фильтр по Cur_Abbreviation, Cur_Name, Cur_Name_Eng, Cur_Code (OrdinalIgnoreCase); пересборка после загрузки справочника и при смене фильтра; SelectedCurrency при необходимости возвращается на позицию 0
- MainWindow.xaml: блок выбора валюты перестроен — поле «🔎 Поиск валюты» (TextBox → CurrencyFilter, UpdateSourceTrigger=PropertyChanged) + ComboBox на FilteredCurrencies (IsEditable=True, IsTextSearchEnabled=True, TextSearch.TextPath=DisplayName)
- MainWindow.xaml: обеим таблицам результатов добавлены MouseDoubleClick="DataGrid_MouseDoubleClick" и ToolTip-подсказка
- MainWindow.xaml.cs: обработчик `DataGrid_MouseDoubleClick` → `DataGridHelper.CopyCellContent(sender, e)` (единый механизм с остальными окнами)
- App.xaml: базовые стили TextBox и ComboBox получили Setter Foreground=TextPrimaryBrush; TextBox — дополнительно CaretBrush=TextPrimaryBrush

## ШАГ 6 — ДЕТАЛИ (поиск валют + PDF отчётов по курсам)
- ViewModels/ExchangeRateViewModel.cs: новая команда `ApplyCurrencyFilterCommand` (RelayCommand) → вызывает существующий приватный `ApplyCurrencyFilter()`; инициализируется в конструкторе рядом с остальными командами
- MainWindow.xaml: новый стиль `CurrencySearchTextBox` (TargetType=TextBox) — плейсхолдер «Введите код валюты (USD, EUR) или название» через `Style.Triggers` + `Trigger Property="Text" Value=""` и VisualBrush (без сторонних библиотек), акцентная рамка, ToolTip; рядом с полем — кнопка «🔍 Найти» (Style=SearchButton), привязана к `ApplyCurrencyFilterCommand`
- MainWindow.xaml.cs: обработчик `CurrencySearchBox_KeyDown` — Enter применяет `ApplyCurrencyFilterCommand` (пустой фильтр = сброс, показываются все валюты)
- Services/PdfExportService.cs: константа `CurrencyHeaderSubText` = «Информация о курсах валют по данным НБ РБ»; перегрузка `BuildHeader(container, subText)` и обёртки `BuildHeader` / `BuildCurrencyHeader`
- Services/PdfExportService.cs: `BuildExchangeRatePage` / `BuildAllRatesPage` / `BuildDynamicsPage` — шапка `page.Header().ShowOnce()` (только первая страница); в конце тела страницы — `column.Item().Element(BuildSignature)`, подпись прижата к низу последней страницы (ШАГ 10); подвал `BuildCurrencyFooter` отвечает только за номер страницы (на каждой странице)
- Services/PdfExportService.cs: `BuildCompanyPage` (ExportCompanyInfoAsync / ExportHistoryEntryAsync) и `BuildBatchPage` (массовый поиск) — `page.Header().ShowOnce()`; подпись — тем же `BuildSignature` в конце тела страницы (в массовом поиске каждый результат и так начинается с новой страницы)

## ЭТАП 3 — ДЕТАЛИ РЕАЛИЗАЦИИ
- MainWindow.xaml, стиль `TabItemStyle`: индикатор активной вкладки — в триггере `IsSelected` ControlTemplate заданы `Border.BorderBrush = SecondaryBrush` (#F5C518) и `BorderThickness = 0,0,0,3` (жёлтая полоса по нижней кромке вместо прежней рамки цветом AccentBrush, которая на васильковом фоне была не видна)
- ВАЖНО (грабли WPF): цвет заголовка вкладки НЕЛЬЗЯ задавать сеттером `TextBlock.Foreground` по `ContentSite` и нельзя ставить `TextBlock.Foreground="{TemplateBinding Foreground}"` на ContentPresenter шаблона. Заголовки вкладок — это StackPanel (иконка + текст), у которых логический родитель — TabItem, поэтому наследуемое значение Foreground берётся от TabItem, а не от ContentPresenter: сеттер внутри ControlTemplate до содержимого заголовка не доходит, заголовок остаётся цвета TextAccentBrush и «исчезает» на васильковом фоне активной вкладки. В тёмной теме дефект был незаметен, потому что TextAccentBrush тёмной темы = #9DB4F0 (светлый) — отсюда «виден только в светлой теме»
- Правильное решение (реализовано): триггеры цвета помещены в `Style.Triggers` самого `TabItemStyle` и меняют свойство `Foreground` у TabItem:
  * `IsSelected = True` → `OnAccentBrush` (#FFFFFF)
  * `IsMouseOver = True` → `TabHoverForegroundBrush`
  * `MultiTrigger (IsSelected + IsMouseOver)` → `OnAccentBrush`
  Содержимое заголовка (иконка + текст) наследует Foreground от TabItem, поэтому цвет применяется ко всему заголовку
- Порядок триггеров важен: `MultiTrigger (IsSelected + IsMouseOver)` объявлен последним, иначе при наведении на активную вкладку применялся бы `TabHoverForegroundBrush` (в светлой теме #12276B — на васильковом фоне не читается)
- Иконка `PackIconBootstrapIcons` наследует Foreground (неявный стиль в App.xaml задаёт только Width/Height/VerticalAlignment), поэтому цвет заголовка применяется и к иконке — отдельных правок иконок не требуется
- Триггеры ControlTemplate по-прежнему отвечают только за фон/рамку Border активной и hover-вкладки (там TargetName обязателен, `Style.Triggers` его не поддерживают)
- Проверка «глазами» без ручного клика: окно определяется через UI Automation, снимаются `BoundingRectangle` вкладок, область активной вкладки копируется `CopyFromScreen`, дальше — гистограмма цветов и увеличение nearest-neighbor. Светлая тема: фон #1A3A8F, текст/иконка #FFFFFF, полоса #F5C518; тёмная тема: фон #4A6FD4, текст/иконка #FFFFFF, полоса #F5C518. Тёмная тема воспроизводится временной вставкой `"Theme": "Dark"` в `bin/.../appsettings.json` (с бэкапом и восстановлением)
- Кнопка «🔍 Найти» (MainWindow.xaml, вкладка «Курсы валют»): собственный `ContentTemplate` не нужен — стиль `SearchButton` уже задаёт `ContentTemplate = IconSearchTemplate` (иконка + `{Binding}`), а локальный `Content="Найти"` перекрывает только текст из стиля, поэтому кнопка корректно рисуется как «иконка + Найти»

## ШАГ 7 — ДЕТАЛИ РЕАЛИЗАЦИИ
- ВАЖНО (грабли WPF, причина нечитаемости ComboBox в тёмной теме): системный шаблон Aero2 рисует выпадающий список кистью `SystemColors.WindowBrushKey` (белой) и не подчиняется палитре приложения, а подсветку пункта берёт из системных цветов. Приложение задаёт Foreground светлым цветом тёмной темы (#E8EDF7) — пункты оказывались светлыми на белом фоне. Поэтому в App.xaml добавлены собственные `AppComboBoxTemplate` (ControlTemplate для ComboBox) и `AppComboBoxItemStyle` (Style для ComboBoxItem)
- `AppComboBoxTemplate`: Border (Background/BorderBrush/BorderThickness через TemplateBinding, CornerRadius=4) → ContentPresenter (SelectionBoxItem) + TextBox `PART_EditableTextBox` + ToggleButton с иконкой ChevronDown; Popup `PART_Popup` (AllowsTransparency, Placement=Bottom, IsOpen через TemplateBinding) с Border (CardBackgroundBrush + ComboBoxBorderBrush) и ItemsPresenter внутри ScrollViewer
- Обязательные части шаблона ComboBox: `PART_Popup`, `PART_EditableTextBox` и ToggleButton с TwoWay-привязкой к IsDropDownOpen. Без них редактируемые ComboBox (поле УНП, выбор валюты) теряют ввод/выпадающий список
- У `PART_EditableTextBox` явно указан `Style="{x:Null}"` — это отключает неявный стиль TextBox приложения (иначе внутри списка появляется вторая рамка и фон); Foreground и CaretBrush берутся у ComboBox через TemplateBinding
- `AppComboBoxItemStyle`: Foreground=TextPrimaryBrush, Background=CardBackgroundBrush, наведение (IsHighlighted) = LightAccentBrush + TextPrimaryBrush, выбор (IsSelected) = AccentBrush + OnAccentBrush, IsEnabled=False → Opacity 0.5; подсветка объявлена в Style.Triggers, поэтому перекрывает базовые сеттеры
- Неявный стиль ComboBox: Background=ComboBoxBackgroundBrush, Foreground=ComboBoxForegroundBrush, BorderBrush=ComboBoxBorderBrush, ItemContainerStyle=AppComboBoxItemStyle; наведение и фокус меняют рамку на AccentBrush
- MainWindow.xaml: `WatermarkComboBox` получил `BasedOn="{StaticResource {x:Type ComboBox}}"` — явный ключевой стиль полностью отменяет неявный, и без BasedOn поле УНП оставалось на системном шаблоне (белый Popup). `Margin="0"` сохраняет прежнюю геометрию поля, фон оставлен CardBackgroundBrush (в тёмной теме поле УНП выглядит как раньше)
- DataGrid: в неявный стиль добавлен Foreground=TextPrimaryBrush; добавлен неявный стиль `DataGridCell` (Foreground=TextPrimaryBrush, Background/BorderBrush=Transparent, Padding 8,4, FocusVisualStyle=null) с триггером IsSelected (AccentBrush + OnAccentBrush). Без него текст ячеек рисовался системным цветом и в тёмной теме был чёрным по тёмному
- ListBox/ListBoxItem: фон списка — CardBackgroundBrush (системный белый делал историю курсов нечитаемой в тёмной теме), подсветка пункта — IsMouseOver=TabHoverBrush, IsSelected=HeaderBackgroundBrush + TextPrimaryBrush. Пункту задан собственный ControlTemplate: триггеры системного шаблона правят Border по имени (`TargetName="Bd"`) и перекрывали бы сеттеры стиля
- DatePicker/DatePickerTextBox: Background=ComboBoxBackgroundBrush, Foreground/CaretBrush=ComboBoxForegroundBrush, BorderBrush=ComboBoxBorderBrush — в тёмной теме дата была чёрной на тёмном фоне. Всплывающий календарь остаётся системным (вне объёма шага)
- ProgressBar: Foreground=AccentBrush (полоса), Background=GridLineBrush (дорожка), BorderThickness=0
- НЕ добавлять неявный стиль TextBlock с Foreground: шаблоны кнопок `Icon*Template` (App.xaml) содержат `<TextBlock Text="{Binding}"/>`, который обязан наследовать цвет кнопки (OnAccentBrush, белый) — неявный стиль сделал бы подписи тёмными на акцентном фоне
- ViewModels/SettingsViewModel.cs: `ApiOption` (Key, SectionName, Title, Url, IsImplemented, DisplayTitle, ToString→DisplayTitle для UI Automation), каталог `ApiOptions`: Nalog → ApiSettings, Nbrb → NbrbSettings, Belstat → BelstatSettings (на ШАГЕ 8 источники «Белстат» помечались «в разработке» — клиент тогда ещё не был реализован, см. Docs/BELSTAT_API_RESEARCH.md; с ШАГА 9 пометка снята, IsImplemented = true)
- ViewModels/SettingsViewModel.cs: `AvailableApis`, `ApiUrl => GetApiOption(SelectedApi).Url`; сеттер `SelectedApi` дополнительно вызывает OnPropertyChanged(ApiUrl), поэтому адрес меняется сразу при выборе; неизвестный ключ нормализуется в «Nalog»
- SettingsViewModel.Save: адрес выбранного источника пишется в его собственную секцию, ApiSettings.BaseUrl заполняется адресом Налоговой службы, если он был пуст (поиск по УНП работает только через grp.nalog.gov.by — смена «источника» его не ломает), тема и SelectedApi — в корень appsettings.json
- Проверка: скрипты UI-автоматизации лежат в `%TEMP%\nps_diag` (`diag5.ps1`…`diag8.ps1`): UIA (TabItem.SelectionItemPattern, ComboBox.ExpandCollapsePattern/ValuePattern), CopyFromScreen, зум nearest-neighbor. Отдельно проверено: смена API (URL до/после выбора, содержимое appsettings.json, повторный запуск) и заполнение таблицы курсов в тёмной теме

## ШАГ 8 — ДЕТАЛИ РЕАЛИЗАЦИИ
- Баг №1 (MainWindow.xaml, вкладка истории УНП, столбец «Действия»): обе кнопки столбца — иконки без подписи, но использовали неявный стиль кнопок (AppButtonBase) с MinWidth=100 и Padding=8,4, поэтому занимали ~100x36 внутри строки DataGrid (RowHeight=34) и выглядели как пустые синие прямоугольники. Шаблоны иконок `IconRepeatOnlyTemplate` / `IconViewOnlyTemplate` (16px PackIconBootstrapIcons) были исправны — размер задавал контейнер кнопки, а не шаблон
- Решение: стиль `IconButton` в App.xaml (BasedOn AppButtonBase, MinWidth=0, Width=Height=28, Padding=0, Margin=2, HorizontalContentAlignment/VerticalContentAlignment=Center). Foreground/Background наследуются от AppButtonBase (OnAccentBrush на ButtonBackgroundBrush), поэтому иконка читаема в обеих темах (светлая: #FFFFFF на #1A3A8F, тёмная: #FFFFFF на #4A6FD4)
- Баг №2 (Views/SettingsWindow.xaml, ComboBox «Тема оформления»): `AppComboBoxItemStyle` применял AccentBrush + OnAccentBrush к обоим состояниям — и к IsHighlighted, и к IsSelected, поэтому оба пункта (Светлая / Тёмная) выглядели выбранными
- Решение: IsHighlighted = LightAccentBrush + TextPrimaryBrush, IsSelected = AccentBrush + OnAccentBrush, IsEnabled=False = Opacity 0.5. Триггер IsSelected объявлен после IsHighlighted, поэтому при наведении на выбранный пункт остаётся акцентная заливка (в Style.Triggers побеждает объявленный последним сработавший триггер)
- Новая кисть `LightAccentBrush`: App.xaml #DCE6FA (значение светлой темы), App.xaml.cs ApplyTheme(light #DCE6FA, dark #324A7D). Контраст состояний: светлая тема — наведение #DCE6FA против выбора #1A3A8F, тёмная — наведение #324A7D против выбора #4A6FD4
- В списке «Тема оформления» пункты заданы явными `<ComboBoxItem>` (без привязки), а состояния берутся из ItemContainerStyle — правка стиля действует и на них
- Проверка: `dotnet build` — 0 ошибок, 0 предупреждений; цвета наведения и выбора различаются в обеих темах
- Проверка UI (скрипт `%TEMP%\nps_diag11\diag11.ps1`: UIA + CopyFromScreen + зум nearest-neighbor, светлая и тёмная темы): кнопки «Действия» — квадраты 35x35 px при масштабе системы 125% (то есть ровно 28 DIP), заливка #1A3A8F (тёмная тема #4A6FD4) с белой иконкой, расстояние между кнопками 40 px (32 DIP = 28 + Margin 2+2); список «Тема оформления» — выбранный пункт #1A3A8F с белым текстом, наведённый пункт #DCE6FA (тёмная тема #324A7D) с тёмным (в тёмной теме светлым) текстом, второй пункт — фон карточки (#FFFFFF / #1B2A47). Отчёт `%TEMP%\nps_diag11\report.txt`, снимки LIGHT/DARK_actions_row.png и LIGHT/DARK_theme_combo.png
- Проверка контраста иконок (Bootstrap Icons, вектор, а не emoji): все использования `PackIconBootstrapIcons` наследуют Foreground от родителя — в шаблонах кнопок это OnAccentBrush на акцентном фоне, в шапках окон и `SectionHeader` — OnAccentBrush на AccentBrush, в заголовках колонок — TabItemStyle/TextAccentBrush, у шеврона ComboBox — ComboBoxForegroundBrush, в предупреждениях — WarningTextBrush. Иконок, зависящих от системных цветов, нет, правка для обеих тем не требуется
## ШАГ 9 — ДЕТАЛИ РЕАЛИЗАЦИИ (вкладка «📊 Госстатистика»)
- Состав вкладки (MainWindow.xaml, 5-я вкладка — после «💱 Курсы валют»): слева — карточка каталога (ComboBox рубрики, поле «Поиск показателя» + кнопка «Найти», ComboBox показателя, подписи выбранного), карточка периода и действий («Период с … по …» — два ComboBox годов, кнопки «Получить данные» / «Отмена» / PDF / Excel / CSV), прогресс-бар со строкой статуса, заголовок результата (показатель, период, число строк) и таблица; справа — история запросов (ListBox) и кнопки «Обновить историю» / «Очистить историю»
- Разметка вкладки — MainWindow.xaml (строки ~1080–1429), DataContext вкладки — {Binding Belstat}; стиль поля поиска `BelstatSearchTextBox` объявлен в Window.Resources MainWindow.xaml (подсказка-плейсхолдер по пустому тексту + акцентная рамка)
- Состав кода: ViewModels/BelstatViewModel.cs (состояние, команды, загрузка, экспорт, история), Models/BelstatModels.cs (модели SDMX-JSON: рубрики, показатели, измерения, значения), Services/BelstatApiService.cs + IBelstatApiService, Database/BelstatHistoryRepository.cs + IBelstatHistoryRepository, Helpers/DataGridHelper.cs (копирование ячейки), Docs/BELSTAT_API_RESEARCH.md (исследование API)
- Конфигурация: секция BelstatSettings в Configuration/AppConfig.cs и appsettings.json (BaseUrl = https://dataportal.belstat.gov.by/osids-public-api/, TimeoutSeconds = 60, Language = "ru"); регистрация сервисов и HttpClient — в App.xaml.cs, ViewModel — в DI, доступ из MainViewModel через свойство Belstat
- Ленивая загрузка: на корневом Grid вкладки стоит Loaded="BelstatTab_Loaded", обработчик вызывает BelstatViewModel.InitializeAsync (дерево рубрик + история запросов), повторные срабатывания отсекаются флагом _isInitialized — как на вкладке «Курсы валют». Автозагрузка по SelectionChanged не используется (в проекте такого механизма нет)
- Кнопки действий не задают IsEnabled: команды сами закрывают себя через CanExecute (!IsLoading / HasSelection / HasExportData / HistoryItems.Count > 0) — конвертера InvertBoolConverter в проекте нет, поэтому IsEnabled-привязок у кнопок нет
- Данные для экспорта хранятся отдельно от таблицы: _resultHeaders + _resultRows (List<string[]>), а таблица — производная (BuildDataTable → ResultTable → DefaultView); поэтому PDF/Excel/CSV не зависят от состояния DataGrid
- Имена колонок DataTable уникализируются (MakeUnique: повтор подписи получает суффикс «(2)»), колонка значений называется «Значение», нераспознанное измерение получает подпись «Колонка N»
- Проверка поведения DataGrid выполнена отдельным WPF-пробником на C# (не PowerShell): простая привязка по имени колонки с точкой оставляет ячейку пустой, индексная форма "[имя]" отдаёт правильное значение, заголовки авто-колонок равны ColumnName. Пробник лежал вне каталога проекта и удалён после проверки
- Проверка сборки: `dotnet build NalogParserWPF\NalogParserWPF.csproj` — 0 ошибок, 0 предупреждений

## ШАГ 10 — ДЕТАЛИ (подпись «Проверил следователь» в PDF + локализация данных Белстата)
- Подпись в PDF формируется за один проход, без `ShowOnce` в теле и без дополнительных страниц: `BuildSignature` (Services/PdfExportService.cs) = `container.ExtendVertical().AlignBottom().PaddingBottom(4).Column(...)`. Контейнер растягивается на всю оставшуюся высоту страницы и прижимает содержимое к нижней кромке, поэтому подпись всегда оказывается внизу последней страницы — над строкой «Страница N из M».
- Правое выравнивание задаётся у самих строк подписи (`column.Item().AlignRight().Text(...)`), а не у контейнера: `AlignRight` на контейнере переопределил бы вертикальное выравнивание, и подпись «отклеилась» бы от нижней кромки.
- Вызовы `column.Item().Element(BuildSignature)` добавлены в конце тела страницы в `BuildCompanyPage`, `BuildBatchPage`, `BuildDynamicsPage`, `BuildExchangeRatePage`, `BuildAllRatesPage`, `BuildBelstatPage`. Из подвалов (`BuildFooter`, `BuildCurrencyFooter`, `BuildBelstatFooter`) подпись убрана — подвалы отвечают только за дату генерации, источник данных и номер страницы.
- Проверка многстраничности (WPF-пробник Docs/_probe/probe_sig_company.cs, 3 сценария: `full-bottom`, `full-tail`, `empty-bottom`): во всех случаях число страниц равно 1 и число PNG равно числу страниц, исключений нет. Грабли QuestPDF: `context.TotalPages` внутри `ShowIf` равен null (рабочий вариант — `context.PageNumber`); `GeneratePdf(string)` ничего не возвращает (для байтов — `GeneratePdf(Stream)`); страницы снимаются `GenerateImages(GenerateDocumentImagePath, ImageGenerationSettings)`.
- Проверка положения подписи по «чернилам» изображения (Docs/_probe/ink.ps1 → Docs/_probe/ink_report.txt): подпись `full-bottom` — 85,5–88,6 % высоты страницы (футер 94,2–95,1 %), `full-tail` — 52,6–55,7 % (сразу после контента), `empty-bottom` — только футер, подписи нет. Смещение ~33 % высоты страницы подтверждает, что `AlignBottom` реально прижимает подпись к низу, а не просто дописывает её в поток.
- Локализация данных Белстата — трёхуровневая. 1) Основной источник русских подписей: `BelstatApiService` запрашивает справочник структуры показателя (`GET sdmx-api/indicator/datastructure/SDMX-JSON/{code}`) и подставляет `names.ru` из схемы понятий в названия измерений, а из классификаторов `CL_*` — в расшифровки значений (`ApplyLocalization`, кэш 6 часов, до 3 справочников на запрос рубрики). 2) Если справочник недоступен, в сервисе применяется резервный словарь `Helpers/BelstatLocalizationHelper.cs` (DimensionNames / ValueNames). 3) `BelstatViewModel.BuildHeaders` / `BuildRowsFromResponse` повторно применяют тот же словарь к `Caption` (`Name ?? Id`) — страховка для путей, где структура пришла без локализации (например, значения рубрики из ZIP).
- Неизвестные подписи словарь не портит: `TranslateDimensionName` / `TranslateValueName` возвращают исходную строку (null остаётся null), поэтому локализация только дополняет данные, а не искажает их.
- Проверка сборки: `dotnet build NalogParserWPF\NalogParserWPF.csproj` — 0 ошибок, 0 предупреждений.

## ТЕМА И ПАЛИТРА
- Все цвета задаются только ключами SolidColorBrush: светлые значения — в App.xaml, тёмные — в App.ApplyTheme (App.xaml.cs)
- В XAML цвета указываются исключительно как {DynamicResource <Key>}; литералы (#hex, White, Gray, LightGray) запрещены. Исключения: определения палитры в App.xaml и ключевое слово Transparent
- Ключи палитры: WindowBackgroundBrush, PanelBackgroundBrush, CardBackgroundBrush, InputBackgroundBrush, ComboBoxBackgroundBrush, ComboBoxForegroundBrush, ComboBoxBorderBrush, TextPrimaryBrush, TextSecondaryBrush, TextMutedBrush, TextAccentBrush, AccentBrush, LightAccentBrush, OnAccentBrush, SecondaryBrush, OnSecondaryBrush, BorderBrush, GridLineBrush, HeaderBackgroundBrush, AltRowBackgroundBrush, TabBackgroundBrush, TabHoverBrush, TabHoverForegroundBrush, ButtonBackgroundBrush, ButtonTextBrush, DividerBrush, WarningBackgroundBrush, WarningBorderBrush, WarningTextBrush, HighlightBackgroundBrush, DangerSoftBrush, DangerBrush, ExportButtonBrush, ExcelButtonBrush, SecondaryButtonBrush, NeutralButtonBrush, OverlayDisabledBrush, OverlayPressedBrush
- `LightAccentBrush` — мягкий акцент для наведения в списках (пункты выпадающих списков): светлая тема #DCE6FA, тёмная #324A7D. Выбранный пункт остаётся AccentBrush (#1A3A8F, тёмная #4A6FD4), поэтому наведение и выбор не выглядят одинаково
- ApplyTheme перекрашивает все ключи (локальная функция Set(key, lightArgb, darkArgb), формат 0xAARRGGBB, учитывается альфа для оверлеев кнопок); тёмная тема применяется и к уже открытым окнам без перезапуска
- Тема выбирается в окне «Настройки» (SettingsViewModel → App.ApplyTheme) и на старте из appsettings.json (ключ Theme)

## СООБЩЕНИЯ ПОЛЬЗОВАТЕЛЮ
- Ожидаемые ответы API (HTTP 400: «УНП не найден в базе», невалидный запрос) — только текст в StatusMessage окна, без MessageBox; тело ответа пишется в лог (ILogger)
- MessageBox (через NotificationService.ShowError/ShowWarning) — только для непредвиденных ситуаций: 5xx, сетевые сбои, ошибки файлов/экспорта, критические исключения
- Массовый поиск: ошибка по конкретному УНП попадает в колонку «Ошибка» строки таблицы, а не в отдельный диалог

## ПРАВИЛА ВНЕШНЕГО ВИДА
- Шапка на всех вкладках (кроме "О программе"):
  * Строка 1: "Следственный комитет Республики Беларусь"
  * Строка 2: "Информация из официальных источников"
- Кнопки "Настройки" и "О программе" должны быть на ВСЕХ вкладках.
- Цветовая схема: васильковый (#1A3A8F, AccentBrush) + жёлтые вкрапления (#F5C518, SecondaryBrush).
- Активная вкладка: васильковый фон + белый текст/иконка (OnAccentBrush) + жёлтая полоса 3px по нижней кромке. Цвет заголовка вкладки задаётся только через `Style.Triggers` стиля `TabItemStyle` (на уровне TabItem), а не сеттерами внутри ControlTemplate.
- Выпадающие списки, поля даты, таблицы и списки оформляются только кистями палитры: в App.xaml для этого есть собственные шаблоны `AppComboBoxTemplate` / `AppComboBoxItemStyle` и стили `DataGridCell`, `ListBox`, `ListBoxItem`, `DatePicker`, `DatePickerTextBox`, `ProgressBar`. Системные цвета Windows в интерфейсе не используются (в тёмной теме они дают белый Popup и чёрный текст).
- Кнопки без подписи (только иконка) используют стиль `IconButton` (MinWidth=0, 28x28, Padding=0): базовый MinWidth=100 растягивает такую кнопку в пустой прямоугольник, особенно заметно в строках DataGrid.
- В списках наведение и выбор обязаны различаться: наведение — `LightAccentBrush` + `TextPrimaryBrush`, выбор — `AccentBrush` + `OnAccentBrush`.
- Иконки: Bootstrap Icons.

## ВРЕМЯ
- Все временные метки — локальное время Республики Беларусь (UTC+3).

## РАЗМЕРЫ
- Окно программы открывается в размере, достаточном для отображения всей информации.
- Дополнительные окна — тоже максимально удобные.
- Поля даты — достаточной ширины.

## РЕШЕНИЯ ПО API
- API Налоговой: только поиск по УНП.
- API НБ РБ: публичный, без авторизации.
- В настройках — выпадающий список источников данных (ШАГ 7): каталог `ApiOption` в SettingsViewModel, поле «Адрес API» только для чтения и следует за выбором, SelectedApi и адрес выбранного источника сохраняются в appsettings.json и применяются при следующем запуске.
- Смена источника не затрагивает адрес секции ApiSettings (если выбран не «Nalog»): поиск по УНП работает только через grp.nalog.gov.by.
- Белстат (https://dataportal.belstat.gov.by/osids-public-api) — клиент реализован: Services/BelstatApiService.cs (SDMX-JSON, без авторизации, кэш справочников и значений), вкладка «📊 Госстатистика»; исследование API — Docs/BELSTAT_API_RESEARCH.md; пометка «в разработке» в списке источников снята (ШАГ 9).

## ЭТАП 10 — UX-ПРАВКИ (6 изменений)
- 1) Переключатель «Кратко / Подробно» (вкладка «Проверка по УНП», MainWindow.xaml ~421–429) — `ToggleButton` со стилем `ModeToggleButton` (Window.Resources MainWindow.xaml, ~49): активное состояние (IsChecked=True) красится AccentBrush и получает жёлтую полосу по нижней кромке, иначе стандартный ToggleButton выглядел серой кнопкой, неотличимой от неактивной. Привязка `IsChecked="{Binding IsDetailedView}"` (TwoWay по умолчанию), иконка — `IconDetailsTemplate`, тот же стиль используется режимами вкладки «Курсы валют».
- 1a) Строка действий результата поиска: «Экспорт в PDF» → «Копировать» (Margin `0,0,8,0`, чтобы кнопки не слипались) → новая кнопка «Очистить» (`IconClearTemplate`, фон `DangerSoftBrush`, `ClearResultCommand`). `MainViewModel.ClearResult()` сбрасывает `CurrentResult`, `CurrentCompany`, `SearchQuery` и ставит StatusMessage «Готов к поиску»; CanExecute команды — `CurrentCompany != null`.
- 2) Кнопка «⏹ Отмена» и стиль `CancelButton` удалены: в App.xaml ключа `CancelButton` больше нет, в MainWindow.xaml нет ни «Отмена», ни привязки `CancelSearchCommand`. Отмена долгого запроса осталась только там, где она уместна: массовый поиск (локальный стиль `CancelButton` в Views/BatchSearchView.xaml + `CancelSearchCommand` в BatchSearchViewModel) и диалог «Настройки» (закрытие без сохранения).
- 3) История проверок УНП (MainWindow.xaml ~601–760) — расширенные операции:
  * `DataGrid x:Name="HistoryGrid"`: `SelectionMode="Extended"` + `SelectionUnit="FullRow"` (Ctrl/Shift — множественное выделение), событие `SelectionChanged="HistoryGrid_SelectionChanged"` пересчитывает CanExecute кнопки массового удаления.
  * Новая колонка «Дата проверки» (`Timestamp`, формат `dd.MM.yyyy HH:mm`, Width=* MinWidth=120).
  * Контекстное меню строки: «Просмотр записи» (ViewHistoryCommand), «Повторить поиск» (RepeatSearchCommand), «Удалить запись» (DeleteHistoryItemCommand); команды и параметр берутся через `PlacementTarget` контекстного меню (`{Binding Path=PlacementTarget.DataContext.…, RelativeSource={RelativeSource AncestorType=ContextMenu}}` и `PlacementTarget.SelectedItem`) — прежний `{x:Reference HistoryGrid}` давал циклическую зависимость разметки (см. «ШАГ 12»), а сама команда дополнительно проверяет параметр (CanExecute `item != null`).
  * «Удалить запись» доступно и как кнопка-иконка в каждой строке (`IconDeleteOnlyTemplate`, стиль `IconButton`), а массовое удаление — кнопкой «Удалить выделенные» (`DeleteSelectedCommand`) рядом с «Очистить всё».
  * Легенда статусов: у заголовка колонки статуса ToolTip `{x:Static helpers:StatusHelper.StatusLegend}`, правый клик по плашке статуса открывает меню с расшифровкой (Действующий / Ликвидирован / В процессе ликвидации + строка «Текущий статус: …»), у самой плашки — ToolTip `StatusHint`.
- 4) Русификация Legal/Physical: `Converters/SearchTypeToRussianConverter.cs` (`ToRussianText`): `Legal`/«юрлицо»/«ЮЛ» → «Юридическое лицо», `Physical`/«физлицо»/«ФЛ» → «Физическое лицо», пусто → «Не указан», иные значения возвращаются как есть. Конвертер зарегистрирован в HistoryDetailWindow.xaml (`x:Key="SearchTypeToRussianConverter"`) и применён к `CurrentEntry.SearchType`; в `ViewModels/HistoryItemViewModel.cs` добавлено свойство `TypeText` (строка записи в списках истории).
- 5) «📊 Госстатистика» — загрузка ранее выполненного запроса из локальной БД:
  * `BelstatViewModel`: `LoadFromHistoryCommand` (RelayCommand, параметр `BelstatHistoryItem`), `LoadFromHistoryAsync`, `ResolveIndicator` (ищет показатель по коду в каталоге, при отсутствии добавляет строку с сохранённым названием, чтобы восстановленные параметры отобразились полностью), `ParseYears` (разбирает «2021-2023» и «2023» в границы периода), флаг `_suppressRubricAutoLoad` (подавляет автоподгрузку показателей при смене рубрики — показатели запрашиваются явно), константы `HistoryStaleDays = 30` и `HistoryLoadedMessage`.
  * После восстановления StatusMessage = «Данные загружены из локальной БД. Проверьте актуальность.», а для записи старше 30 дней выводится предупреждение «Запись истории от дд.ММ.гггг старше 30 дней… выполните запрос повторно» (NotificationService.ShowWarning).
  * XAML (MainWindow.xaml ~1437–1509): `ListBox x:Name="BelstatHistoryListBox"` (+ `MouseDoubleClick="BelstatHistoryListBox_MouseDoubleClick"`, ContextMenu «Загрузить из истории» / «Обновить историю» и заголовок «Запись от …»), кнопка «Загрузить из истории» (`IconLoadTemplate`, SecondaryButtonBrush, CommandParameter = SelectedItem списка) и «Очистить историю» (`IconClearTemplate`, DangerSoftBrush). Автозапрос значений НЕ выполняется: подставляются показатель, рубрика и период, значения загружаются кнопкой «Получить данные».
- 6) Нумерация дерева рубрик: `BelstatApiService.FlattenRubrics` / `AppendRubric` при разворачивании дерева заполняют `Number` («1», «1.2», «1.2.3»), `Depth` и `ParentCode`; `BelstatRubric.DisplayName` (Models/BelstatModels.cs ~37) собирает отступ `Depth * 3` пробела + «номер. » + название, поэтому ComboBox рубрик показывает иерархию с номерами (и `DisplayMemberPath="DisplayName"` для рубрик, показателей и годов).
- Проверка: `dotnet build NalogParserWPF\NalogParserWPF.csproj -nologo` — сборка успешна, 0 ошибок, 0 предупреждений (net8.0-windows). Дополнительно использован служебный скрипт `check_resources.ps1` (сверка `x:Key` ↔ `StaticResource` по всем XAML): 112 ключей / 243 использования, отсутствующих ресурсов нет.

## ШАГ 11 — ДЕТАЛИ РЕАЛИЗАЦИИ (поиск показателей во вкладке «📊 Госстатистика»)
- Что улучшено: поиск показателя Белстата стал «живым» (результаты появляются по мере ввода), добавлен нечёткий поиск по FuzzySharp, список рубрик сужается по той же строке поиска, появилась кнопка «Очистить» и осмысленные сообщения для краевых случаев. Экспорт (PDF/Excel/CSV), загрузка значений, построение таблицы, история запросов и работа с API не изменялись.
- Изменённые файлы: `ViewModels/BelstatViewModel.cs` (вся логика поиска) и `MainWindow.xaml` (разметка карточки каталога вкладки: привязка рубрик, поле поиска, кнопка «Очистить», список результатов). Файл `NalogParserWPF.csproj` — добавлен пакет `FuzzySharp` 2.0.2 (netstandard2.0, совместим с net8.0).
- Настройки поиска — константы в `BelstatViewModel`: `LiveSearchLimit = 20` (сколько результатов «живого» поиска показывается), `FuzzyTopCount = 20` (сколько возвращает FuzzySharp), `FuzzyThreshold = 70` (минимальный порог схожести, %), `FuzzyMinQueryLength = 2` (с какой длины запроса включается нечёткий поиск).
- Сообщения (константы): `NoExactMatchMessage` = «Точных совпадений нет. Показаны похожие результаты.», `NothingFoundMessage` = «Ничего не найдено. Попробуйте изменить запрос.», `SearchClearedMessage` = «Поиск очищен. Показаны показатели выбранной рубрики.»
- «Живой» фильтр: `FilteredIndicatorsView` (тип `ICollectionView`) создан в конструкторе через `CollectionViewSource { Source = Indicators }` с обработчиком `Filter` → `FilterIndicatorsView(item)` (принимает только показатели из `_liveSearchMatches`). Аналогично `FilteredRubricsView` для `Rubrics`. Сеттер `SearchQuery` (свойство привязано к TextBox с `UpdateSourceTrigger=PropertyChanged`) вызывает `RefreshSearchResults()` + `UpdateLiveSearchStatus()`, то есть фильтрация идёт без задержки и без обращения к сети.
- `RefreshSearchResults()` — единая точка пересчёта: очищает `_liveSearchMatches`, точным фильтром (`MatchesQuery` — вхождение запроса в `Name` или `Code`, `OrdinalIgnoreCase`) набирает до `LiveSearchLimit` совпадений из текущего списка `Indicators`; если совпадений нет и длина запроса ≥ `FuzzyMinQueryLength`, добирает похожие показатели нечётким поиском и взводит `_lastSearchWasFuzzy`; затем обновляет оба представления и уведомляет `LiveSearchResultCount` / `HasSearchResults`. Коллекции перебираются через `foreach` (не через LINQ к `ICollectionView`), чтобы не получить повторный `Refresh` изнутри цикла.
- Нечёткий поиск `FuzzySearchIndicators(query)`: пул кандидатов — накопленный каталог `_indicatorPool` (`Dictionary<string, BelstatIndicator>` по коду, пополняется в `RememberIndicators` при каждой загрузке показателей рубрики/поиске), поэтому поиск работает и по тем рубрикам, которые пользователь уже просматривал. Вызов — `FuzzySharp.Process.ExtractTop(needle, names, processor: s => s.ToLowerInvariant(), limit: FuzzyTopCount, cutoff: FuzzyThreshold)`, результат `ExtractedResult<T>` → `.Index` → показатель из пула (FuzzySharp сортирует по убыванию схожести). Пул копируется в локальный список `pool` перед вызовом, чтобы индексы результата совпадали с элементами списка.
- КЛЮЧЕВАЯ ГРАБЛЯ FuzzySharp и кириллицы: штатный предпроцессор (`DefaultStringProcessor`) оставляет только латиницу и цифры, то есть русские названия после нормализации превращаются в мусор и схожесть считается неверно. Поэтому в `ExtractTop` передаётся собственная нормализация `s => s.ToLowerInvariant()`; регистр запроса приводится к нижнему в `FuzzySearchIndicators`.
- Проверка нечёткого поиска на кириллице (отдельный временный консольный проект net8.0 + FuzzySharp 2.0.2, те же параметры `limit=5`, `cutoff=70`, после проверки проект удалён): «заработная плта» → 84 % («Номинальная начисленная средняя заработная плата»), «безработитца» → 78 % («Уровень безработицы»), «индекс потреб» → 90 % («Индекс потребительских цен на товары и услуги»). Запросы «инфляция» и «ВВП» локально совпадений не дают — это ожидаемо: таких слов в названиях показателей нет (аббревиатуры и терминологические синонимы обслуживает серверный `indicatorSearch`), а «Индекс потребительских цен» не равен буквально «инфляции».
- Краевые случаи:
  * пустой запрос — фильтры снимаются (в дереве видны все рубрики, показатели рубрики видны все), статус не меняется;
  * запрос короче `FuzzyMinQueryLength` — работает только точный фильтр, нечёткий поиск не запускается;
  * числовой запрос (`IsNumericQuery`) — в серверном поиске `SearchIndicatorsAsync` результаты дополнительно фильтруются по вхождению цифр в код показателя;
  * нет точных совпадений — подставляются похожие (статус `NoExactMatchMessage`); нет и похожих — `NothingFoundMessage`;
  * выбор пункта из списка результатов — `SelectedSearchResult` в сеттере применяет результат через `ApplySearchResult` и сразу сбрасывает выделение (`_selectedSearchResult = null` + `OnPropertyChanged`), иначе повторный клик по той же строке не срабатывал бы (WPF не меняет значение уже выбранного элемента).
- `ApplySearchResult(indicator)`: находит рубрику через `FindRubricForIndicator` (по `RubricCode`, при его отсутствии — по коду из накопленного пула) → `FindRubricByCode` (рекурсивный поиск по дереву). Если рубрика найдена и ещё не выбрана — она выбирается с подавлением автоподгрузки (`_suppressRubricAutoLoad`), а показатели рубрики загружаются явно (`LoadRubricIndicatorsAndSelectAsync` → `LoadIndicatorsForRubricAsync`) и только после этого выделяется показатель: если загружать показатели «в фоне» одновременно с выбором, асинхронная загрузка очищала бы `Indicators` и сбрасывала выбор. Рубрика запоминается в `_pendingSelectRubric`, чтобы фильтр дерева её не выбросил при программном выборе. Если рубрика уже выбрана или не найдена — показатель выделяется сразу. Само выделение — `SelectIndicator`: показатель ищется в списке по коду (загруженный из API экземпляр не тот же объект, что найден поиском) и добавляется, только если его нет, чтобы в списке не появилось дубликатов.
- `FilterRubricsView` + `SubtreeMatches`: дерево фильтруется рекурсивно — рубрика остаётся, если запрос встречается в её названии либо в названии любого потомка (родительские разделы видны как контекст); дополнительно всегда оставляются `_selectedRubric` и `_pendingSelectRubric`, иначе при вводе запроса ComboBox терял бы выбор и сбрасывал показатели.
- Кнопка «Очистить» (`ClearSearchCommand`, `CanExecute` = `!IsLoading`; в `RaiseCommandStates` вызывается `ClearSearchCommand.RaiseCanExecuteChanged()`): `ClearSearch()` меняет `_searchQuery` напрямую (без сеттера, чтобы не пересчитывать фильтры дважды), сбрасывает `SelectedSearchResult`, вызывает единый `RefreshSearchResults()` и, если список показателей пришёл из поиска по каталогу (`_indicatorsFromSearch`), возвращает показатели выбранной рубрики через `LoadIndicatorsForRubricAsync`, иначе пишет `SearchClearedMessage`.
- Разметка (`MainWindow.xaml`, карточка «Каталог государственной статистики», строки ~1163–1270): ComboBox рубрики привязан к `{Binding FilteredRubricsView}` (был `Rubrics`) при `SelectedItem="{Binding SelectedRubric}"` — поиск сужает список рубрик, не ломая выбор; после поля поиска добавлена кнопка «Очистить» (`IconClearTemplate`, `DangerSoftBrush`, MinWidth=110, Command=`ClearSearchCommand`); ниже — `ListBox ItemsSource="{Binding FilteredIndicatorsView}"`, `SelectedItem="{Binding SelectedSearchResult}"`, `MaxHeight=180`, `Visibility="{Binding HasSearchResults, Converter={StaticResource BoolToVis}}"`, в шаблоне пункта — название (`Name`) и код (`Code`) показателя. Все кисти и конвертеры — существующие ключи темы (`CardBackgroundBrush`, `AccentBrush`, `TextPrimaryBrush`, `TextSecondaryBrush`, `BoolToVis`), новых ресурсов не потребовалось.
- Ограничение: нечёткий поиск работает по локально накопленному каталогу показателей (`_indicatorPool`) — это данные рубрик, которые пользователь уже открывал (или результаты предыдущих поисков). Аббревиатуры («ВВП») и терминологические синонимы («инфляция» вместо «Индекс потребительских цен») локально не находятся: для них нужно нажимать «Найти» — серверный поиск Белстата подбирает показатели по своей базе.
- Проверка: `dotnet build NalogParserWPF.sln -v:m --nologo` — сборка успешна, 0 ошибок, 0 предупреждений (net8.0-windows). Сверка ресурсов XAML (`check_resources.ps1`): 112 ключей / 243 использования, отсутствующих ресурсов нет.

## ШАГ 12 — ДЕТАЛИ РЕАЛИЗАЦИИ (исправление циклической зависимости XAML)

Симптом: при запуске приложение не показывало главное окно, а сразу выводило диалог «Критическая ошибка» с текстом «Не удается вызвать MarkupExtension.ProvideValue из-за циклической зависимости. Свойства внутри MarkupExtension не могут ссылаться на объекты, ссылающиеся на результат MarkupExtension.»; в сообщении указывались строки `MainWindow.xaml` 625, 626, 628, 629, 632, 633, 1485, 1488, 1489, 1491. Процесс оставался жить (окно ошибки) — `App.OnDispatcherUnhandledException` показывает MessageBox и ставит `e.Handled = true`, а `MainWindow.Show()` до конца не доходил.

Причина: в обоих контекстных меню команды и параметры брались через `Source={x:Reference ИмяВладельца}` — ссылка на элемент внутри свойства самого этого элемента (`DataGrid.ContextMenu` у `HistoryGrid`, `ListBox.ContextMenu` у `BelstatHistoryListBox`). `x:Reference` разрешается на этапе загрузки разметки, и владелец контекстного меню ещё не построен → цикл. Дополнительно ContextMenu отображается в отдельном дереве (Popup), поэтому привязка к визуальным предкам владельца для него в принципе не работает.

Исправление (`MainWindow.xaml`, контекстные меню `HistoryGrid` и `BelstatHistoryListBox`):
- Команды: `{Binding Path=PlacementTarget.DataContext.<Команда>, RelativeSource={RelativeSource AncestorType=ContextMenu}}` — DataContext берётся у элемента, для которого меню открыто (`PlacementTarget`).
- Параметр: `{Binding Path=PlacementTarget.SelectedItem, RelativeSource={RelativeSource AncestorType=ContextMenu}}` — выделенная строка владельца.
- Заголовок «Запись от …» в меню истории Госстатистики: `Path=PlacementTarget.SelectedItem.Timestamp` вместо `Source={x:Reference BelstatHistoryListBox}`.
- Никаких других `x:Reference` в проекте нет (проверено `Select-String` по всем `*.xaml`), остальные привязки проекта используют безопасные формы: `RelativeSource AncestorType=DataGrid` (кнопки строк истории УНП, MainWindow.xaml ~713–725), `ElementName` внутри одного шаблона (`BatchSearchView.xaml`), `{TemplateBinding}`/`RelativeSource TemplatedParent` (App.xaml).

Дополнительно (`ViewModels/MainViewModel.cs`, ~60–64): у `ViewHistoryCommand`, `RepeatSearchCommand` и `DeleteHistoryItemCommand` добавлен `CanExecute` с проверкой параметра (`item => item != null`). До этого у команд проверки не было, а `ViewHistory(null)` и `RepeatSearchAsync(null)` бросают `ArgumentNullException`: правый клик по пустой таблице истории + пункт меню давали диалог «Критическая ошибка». Кнопки в строках таблицы передают реальную запись, поэтому их поведение не изменилось; в меню пункты просто неактивны, когда ничего не выделено.

Проверка (UIA-автоматизация + сборка):
- `dotnet build NalogParserWPF.sln -v:m --nologo` — успешно, 0 ошибок, 0 предупреждений.
- Запуск `bin\Debug\net8.0-windows\NalogParserWPF.exe` (рабочий каталог — папка сборки, иначе `appsettings.json` не найден): главное окно «ОПЦИОН» открывается, окон/диалогов ошибки — одно (само окно приложения), вкладки, карточка «Каталог государственной статистики» и список истории отрисовываются.
- Правый клик по таблице истории УНП: меню открывается, пункты «Просмотр записи», «Повторить поиск», «Удалить запись» присутствуют и при пустой таблице (нет выделения) неактивны.
- Правый клик по списку «История запросов статистики»: пункты «Загрузить из истории» (нет выделения → неактивен, CanExecute `parameter is BelstatHistoryItem`) и «Обновить историю» (активен → `PlacementTarget.DataContext` действительно разрешается; вызов пункта выполняется без ошибок и диалогов).

## ШАГ 13 — ДЕТАЛИ РЕАЛИЗАЦИИ (UX вкладки «📊 Госстатистика»: разделитель высоты и отдельное окно таблицы результатов)

Пять улучшений вкладки «📊 Госстатистика». Логика работы с API Белстата, каталог, поиск показателей, история запросов и сами экспортёры не изменялись.

1) Разделитель высоты «каталог ↔ таблица результатов» (`MainWindow.xaml`, левая колонка вкладки).
- Строки Grid перестроены: `Auto` (каталог, `MinHeight=96`) → `6` (разделитель) → `Auto` (период и действия) → `Auto` (индикатор загрузки и статус) → `Auto` (заголовок результата) → `*` (таблица результатов, `MinHeight=220`). Раньше высота каталога была фиксированной, поэтому таблице результата не хватало места.
- `GridSplitter`: `Grid.Row="1"`, `HorizontalAlignment`/`VerticalAlignment=Stretch`, `ResizeDirection=Rows`, `ResizeBehavior=PreviousAndNext`, `ShowsPreview=False`, `Cursor=SizeNS`, `Background="{DynamicResource BorderBrush}"` + ToolTip. Строка каталога задана `Height="Auto"` — при первом перетаскивании разделитель переводит её в пиксельную высоту и забирает/отдаёт высоту у строки таблицы (`*`).
- `MinHeight` строк (96 у каталога, 220 у таблицы) не дают раздавить ни одну из областей: при упоре разделитель дальше не двигается.
- Карточка каталога обёрнута в `ScrollViewer` (`VerticalScrollBarVisibility=Auto`, `HorizontalScrollBarVisibility=Disabled`, `Padding=0,0,4,0`): если разделителем каталог сжат сильнее, чем его содержимое, поля и списки каталога остаются доступными через полосу прокрутки.
- ВНИМАНИЕ при дальнейших правках разметки: из-за новой строки разделителя индексы строк ниже сдвинуты — период и действия `Grid.Row="2"`, индикатор загрузки и статус `Grid.Row="3"`, заголовок результата `Grid.Row="4"`, таблица результатов `Grid.Row="5"`.

2) Кнопка «Открыть в окне» (ряд кнопок экспорта вкладки) + `BelstatViewModel.OpenInNewWindowCommand`.
- Команда: `new RelayCommand(_ => OpenInNewWindow(), _ => IsResultVisible)`; в `RaiseCommandStates()` вызывается `OpenInNewWindowCommand.RaiseCanExecuteChanged()`, поэтому кнопка активна только при наличии строк результата.
- `OpenInNewWindow()`: пустая таблица → предупреждение `ResultWindowEmptyMessage`; окно берётся из DI (`App.ServiceProvider?.GetRequiredService<Views.BelstatDataWindow>()`; контейнер недоступен → ошибка `ResultWindowServiceProviderMessage`), показывается `ShowDialog()` с владельцем `Application.Current.MainWindow`; исключения логируются и показываются через `_notifications.ShowError`. Обе строки-сообщения — новые константы ViewModel.

3) Новое окно `Views/BelstatDataWindow.xaml(.cs)`.
- Размер 1200×800 (`MinWidth=800`, `MinHeight=500`), `WindowStartupLocation="CenterOwner"`, `WindowStyle="SingleBorderWindow"`, фон и текст из темы (`WindowBackgroundBrush`/`TextPrimaryBrush`) — тёмная тема работает без отдельных правок.
- Шапка как в `AboutWindow`: логотип `/sk-logo-2.png` (48×48) + «Следственный комитет Республики Беларусь» / «Информация из официальных источников» + жёлтая линия `SecondaryBrush`; ниже — заголовок результата (`ResultTitle`), подзаголовок (`ResultSubtitle`) и число строк (`ResultRowCount`, StringFormat «Строк в таблице: {0}»).
- Таблица: `DataGrid ItemsSource="{Binding ResultTable.DefaultView}"`, `AutoGenerateColumns=True`, `IsReadOnly=True`, `CanUserAddRows/DeleteRows=False`, `SelectionUnit=Cell`, обе полосы прокрутки `Auto`, растянут по обеим осям; при пустом результате вместо таблицы показывается плашка `EmptyResultMessage` (конвертеры `BoolToVis` и `InvertedBoolToVisibilityConverter` объявлены в `Window.Resources`).
- Колонки строятся тем же приёмом, что на вкладке: `BelstatDataGrid_AutoGeneratingColumn` переводит путь привязки в индексную форму `Binding("[имя колонки]")`, потому что имена измерений Белстата содержат точки и пробелы. Двойной клик по ячейке копирует её значение (`DataGridHelper.CopyCellContent`).
- Экспорт: кнопки PDF/Excel/CSV привязаны к тем же командам `ExportPdfCommand`/`ExportExcelCommand`/`ExportCsvCommand` (CanExecute `HasExportData`) — окну передаётся тот же экземпляр `BelstatViewModel`, поэтому экспортируется ровно тот результат, что показан; закрытие — кнопка «Закрыть» (`CloseButton_Click`) и Esc (`Window_KeyDown`).
- Стили окна — наследники `AppButtonBase` (`WindowButton` → `ExportButton` с `ExportButtonBrush`, `DataCloseButton` с `DangerSoftBrush` + `IconCloseTemplate`); новых ресурсов в App.xaml не добавлялось.
- Регистрация в DI: `services.AddTransient<Views.BelstatDataWindow>();` (`App.xaml.cs`).

4) Область результатов на вкладке по умолчанию крупнее: Grid таблицы результатов растянут (`HorizontalAlignment`/`VerticalAlignment=Stretch`), у DataGrid включены полосы прокрутки (`Auto` по обеим осям).

5) Кнопка «Отмена» (`CancelCommand`): проверка `CanExecute` в коде уже была — `new RelayCommand(_ => CancelLoading(), _ => IsLoading)`, а сеттер свойства `IsLoading` вызывает `RaiseCommandStates()`, поэтому кнопка активна ровно во время загрузки; дополнительных правок не потребовалось.

Проверка (ШАГ 13):
- `dotnet build` (NalogParserWPF.csproj, net8.0-windows) — успешно, 0 ошибок, 0 предупреждений.
- Запуск `bin\Debug\net8.0-windows\NalogParserWPF.exe` (рабочий каталог — папка сборки): главное окно «ОПЦИОН» открывается, процесс отвечает на сообщения, диалогов ошибки нет.
- Окно `BelstatDataWindow` проверено временным диагностическим хуком в `App.Startup` (по переменной среды `SMOKE_BELSTAT_WINDOW=1` окно бралось из DI и показывалось, результат писался в `%TEMP%\belstat_smoke.txt`; после проверки хук удалён из кода, финальная сборка — без него): окно создаётся и отображается без `XamlParseException`, то есть все ключи `StaticResource`/`DynamicResource` (включая `AppButtonBase`, `IconExport*Template`, `IconCloseTemplate`) и pack-URI логотипа `/sk-logo-2.png` разрешаются; заголовок окна — «Госстатистика — таблица результатов».
- Ограничение автоматической проверки: содержимое таблицы с реальными данными Белстата требует сети и выбора показателя, поэтому оно не прогонялось; разметка окна повторяет уже работающую разметку таблицы вкладки (та же привязка `ResultTable.DefaultView` и тот же обработчик автогенерации колонок).

