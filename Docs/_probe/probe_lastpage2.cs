#:package QuestPDF@2026.7.2
#:property AllowMissingPrunePackageData=true

// Подбор способа разместить подпись «Проверил следователь» на последней странице.
using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

const string Signature = "Проверил следователь _______________";

static int CountPages(byte[] pdf) =>
    Regex.Matches(System.Text.Encoding.Latin1.GetString(pdf), @"/Type\s*/Page[^s]").Count;

byte[] Render(int rows, string variant, List<int> markTrace, List<int> footerTrace, int? signaturePage)
{
    var document = Document.Create(container =>
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

                // Метка: условие всегда ложно, но фиксирует страницу, где начинается подпись.
                column.Item().ShowIf(context =>
                {
                    markTrace.Add(context.PageNumber);
                    return false;
                }).Text(".");

                switch (variant)
                {
                    case "tail":
                        column.Item().PaddingTop(12).Text(Signature);
                        break;
                    case "extend-vertical":
                        column.Item().ExtendVertical().AlignBottom().Text(Signature);
                        break;
                    case "extend-align-bottom":
                        column.Item().Extend().AlignBottom().Text(Signature);
                        break;
                }
            });

            page.Footer().Column(column =>
            {
                if (signaturePage.HasValue)
                {
                    column.Item().ShowIf(context =>
                    {
                        footerTrace.Add(context.PageNumber);
                        return context.PageNumber == signaturePage.Value;
                    }).Text(Signature);
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
    });

    using var stream = new MemoryStream();
    document.GeneratePdf(stream);
    return stream.ToArray();
}

var results = new List<string>();
var dir = Path.Combine(AppContext.BaseDirectory, "lp2");
Directory.CreateDirectory(dir);

foreach (var rows in new[] { 10, 60, 120, 200, 480, 500 })
{
    foreach (var variant in new[] { "tail", "extend-vertical", "extend-align-bottom", "footer-known" })
    {
        var markTrace = new List<int>();
        var footerTrace = new List<int>();

        try
        {
            if (variant == "footer-known")
            {
                var measured = Render(rows, "plain", markTrace, footerTrace, null);
                var knownTotal = CountPages(measured);
                var final = Render(rows, "plain", markTrace, footerTrace, knownTotal);
                File.WriteAllBytes(Path.Combine(dir, $"lp2-{rows}-{variant}.pdf"), final);

                results.Add($"rows={rows} variant={variant} measured={knownTotal} finalPages={CountPages(final)} " +
                            $"shownOn={string.Join("|", footerTrace.Distinct())}");
                continue;
            }

            var pdf = Render(rows, variant, markTrace, footerTrace, null);
            File.WriteAllBytes(Path.Combine(dir, $"lp2-{rows}-{variant}.pdf"), pdf);

            results.Add($"rows={rows} variant={variant} pages={CountPages(pdf)} " +
                        $"signatureStartPage={(markTrace.Count > 0 ? string.Join("|", markTrace.Distinct()) : "-")}");
        }
        catch (Exception ex)
        {
            results.Add($"rows={rows} variant={variant} EXCEPTION {ex.GetType().Name}: {ex.Message}");
        }
    }
}

foreach (var line in results)
    Console.WriteLine(line);

