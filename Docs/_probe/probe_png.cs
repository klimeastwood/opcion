#:package QuestPDF@2026.7.2
#:property AllowMissingPrunePackageData=true

// Визуальная проверка: подпись на последней странице в PNG.
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

const string Signature = "Проверил следователь _______________";

static void BuildBody(ColumnDescriptor column, int rows, string variant)
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

    switch (variant)
    {
        case "tail":
            column.Item().PaddingTop(12).Text(Signature);
            break;
        case "extend-vertical":
            column.Item().PaddingTop(12).ExtendVertical().AlignBottom().Text(Signature);
            break;
    }
}

var root = Path.Combine(AppContext.BaseDirectory, "pngs");
Directory.CreateDirectory(root);

foreach (var rows in new[] { 10, 60 })
{
    foreach (var variant in new[] { "tail", "extend-vertical" })
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11));
                page.Content().Column(column => { BuildBody(column, rows, variant); });
                page.Footer().Text(text =>
                {
                    text.Span("Страница ");
                    text.CurrentPageNumber();
                    text.Span(" из ");
                    text.TotalPages();
                });
            });
        });

        var target = Path.Combine(root, $"{variant}-{rows}");
        Directory.CreateDirectory(target);
        document.GenerateImages(
            pageNumber => Path.Combine(target, $"page-{pageNumber}.png"),
            new ImageGenerationSettings { ImageFormat = ImageFormat.Png });

        Console.WriteLine($"{variant}-{rows}: {string.Join(", ", Directory.GetFiles(target).Select(Path.GetFileName))}");
    }
}
