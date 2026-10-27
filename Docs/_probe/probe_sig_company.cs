#:package QuestPDF@2026.7.2
#:property AllowMissingPrunePackageData=true

// Проверка подписи «Проверил следователь» на ОДНОСТРАНИЧНЫХ отчётах (BuildCompanyPage /
// BuildBatchPage в Services/PdfExportService.cs): тело отчёта заканчивается вызовом
// column.Item().Element(BuildSignature), а BuildSignature = ExtendVertical + AlignBottom.
// Контроль: строится та же структура, что и в отчёте по УНП, в трёх вариантах —
//   full-bottom  — данные есть, подпись прижата к низу (новая разметка);
//   full-tail    — данные есть, подпись сразу после содержимого (прежняя разметка);
//   empty-bottom — данных нет (ветка «return» до подписи), подписи быть не должно.
// Задача проверки: ни один вариант не создаёт лишнюю страницу и не падает.
using System.Text;
using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

// Точная копия BuildSignature после правки.
void BuildSignature(IContainer container)
{
    container.ExtendVertical().AlignBottom().PaddingBottom(4).Column(column =>
    {
        column.Spacing(4);
        column.Item().AlignRight().Text("Проверил следователь _______________").FontSize(11);
        column.Item().AlignRight().Text("Дата: 04.10.2026").FontSize(11);
    });
}

Document BuildDocument(bool hasData, bool bottom)
{
    return Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(TextStyle.Default.FontFamily("Arial").FontSize(10));

            page.Header().ShowOnce().Element(header => header.Row(row =>
            {
                row.ConstantItem(90).PaddingHorizontal(6).PaddingVertical(2)
                    .Border(1f).BorderColor("#1A8B8B").AlignCenter().AlignMiddle()
                    .Text("SK").FontSize(22);

                row.RelativeItem().PaddingLeft(10).AlignMiddle().Column(column =>
                {
                    column.Spacing(3);
                    column.Item().Text("Следственный комитет Республики Беларусь")
                        .FontSize(20).Bold().FontColor("#1A8B8B");
                    column.Item().Text("Информация из официальных источников")
                        .FontSize(10).FontColor("#666666");
                });
            }));

            page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
            {
                column.Spacing(5);

                column.Item().Text("Результат проверки контрагента")
                    .FontSize(16).Bold().FontColor("#1A5C5C");
                column.Item().LineHorizontal(1, Unit.Point).LineColor("#1A8B8B");

                if (!hasData)
                {
                    // Ветка BuildCompanyPage: данных нет — подпись не выводится.
                    column.Item().Text("Данные не найдены в сохраненной записи.").FontSize(12);
                    return;
                }

                column.Item().Text("ИНФОРМАЦИЯ О КОНТРАГЕНТЕ")
                    .FontSize(13).Bold().FontColor("#1A5C5C");
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(150);
                        columns.RelativeColumn();
                    });

                    for (var i = 1; i <= 8; i++)
                    {
                        table.Cell().Border(0.5f).Background("#F0F8F8").Padding(6)
                            .Text($"Реквизит {i}").FontSize(10).Bold().FontColor("#1A5C5C");
                        table.Cell().Border(0.5f).Padding(6).Text($"Значение {i}").FontSize(10);
                    }
                });

                column.Item().PaddingTop(10).Background("#E8F5E9").Padding(10)
                    .Text("КОНТРАГЕНТ ДЕЙСТВУЕТ").FontSize(13).Bold()
                    .FontColor(Colors.Green.Darken2);

                if (bottom)
                {
                    column.Item().Element(BuildSignature);
                }
                else
                {
                    column.Item().PaddingTop(20).Column(sig =>
                    {
                        sig.Spacing(4);
                        sig.Item().AlignRight().Text("Проверил следователь _______________").FontSize(11);
                        sig.Item().AlignRight().Text("Дата: 04.10.2026").FontSize(11);
                    });
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
var root = Path.Combine(@"D:\KLIMIATA\docs\BGUIR\PROJECT\NalogParserWPF\Docs\_probe", "sigc");
Directory.CreateDirectory(root);

foreach (var scenario in new[]
         {
             (Name: "full-bottom", Data: true, Bottom: true),
             (Name: "full-tail", Data: true, Bottom: false),
             (Name: "empty-bottom", Data: false, Bottom: true),
         })
{
    try
    {
        var document = BuildDocument(scenario.Data, scenario.Bottom);

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        var pdf = stream.ToArray();
        var pages = Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Type\s*/Page[^s]").Count;
        File.WriteAllBytes(Path.Combine(root, $"{scenario.Name}.pdf"), pdf);

        var target = Path.Combine(root, $"png-{scenario.Name}");
        Directory.CreateDirectory(target);
        document.GenerateImages(
            pageNumber => Path.Combine(target, $"page-{pageNumber}.png"),
            new ImageGenerationSettings { ImageFormat = ImageFormat.Png });

        report.Add($"{scenario.Name} pages={pages} png={Directory.GetFiles(target).Length}");
    }
    catch (Exception ex)
    {
        report.Add($"{scenario.Name} EXCEPTION {ex.GetType().Name}: {ex.Message}");
    }
}

foreach (var line in report)
    Console.WriteLine(line);

File.WriteAllText(
    Path.Combine(root, "out.txt"),
    string.Join(Environment.NewLine, report),
    new UTF8Encoding(false));
