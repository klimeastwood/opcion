using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace NalogParserWPF.Helpers
{
    /// <summary>
    /// Подключает фильтрацию строк таблицы по тексту, введённому в отдельном поле.
    /// <para>
    /// Таблицы Белстата построены на <see cref="DataView"/>, поэтому текст переносится
    /// в <see cref="DataView.RowFilter"/> (фильтрация выполняется внутри DataView, интерфейс
    /// не перебирает строки). Для обычных коллекций фильтр ставится на представление по умолчанию
    /// (<see cref="CollectionViewSource.GetDefaultView"/>), то есть на то же представление,
    /// которое показывает <see cref="DataGrid"/>, — сортировка при этом сохраняется.
    /// </para>
    /// <para>
    /// Ввод защищён от «мигания»: текст принимается не на каждый символ, а после паузы
    /// (<see cref="DebounceMilliseconds"/> мс), предыдущий расчёт отменяется через
    /// <see cref="CancellationTokenSource"/>. Для таблиц в 50 000+ строк это заменяет
    /// пересчёт фильтра на каждый символ одним пересчётом после окончания ввода.
    /// </para>
    /// </summary>
    public static class DataGridFilterHelper
    {
        /// <summary>
        /// Задержка перед применением фильтра. Пока пользователь печатает, фильтр не пересчитывается:
        /// ввод символов больше не блокирует интерфейс на таблицах в десятки тысяч строк.
        /// </summary>
        private const int DebounceMilliseconds = 300;

        /// <summary>Метка динамически добавленного индикатора фильтрации (защита от повторного добавления).</summary>
        private const string IndicatorTag = "NalogParserWPF.FilterProgress";

        /// <summary>Метка «фильтр к этой таблице уже подключён» — защита от повторной подписки.</summary>
        private static readonly DependencyProperty AttachedProperty =
            DependencyProperty.RegisterAttached(
                "Attached",
                typeof(bool),
                typeof(DataGridFilterHelper),
                new PropertyMetadata(false));

        /// <summary>Последний применённый текст фильтра (защита от рекурсии Refresh → CollectionChanged).</summary>
        private static readonly DependencyProperty LastTextProperty =
            DependencyProperty.RegisterAttached(
                "LastText",
                typeof(string),
                typeof(DataGridFilterHelper),
                new PropertyMetadata(null));

        /// <summary>Источник данных, к которому применён последний фильтр.</summary>
        private static readonly DependencyProperty LastSourceProperty =
            DependencyProperty.RegisterAttached(
                "LastSource",
                typeof(object),
                typeof(DataGridFilterHelper),
                new PropertyMetadata(null));

        /// <summary>Состояние фильтрации конкретной таблицы: таймер задержки, отмена и индикатор.</summary>
        private static readonly DependencyProperty StateProperty =
            DependencyProperty.RegisterAttached(
                "State",
                typeof(FilterState),
                typeof(DataGridFilterHelper),
                new PropertyMetadata(null));

        /// <summary>Кэш читаемых свойств модели — поиск по строкам не должен каждый раз читать метаданные.</summary>
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

        /// <summary>
        /// Текст последнего применённого фильтра по каждому источнику. Нужен экспорту: после фильтрации
        /// PDF/Excel/CSV выгружают только видимые строки и помечают документ запросом, по которому отобраны данные.
        /// Ключи слабые (<see cref="ConditionalWeakTable{TKey,TValue}"/>), поэтому источники не удерживаются в памяти.
        /// </summary>
        private static readonly ConditionalWeakTable<object, string> AppliedFilterTexts = new();

        /// <summary>Состояние фильтрации одной таблицы (один экземпляр на <see cref="DataGrid"/>).</summary>
        private sealed class FilterState
        {
            /// <summary>Таймер задержки ввода: перезапускается на каждый символ.</summary>
            public DispatcherTimer? DebounceTimer { get; set; }

            /// <summary>Отмена предыдущего расчёта фильтра (если пользователь печатает быстро).</summary>
            public CancellationTokenSource? Cancellation { get; set; }

            /// <summary>Индикатор «Фильтрация...» рядом с полем ввода.</summary>
            public ProgressBar? Indicator { get; set; }
        }

        /// <summary>
        /// Подключает фильтрацию к таблице по тексту поля <paramref name="filterBox"/>.
        /// Вызывается один раз при создании окна.
        /// </summary>
        /// <param name="grid">Таблица, строки которой фильтруются.</param>
        /// <param name="filterBox">Поле ввода строки поиска над таблицей.</param>
        public static void AttachFilter(DataGrid? grid, TextBox? filterBox)
        {
            if (grid == null || filterBox == null) return;
            if ((bool)grid.GetValue(AttachedProperty)) return;
            grid.SetValue(AttachedProperty, true);

            var state = GetOrCreateState(grid);
            state.Indicator = CreateIndicator(filterBox);

            // Ввод символа лишь перезапускает таймер задержки; фильтр применяется после паузы.
            filterBox.TextChanged += (_, _) => OnFilterTextChanged(grid, filterBox);

            // Данные приходят позже (запрос к API, новая таблица Белстата), поэтому фильтр
            // переприменяется при загрузке таблицы, при её показе (вкладка «Госстатистика»
            // открывается лениво) и при любом изменении набора строк — без задержки.
            grid.Loaded += (_, _) => ApplyFilterNow(grid, filterBox.Text);
            grid.IsVisibleChanged += (_, _) => ApplyFilterNow(grid, filterBox.Text);
            if (grid.Items is INotifyCollectionChanged items)
                items.CollectionChanged += (_, _) => ApplyFilterNow(grid, filterBox.Text);
        }

        /// <summary>
        /// Возвращает текст фильтра, который сейчас применён к источнику данных таблицы
        /// (например, к <see cref="DataView"/> таблицы Белстата). Пустая строка — источник не отфильтрован.
        /// </summary>
        public static string GetAppliedFilterText(object? source)
        {
            if (source == null) return string.Empty;
            return AppliedFilterTexts.TryGetValue(source, out var text) ? text : string.Empty;
        }

        /// <summary>Отфильтрован ли источник данных (у <see cref="DataView"/> задан непустой <see cref="DataView.RowFilter"/>).</summary>
        public static bool IsFiltered(object? source)
        {
            if (source is DataTable table) source = table.DefaultView;
            return source is DataView view && !string.IsNullOrEmpty(view.RowFilter);
        }

        /// <summary>
        /// Обработчик ввода в поле фильтра: перезапускает таймер задержки. Фильтр применяется
        /// только когда пользователь перестал печатать (таймер сработал).
        /// </summary>
        private static void OnFilterTextChanged(DataGrid grid, TextBox filterBox)
        {
            var state = GetOrCreateState(grid);

            // Если таймер уже идёт — сбрасываем и начинаем заново.
            state.DebounceTimer?.Stop();

            var timer = new DispatcherTimer(DispatcherPriority.Background, grid.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(DebounceMilliseconds)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                _ = ApplyFilterAsync(grid, filterBox);
            };

            state.DebounceTimer = timer;
            timer.Start();
        }

        /// <summary>
        /// Применяет фильтр после паузы ввода. Предыдущий расчёт отменяется — если пользователь
        /// печатает быстро, старый фильтр не доводится до конца, начинается новый.
        /// Тяжёлая часть (построение выражения) считается в фоне, применение к <see cref="DataView"/>
        /// выполняется в потоке интерфейса: DataView связан с таблицей, менять его «из фона» нельзя.
        /// </summary>
        private static async Task ApplyFilterAsync(DataGrid grid, TextBox filterBox)
        {
            var state = GetOrCreateState(grid);
            var text = (filterBox.Text ?? string.Empty).Trim();

            // Отменяем предыдущий расчёт.
            state.Cancellation?.Cancel();
            var cancellation = new CancellationTokenSource();
            state.Cancellation = cancellation;
            var token = cancellation.Token;

            // Источник читаем в потоке интерфейса: обращение к DataGrid из фона недопустимо.
            var source = grid.ItemsSource;

            SetIndicator(state, true);
            try
            {
                string? filter = await Task.Run(() => BuildFilterExpression(source, text), token);
                token.ThrowIfCancellationRequested();

                // Даём разметке отрисоваться, чтобы индикатор «Фильтрация...» был виден
                // до тяжёлого пересчёта строк, и проверяем отмену ещё раз.
                await grid.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                token.ThrowIfCancellationRequested();

                ApplyFilterInternal(grid, text, filter, token);
            }
            catch (OperationCanceledException)
            {
                // Пришёл новый ввод — этот расчёт больше не нужен.
            }
            catch (Exception exception)
            {
                // Ошибка выражения фильтра не должна ронять окно: показываем таблицу без фильтра.
                // Запись о применённом фильтре снимается, иначе повторная попытка с тем же текстом
                // считалась бы уже выполненной и фильтр больше не применялся бы.
                System.Diagnostics.Debug.WriteLine("Фильтр таблицы не применён: " + exception.Message);
                ClearFilter(grid);
                grid.SetValue(LastSourceProperty, null);
                grid.SetValue(LastTextProperty, null);
            }
            finally
            {
                if (ReferenceEquals(state.Cancellation, cancellation))
                {
                    SetIndicator(state, false);
                    state.Cancellation = null;
                    cancellation.Dispose();
                }
            }
        }

        /// <summary>
        /// Применяет фильтр немедленно (загрузка таблицы, показ вкладки, изменение набора строк).
        /// Задержка здесь не нужна: это не ввод текста, а поступление данных.
        /// </summary>
        private static void ApplyFilterNow(DataGrid grid, string? text)
        {
            var state = GetOrCreateState(grid);
            state.DebounceTimer?.Stop();

            var trimmed = (text ?? string.Empty).Trim();
            var source = grid.ItemsSource;
            if (source == null)
            {
                // Источник ещё не задан: фильтр применится после загрузки данных.
                grid.SetValue(LastSourceProperty, null);
                grid.SetValue(LastTextProperty, null);
                return;
            }

            var token = state.Cancellation?.Token ?? CancellationToken.None;
            ApplyFilterInternal(grid, trimmed, BuildFilterExpression(source, trimmed), token);
        }

        /// <summary>Переносит текст фильтра в источник данных таблицы.</summary>
        private static void ApplyFilterInternal(DataGrid grid, string text, string? filter, CancellationToken token)
        {
            var source = grid.ItemsSource;
            if (source == null)
            {
                grid.SetValue(LastSourceProperty, null);
                grid.SetValue(LastTextProperty, null);
                return;
            }

            token.ThrowIfCancellationRequested();

            // Повтор того же текста к тому же источнику не пересчитывается.
            if (ReferenceEquals(grid.GetValue(LastSourceProperty), source)
                && string.Equals((string?)grid.GetValue(LastTextProperty), text, StringComparison.Ordinal))
            {
                return;
            }

            grid.SetValue(LastTextProperty, text);
            grid.SetValue(LastSourceProperty, source);

            var resolved = source is DataTable table ? table.DefaultView : source;

            if (resolved is DataView dataView)
            {
                string rowFilter = filter ?? string.Empty;
                if (!string.Equals(dataView.RowFilter, rowFilter, StringComparison.Ordinal))
                    dataView.RowFilter = rowFilter;

                RecordFilterText(dataView, text);
                return;
            }

            var view = CollectionViewSource.GetDefaultView(resolved);
            if (view == null) return;

            view.Filter = text.Length == 0 ? null : item => Matches(item, text);
            RecordFilterText(resolved, text);
        }

        /// <summary>Снимает фильтр с источника таблицы (аварийный путь при ошибке выражения).</summary>
        private static void ClearFilter(DataGrid grid)
        {
            var source = grid.ItemsSource;
            if (source is DataTable table) source = table.DefaultView;
            if (source is DataView view) view.RowFilter = string.Empty;

            // Фильтр снят — запись о нём тоже, иначе экспорт пометил бы документ запросом,
            // по которому строки на самом деле не отбирались.
            if (source != null) AppliedFilterTexts.Remove(source);
        }

        /// <summary>Запоминает применённый текст фильтра по источнику (нужен экспорту «только видимых строк»).</summary>
        private static void RecordFilterText(object source, string text)
        {
            AppliedFilterTexts.Remove(source);
            if (text.Length > 0)
                AppliedFilterTexts.Add(source, text);
        }

        /// <summary>
        /// Строит выражение фильтра для источника. Для <see cref="DataTable"/>/<see cref="DataView"/>
        /// возвращает выражение <see cref="DataView.RowFilter"/>, для коллекций — <c>null</c>
        /// (там фильтр задаётся делегатом на представлении).
        /// </summary>
        private static string? BuildFilterExpression(object? source, string text)
        {
            if (source is DataTable table) source = table.DefaultView;
            return source is DataView dataView ? BuildRowFilter(dataView, text) : null;
        }

        /// <summary>Строит выражение <see cref="DataView.RowFilter"/> для поиска текста по всем колонкам.</summary>
        private static string BuildRowFilter(DataView dataView, string text)
        {
            var table = dataView.Table;
            if (text.Length == 0 || table == null) return string.Empty;

            var conditions = new List<string>(table.Columns.Count);
            string value = EscapeLikeValue(text);

            foreach (DataColumn column in table.Columns)
            {
                // Колонку с «]» в имени в выражении DataView назвать нельзя: экранирование «]]»
                // парсер выражений не понимает (SyntaxErrorException), и весь фильтр перестал бы
                // работать. Такую колонку пропускаем — поиск идёт по остальным колонкам.
                if (column.ColumnName.Contains(']')) continue;

                // Имя колонки берётся в квадратные скобки, потому что колонки Белстата содержат
                // пробелы и точки («г. Минск»); «[» внутри таких скобок читается как обычный символ.
                string reference = "[" + column.ColumnName + "]";

                // Для строковых колонок Convert не нужен — LIKE работает со строкой напрямую,
                // а на десятках тысяч строк каждая лишняя функция в выражении заметно тормозит.
                // Convert к строке обязателен только для числовых и датных колонок: иначе LIKE
                // к ним не применяется (таблицы Белстата содержат и числа, и даты).
                conditions.Add(column.DataType == typeof(string)
                    ? reference + " LIKE '" + value + "'"
                    : "Convert(" + reference + ", 'System.String') LIKE '" + value + "'");
            }

            // Все колонки оказались неподходящими (все имена содержат «]») — фильтровать нечего.
            if (conditions.Count == 0) return string.Empty;

            return string.Join(" OR ", conditions);
        }

        /// <summary>Экранирует текст поиска и добавляет подстановочные символы «в любом месте строки».</summary>
        private static string EscapeLikeValue(string text)
        {
            var builder = new StringBuilder(text.Length + 2);

            foreach (char symbol in text)
            {
                switch (symbol)
                {
                    case '\'': builder.Append("''"); break;          // конец строкового литерала
                    case '[': builder.Append("[[]"); break;          // диапазон символов LIKE
                    case ']': builder.Append("[]]"); break;
                    case '%': builder.Append("[%]"); break;          // «любые символы»
                    case '*': builder.Append("[*]"); break;
                    default: builder.Append(symbol); break;
                }
            }

            return "%" + builder + "%";
        }

        /// <summary>
        /// Проверяет, содержит ли строка коллекции искомый текст (по всем читаемым свойствам).
        /// Приведение к строке идёт по текущей культуре, поэтому даты и числа в таблице
        /// находятся по своему экранному виду.
        /// </summary>
        private static bool Matches(object? item, string text)
        {
            if (item == null) return false;
            if (item is string stringItem)
                return stringItem.Contains(text, StringComparison.CurrentCultureIgnoreCase);

            foreach (var property in GetProperties(item.GetType()))
            {
                object? value;
                try
                {
                    value = property.GetValue(item);
                }
                catch (Exception)
                {
                    continue;
                }

                if (value == null) continue;

                string? text2 = value as string ?? Convert.ToString(value, CultureInfo.CurrentCulture);
                if (!string.IsNullOrEmpty(text2)
                    && text2.Contains(text, StringComparison.CurrentCultureIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Читаемые свойства без индексаторов, найденные один раз для каждого типа модели.</summary>
        private static PropertyInfo[] GetProperties(Type type) =>
            PropertyCache.GetOrAdd(
                type,
                t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                      .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                      .ToArray());

        /// <summary>Состояние фильтрации таблицы (создаётся при первом обращении).</summary>
        private static FilterState GetOrCreateState(DataGrid grid)
        {
            if (grid.GetValue(StateProperty) is FilterState state) return state;

            state = new FilterState();
            grid.SetValue(StateProperty, state);
            return state;
        }

        /// <summary>
        /// Создаёт маленький индикатор фильтрации рядом с полем ввода (панель фильтра —
        /// <see cref="StackPanel"/>). Показать и скрыть его позволяет <see cref="SetIndicator"/>.
        /// </summary>
        private static ProgressBar? CreateIndicator(TextBox filterBox)
        {
            if (filterBox.Parent is not Panel panel) return null;

            foreach (var child in panel.Children)
            {
                if (child is ProgressBar existing && Equals(existing.Tag, IndicatorTag))
                    return existing;
            }

            var indicator = new ProgressBar
            {
                IsIndeterminate = true,
                Width = 90,
                Height = 14,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                ToolTip = "Фильтрация...",
                Tag = IndicatorTag
            };

            // Индикатор ставится сразу после поля ввода, чтобы не разрывать подсказку «Фильтр: …».
            var index = panel.Children.IndexOf(filterBox);
            if (index >= 0 && index + 1 <= panel.Children.Count)
                panel.Children.Insert(index + 1, indicator);
            else
                panel.Children.Add(indicator);

            return indicator;
        }

        /// <summary>Показывает или скрывает индикатор «Фильтрация...» рядом с полем ввода.</summary>
        private static void SetIndicator(FilterState state, bool isFiltering)
        {
            if (state.Indicator == null) return;
            state.Indicator.Visibility = isFiltering ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
