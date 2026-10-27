using System.Data;
using System.Text;

// Изолированная проверка выражений DataView.Sort / DataView.RowFilter, которые формируют
// DataGridSortHelper и DataGridFilterHelper для таблиц Белстата: имена колонок с точками
// и пробелами («g. Minsk Region»), числовые колонки под LIKE, экранирование служебных
// символов LIKE. Запускается на том же рантайме (net8.0), что и приложение.
// Запуск: dotnet run --project Docs/_probe/dvprobe

Console.OutputEncoding = Encoding.UTF8;
int fail = 0;

void Check(string name, bool ok, string info)
{
    if (ok) Console.WriteLine("PASS " + name);
    else { Console.WriteLine("FAIL " + name + " (" + info + ")"); fail++; }
}

// Повторяет EscapeLikeValue из DataGridFilterHelper.
string EscapeLike(string text)
{
    var builder = new StringBuilder(text.Length + 2);
    foreach (char symbol in text)
    {
        switch (symbol)
        {
            case '\'': builder.Append("''"); break;
            case '[': builder.Append("[[]"); break;
            case ']': builder.Append("[]]"); break;
            case '%': builder.Append("[%]"); break;
            case '*': builder.Append("[*]"); break;
            default: builder.Append(symbol); break;
        }
    }
    return "%" + builder + "%";
}

// Повторяет BuildRowFilter из DataGridFilterHelper (оптимизированный вариант:
// для строковых колонок — LIKE напрямую, Convert только для нестроковых).
string BuildRowFilter(DataTable table, string text, bool legacyConvertEverywhere = false)
{
    if (text.Length == 0 || table == null) return string.Empty;

    var conditions = new List<string>(table.Columns.Count);
    string value = EscapeLike(text);
    foreach (DataColumn column in table.Columns)
    {
        // Колонки с «]» в имени в выражении назвать нельзя (экранирование «]]» парсер не
        // понимает) — такие колонки пропускаются, остальные продолжают искать.
        if (column.ColumnName.Contains(']')) continue;

        string reference = "[" + column.ColumnName + "]";
        conditions.Add(!legacyConvertEverywhere && column.DataType == typeof(string)
            ? reference + " LIKE '" + value + "'"
            : "Convert(" + reference + ", 'System.String') LIKE '" + value + "'");
    }
    return string.Join(" OR ", conditions);
}

DataTable NewTable()
{
    var t = new DataTable();
    t.Columns.Add("Territory Name", typeof(string));
    t.Columns.Add("g. Minsk Region", typeof(string));
    t.Columns.Add("Period", typeof(int));
    t.Columns.Add("Value", typeof(double));
    t.Rows.Add("Minsk oblast", "Minsk", 2020, 10.5);
    t.Rows.Add("Gomel oblast", "Gomel", 2021, 20.0);
    t.Rows.Add("Minsk city", "Minsk", 2022, 30.25);
    t.Rows.Add("100% region", "Orsha", 2023, 5.0);
    return t;
}

// 1) Фильтр по всем колонкам (строка, содержащая точку и пробел, тоже участвует).
var table = NewTable();
var view = table.DefaultView;

view.RowFilter = BuildRowFilter(table, "Minsk");
Check("filter \"Minsk\" -> 2 rows", view.Count == 2, view.Count + " rows");

// 2) Convert(...,'System.String') должен находить значение в числовой колонке.
view.RowFilter = BuildRowFilter(table, "2021");
Check("filter \"2021\" -> 1 row", view.Count == 1, view.Count + " rows");

// 3) Служебный символ «%» экранируется и ищется как литерал.
view.RowFilter = BuildRowFilter(table, "%");
Check("filter \"%\" -> literal match",
    view.Count == 1 && (int)view[0]["Period"]! == 2023, view.Count + " rows");

// 4) Пустой текст снимает фильтр.
view.RowFilter = BuildRowFilter(table, "");
Check("empty filter -> 4 rows", view.Count == 4, view.Count + " rows");

// 5) Сортировка по колонке с точкой в имени (экранирование «[имя]»).
view.RowFilter = "";
view.Sort = "[g. Minsk Region] ASC";
Check("sort \"[g. Minsk Region] ASC\"",
    (string)view[0]["g. Minsk Region"]! == "Gomel", (string)view[0]["g. Minsk Region"]!);

// 6) Сортировка по числовой колонке по убыванию.
view.Sort = "[Value] DESC";
Check("sort \"[Value] DESC\"", (double)view[0]["Value"]! == 30.25, view[0]["Value"]!.ToString()!);

// 7) Сортировка по колонке с пробелами в имени.
view.Sort = "[Territory Name] ASC";
Check("sort bracketed space name", view.Count == 4, view.Count + " rows");

// 8) Фильтр и сортировка действуют одновременно.
view.RowFilter = BuildRowFilter(table, "Minsk");
view.Sort = "[Value] DESC";
Check("filter + sort together",
    view.Count == 2 && (double)view[0]["Value"]! == 30.25, view.Count + " rows");

// 9) Строковая колонка с «%» в значении ищется как литерал (экранирование «[%]»).
view.Sort = string.Empty;
view.RowFilter = BuildRowFilter(table, "100%");
Check("filter \"100%\" -> literal percent in string",
    view.Count == 1 && (string)view[0]["Territory Name"]! == "100% region", view.Count + " rows");

// 10) Имя колонки со служебным символом «]»: парсер выражений DataView не поддерживает
//     экранирование «]]», поэтому такие колонки в фильтр не включаются (иначе RowFilter
//     падал бы с SyntaxErrorException и фильтр вообще не работал).
var bracketTable = new DataTable();
bracketTable.Columns.Add("Value]", typeof(string));
bracketTable.Columns.Add("Open[Name", typeof(string));
bracketTable.Columns.Add("Region", typeof(string));
bracketTable.Rows.Add("Br]est", "M]]insk", "Brest");
bracketTable.Rows.Add("Grodno", "Grodno", "Grodno");
var bracketView = bracketTable.DefaultView;

// Диагностика: как парсер реагирует на попытку назвать колонку с «]» и с «[».
foreach ((string column, string reference) in new[]
    {
        ("Value]", "[Value]]"),
        ("Value]", "[Value]]]]"),
        ("Open[Name", "[Open[Name]"),
        ("Region", "[Region]")
    })
{
    var probe = new DataTable();
    probe.Columns.Add(column, typeof(string));
    probe.Rows.Add("Br]est");
    try
    {
        probe.DefaultView.RowFilter = reference + " LIKE '%x%'";
        Console.WriteLine($"  reference \"{reference}\" for \"{column}\": accepted");
    }
    catch (Exception exception)
    {
        Console.WriteLine($"  reference \"{reference}\" for \"{column}\": {exception.GetType().Name}");
    }
}

bracketView.RowFilter = BuildRowFilter(bracketTable, "Brest");
Check("column with ']' skipped, filter built on other columns",
    bracketView.Count == 1 && (string)bracketView[0]["Region"]! == "Brest", bracketView.Count + " rows");

bracketView.RowFilter = BuildRowFilter(bracketTable, "Grodno");
Check("column with '[' still searchable",
    bracketView.Count == 1 && (string)bracketView[0]["Region"]! == "Grodno", bracketView.Count + " rows");

// 11) Оптимизированное выражение (LIKE без Convert для строк) отбирает те же строки,
//     что и старое (Convert для всех колонок) — иначе оптимизация меняла бы результат.
var compareTable = new DataTable();
compareTable.Columns.Add("Territory Name", typeof(string));
compareTable.Columns.Add("g. Minsk Region", typeof(string));
compareTable.Columns.Add("Period", typeof(int));
compareTable.Columns.Add("Value", typeof(double));
compareTable.Columns.Add("Updated", typeof(DateTime));
var random = new Random(12345);
var regions = new[] { "Minsk", "Gomel", "Brest", "Grodno", "Vitebsk" };
for (int i = 0; i < 50000; i++)
{
    compareTable.Rows.Add(
        regions[random.Next(regions.Length)] + " oblast " + i,
        regions[random.Next(regions.Length)],
        2015 + random.Next(10),
        Math.Round(random.NextDouble() * 1000, 2),
        new DateTime(2015 + random.Next(10), 1 + random.Next(12), 1 + random.Next(28)));
}
var compareView = compareTable.DefaultView;

int sameCount = 0;
foreach (string query in new[] { "Minsk", "2021", "0.5", "Brest", "2018-03", "oblask", "20" })
{
    compareView.RowFilter = BuildRowFilter(compareTable, query);
    int optimized = compareView.Count;
    compareView.RowFilter = BuildRowFilter(compareTable, query, legacyConvertEverywhere: true);
    int legacy = compareView.Count;
    if (optimized == legacy) sameCount++;
    else Console.WriteLine($"  mismatch \"{query}\": optimized={optimized} legacy={legacy}");
}
Check("optimized expr == legacy expr on 50k rows (7 queries)", sameCount == 7, sameCount + "/7");

// 12) Замер: сколько времени экономит отказ от Convert на строковых колонках.
var sw = System.Diagnostics.Stopwatch.StartNew();
compareView.RowFilter = BuildRowFilter(compareTable, "Minsk");
sw.Stop();
long optimizedMs = sw.ElapsedMilliseconds;

sw.Restart();
compareView.RowFilter = BuildRowFilter(compareTable, "Minsk", legacyConvertEverywhere: true);
sw.Stop();
long legacyMs = sw.ElapsedMilliseconds;
Console.WriteLine($"  timing 50k rows: optimized={optimizedMs} ms, legacy={legacyMs} ms");

// 13) Экспорт «только видимых строк» (повторяет BelstatViewModel.GetExportData):
//     при активном RowFilter читаются именно отобранные строки, а общее число берётся из таблицы.
compareView.RowFilter = BuildRowFilter(compareTable, "Grodno");
var exported = new List<string[]>(compareView.Count);
foreach (DataRowView rowView in compareView)
    exported.Add(new[] { rowView[0]?.ToString() ?? string.Empty, rowView[1]?.ToString() ?? string.Empty });
Check("export reads filtered rows only",
    exported.Count == compareView.Count && exported.Count > 0 && exported.Count < compareTable.Rows.Count,
    exported.Count + " of " + compareTable.Rows.Count);
Check("export every row matches the query",
    exported.All(r => r[0].Contains("Grodno", StringComparison.OrdinalIgnoreCase)
                   || r[1].Contains("Grodno", StringComparison.OrdinalIgnoreCase)),
    exported.Count + " rows checked");

compareView.RowFilter = string.Empty;
var unfiltered = compareView.Count;
Check("unfiltered view returns all rows", unfiltered == compareTable.Rows.Count, unfiltered + " rows");

Console.WriteLine(fail == 0 ? "ALL PROBE TESTS PASSED" : "PROBE FAILURES: " + fail);
return fail == 0 ? 0 : 1;
