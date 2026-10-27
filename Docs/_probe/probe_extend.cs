#:package QuestPDF@2026.7.2
#:property AllowMissingPrunePackageData=true

using System.Reflection;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var report = new List<string>();

// 1. Какие расширения, связанные с выравниванием по низу страницы, есть в сборке.
var assembly = typeof(Document).Assembly;
foreach (var type in assembly.GetTypes().Where(t => t.IsAbstract && t.IsSealed))
{
    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
    {
        if (method.Name.Contains("Extend") || method.Name.Contains("Align"))
        {
            var parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
            report.Add($"API {type.Name}.{method.Name}({parameters}) -> {method.ReturnType.Name}");
        }
    }
}

const string SignatureText = "ПОДПИСЬ-ПРОВЕРИЛ-СЛЕДОВАТЕЛЬ-МАРКЕР";

// 2. Поведение: как расположить подпись внизу последней страницы, не создавая лишней страницы.
foreach (var rows in new[] { 10, 20, 30, 40, 120 })
{
    foreach (var variant in new[] { "none", "extend-item", "extend-wrapper", "extend-align-bottom" })
    {
        var trace = new List<int>();
        var path = Path.Combine(AppContext.BaseDirectory, $"loc-{rows}-{variant}.pdf");

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

                        switch (variant)
                        {
                            case "extend-item":
                                column.Item().Extend();
                                break;
                            case "extend-wrapper":
                                column.Item().Element(x => x.Extend());
                                break;
                            case "extend-align-bottom":
                                column.Item().Element(x => x.Extend().AlignBottom());
                                break;
                        }

                        column.Item()
                            .ShowIf(context =>
                            {
                                trace.Add(context.PageNumber);
                                return true;
                            })
                            .Text(SignatureText).FontSize(11);
                    });

                    page.Footer().Column(column =>
                    {
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

            var text = Encoding.Latin1.GetString(File.ReadAllBytes(path));
            var pages = System.Text.RegularExpressions.Regex.Matches(text, @"/Type\s*/Page[^s]").Count;
            report.Add($"rows={rows} variant={variant} pages={pages} signaturePage={string.Join(",", trace)}");
        }
        catch (Exception ex)
        {
            report.Add($"rows={rows} variant={variant} EXCEPTION {ex.GetType().Name}: {ex.Message}");
        }
    }
}

foreach (var line in report)
    Console.WriteLine(line);

File.WriteAllText(
    @"D:\KLIMIATA\docs\BGUIR\PROJECT\NalogParserWPF\Docs\_probe\out_extend.txt",
    string.Join(Environment.NewLine, report),
    new UTF8Encoding(false));
