#:package QuestPDF@2026.7.2
#:property AllowMissingPrunePackageData=true

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

const string SignatureText = "ПОДПИСЬ-ПРОВЕРИЛ-СЛЕДОВАТЕЛЬ-МАРКЕР";

var results = new List<string>();

foreach (var placement in new[] { "footer", "content" })
{
    foreach (var mode in new[] { "none", "showif", "always" })
    {
        var trace = new List<(int Page, int? Total)>();

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, $"cmp-{placement}-{mode}.pdf");

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Content().Column(column =>
                    {
                        column.Item().Text("Строк: 120").FontSize(16);
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            for (var i = 0; i < 120; i++)
                            {
                                table.Cell().Text("Значение " + i);
                                table.Cell().Text(i.ToString());
                            }
                        });

                        if (placement == "content")
                        {
                            if (mode == "always")
                                column.Item().Text(SignatureText);
                            else if (mode == "showif")
                                column.Item().ShowIf(context =>
                                {
                                    trace.Add((context.PageNumber, context.TotalPages));
                                    return context.TotalPages.HasValue && context.PageNumber == context.TotalPages.Value;
                                }).Text(SignatureText);
                        }
                    });

                    page.Footer().Column(column =>
                    {
                        if (placement == "footer")
                        {
                            if (mode == "always")
                                column.Item().Text(SignatureText);
                            else if (mode == "showif")
                                column.Item().ShowIf(context =>
                                {
                                    trace.Add((context.PageNumber, context.TotalPages));
                                    return context.TotalPages.HasValue && context.PageNumber == context.TotalPages.Value;
                                }).Text(SignatureText);
                        }

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

            var size = new FileInfo(path).Length;
            var text = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(path));
            var pageObjects = System.Text.RegularExpressions.Regex.Matches(text, "/Type\\s*/Page[^s]").Count;

            results.Add($"{placement}/{mode}: size={size} pages={pageObjects} predicates={trace.Count} nonNull={trace.Count(t => t.Total.HasValue)} trace={string.Join(",", trace.Select(t => $"{t.Page}/{t.Total?.ToString() ?? "null"}").Take(8))}");
        }
        catch (Exception ex)
        {
            results.Add($"{placement}/{mode}: EXCEPTION {ex.GetType().Name}: {ex.Message}");
        }
    }
}

foreach (var line in results)
    Console.WriteLine(line);
