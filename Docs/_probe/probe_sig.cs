#:package QuestPDF@2026.7.2
#:property AllowMissingPrunePackageData=true

// Проверка финальной конструкции подписи «Проверил следователь» из Services/PdfExportService.cs:
// подпись должна оказаться внизу ПОСЛЕДНЕЙ страницы и не создавать лишнюю страницу.
using System.Text;
using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

// Точная копия BuildSignature после правки (ExtendVertical + AlignBottom + правое выравнивание строк).
void BuildSignature(IContainer container)
{
    container.ExtendVertical().AlignBottom().PaddingBottom(4).Column(column =>
    {
        column.Spacing(4);
        column.Item().AlignRight().Text("Проверил следователь _______________").FontSize(11);
        column.Item().AlignRight().Text("Дата: 04.10.2026").FontSize(11);
    });
}

Document BuildDocument(int rows, string variant, List<int> marker)
{
    return Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(TextStyle.Default.FontFamily("Arial").FontSize(10));

            page.Header().ShowOnce().Element(header =>
                header.Text("ЗАГОЛОВОК ОТЧЁТА (только первая страница)").FontSize(20).Bold());

            page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
            {
                column.Spacing(8);
                column.Item().Text($"Строк в таблице: {rows}").FontSize(10).FontColor("#666666");

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    for (var i = 0; i < rows; i++)
                    {
                        table.Cell().Border(0.5f).Padding(4).Text("Значение " + i).FontSize(8);
                        table.Cell().Border(0.5f).Padding(4).Text(i.ToString()).FontSize(8);
                    }
                });

                // Маркер: страница, на которой начинается блок подписи (условие всегда ложно).
                column.Item().ShowIf(context =>
                {
                    marker.Add(context.PageNumber);
                    return false;
                }).Text(".");

                if (variant == "tail")
                {
                    column.Item().PaddingTop(20).Column(sig =>
                    {
                        sig.Spacing(4);
                        sig.Item().AlignRight().Text("Проверил следователь _______________").FontSize(11);
                        sig.Item().AlignRight().Text("Дата: 04.10.2026").FontSize(11);
                    });
                }
                else
                {
                    column.Item().Element(BuildSignature);
                }
            });

            page.Footer().PaddingTop(8).BorderTop(0.5f).BorderColor("#CCCCCC").Column(column =>
            {
                column.Item().PaddingTop(4).AlignCenter().Text(text =>
                {
                    text.Span("Страница ").FontSize(9).FontColor("#666666");
                    text.CurrentPageNumber().FontSize(9).FontColor("#666666");
                    text.Span(" из ").FontSize(9).FontColor("#666666");
                    text.TotalPages().FontSize(9).FontColor("#666666");
                });
            });
        });
    });
}

var report = new List<string>();
var root = Path.Combine(@"D:\KLIMIATA\docs\BGUIR\PROJECT\NalogParserWPF\Docs\_probe", "sig");
Directory.CreateDirectory(root);

foreach (var rows in new[] { 5, 20, 60, 120, 200, 400 })
{
    foreach (var variant in new[] { "tail", "bottom" })
    {
        try
        {
            var marker = new List<int>();
            var document = BuildDocument(rows, variant, marker);

            using var stream = new MemoryStream();
            document.GeneratePdf(stream);
            var pdf = stream.ToArray();

            var pages = Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Type\s*/Page[^s]").Count;
            File.WriteAllBytes(Path.Combine(root, $"{rows}-{variant}.pdf"), pdf);

            report.Add($"rows={rows} variant={variant} pages={pages} signatureStartPage={string.Join("|", marker.Distinct())}");
        }
        catch (Exception ex)
        {
            report.Add($"rows={rows} variant={variant} EXCEPTION {ex.GetType().Name}: {ex.Message}");
        }
    }
}

// Картинки для «глазной» проверки: одна страница и многостраничный отчёт.
foreach (var pair in new[] { (60, "bottom"), (200, "bottom"), (60, "tail") })
{
    try
    {
        var marker = new List<int>();
        var document = BuildDocument(pair.Item1, pair.Item2, marker);

        var target = Path.Combine(root, $"png-{pair.Item1}-{pair.Item2}");
        Directory.CreateDirectory(target);

        document.GenerateImages(
            pageNumber => Path.Combine(target, $"page-{pageNumber}.png"),
            new ImageGenerationSettings { ImageFormat = ImageFormat.Png });

        report.Add($"png rows={pair.Item1} variant={pair.Item2} files={string.Join(",", Directory.GetFiles(target).Select(Path.GetFileName).OrderBy(x => x.Length).ThenBy(x => x))}");
    }
    catch (Exception ex)
    {
        report.Add($"png rows={pair.Item1} variant={pair.Item2} EXCEPTION {ex.GetType().Name}: {ex.Message}");
    }
}

foreach (var line in report)
    Console.WriteLine(line);

File.WriteAllText(
    @"D:\KLIMIATA\docs\BGUIR\PROJECT\NalogParserWPF\Docs\_probe\out_sig.txt",
    string.Join(Environment.NewLine, report),
    new UTF8Encoding(false));
