#:package QuestPDF@2026.7.2
#:property AllowMissingPrunePackageData=true

using System.Reflection;

var assembly = typeof(QuestPDF.Fluent.Document).Assembly;

var imagePath = assembly.GetExportedTypes().First(t => t.Name == "GenerateDocumentImagePath");
foreach (var m in imagePath.GetMethods(BindingFlags.Public | BindingFlags.Instance))
    Console.WriteLine($"delegate: {m}");

foreach (var name in Enum.GetNames(typeof(QuestPDF.Infrastructure.ImageFormat)))
    Console.WriteLine($"format: {name}");

foreach (var type in assembly.GetExportedTypes())
{
    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
    {
        if (method.Name.Contains("Image") || method.Name.Contains("Generate"))
        {
            var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            Console.WriteLine($"{type.FullName}.{method.Name}({parameters}) -> {method.ReturnType.Name}");
        }
    }
}
