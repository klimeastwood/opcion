#:package QuestPDF@2026.7.2
#:property AllowMissingPrunePackageData=true

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var results = new List<string>();

foreach (var rows in new[] { 5, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 60, 200, 500 })
{
    var trace = new List<(int Page, int? Total)>();
    var path = Path.Combine(AppContext.BaseDirectory, $"probe-{rows}.pdf");

    try
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Content().Column(column =>
                {
                    column.Item().Text($"Строк: {rows}").FontSize(16);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        for (var i = 0; i < rows; i++)
                        {
                            table.Cell().Text("Значение " + i);
                            table.Cell().Text(i.ToString());
                        }
                    });
                });

                page.Footer().Column(column =>
                {
                    column.Item()
                        .ShowIf(context =>
                        {
                            trace.Add((context.PageNumber, context.TotalPages));
                            return context.TotalPages.HasValue && context.PageNumber == context.TotalPages.Value;
                        })
                        .Text("Проверил следователь _______________");

                    column.Item().Text(text =>
                    {
                        text.Span("Страница ");
                        text.CurrentPageNumber();
                        text.Span(" из ");
                        text.TotalPages();
                    });
                });
            });
        }).GeneratePdf(path);

        var text = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(path));
        var pageObjects = System.Text.RegularExpressions.Regex.Matches(text, "/Type\\s*/Page[^s]").Count;
        var maxTotal = trace.Where(t => t.Total.HasValue).Select(t => t.Total!.Value).DefaultIfEmpty(0).Max();
        var lastPageHits = trace.Count(t => t.Total.HasValue && t.Page == t.Total.Value);

        results.Add($"rows={rows} predicates={trace.Count} maxTotal={maxTotal} pageObjects={pageObjects} lastPageHits={lastPageHits} trace={string.Join(",", trace.Select(t => $"{t.Page}/{t.Total?.ToString() ?? "null"}"))}");
    }
    catch (Exception ex)
    {
        results.Add($"rows={rows} EXCEPTION {ex.GetType().Name}: {ex.Message}");
    }
}

foreach (var line in results)
    Console.WriteLine(line);
