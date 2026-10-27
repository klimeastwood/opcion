#:package QuestPDF@2026.7.2
#:property AllowMissingPrunePackageData=true

using System.Reflection;
using System.Text;

var assembly = typeof(QuestPDF.Fluent.Document).Assembly;
var report = new List<string> { "ASSEMBLY: " + assembly.FullName };

foreach (var type in assembly.GetTypes().Where(t => t.Name.StartsWith("ShowIf") || t.Name == "ShowOnce" || t.Name.Contains("Skip")).OrderBy(t => t.FullName))
{
    report.Add("TYPE " + type.FullName + " : " + type.BaseType?.FullName);
    foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        report.Add($"    PROP {property.PropertyType.Name} {property.Name}");
    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName))
        report.Add($"    METH {method.ReturnType.Name} {method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
}

report.Add("---- статические расширения с 'Show'/'Skip' в имени ----");
foreach (var type in assembly.GetTypes().Where(t => t.IsAbstract && t.IsSealed))
{
    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
    {
        if (method.Name.Contains("Show") || method.Name.Contains("Skip") || method.Name == "PageNumbers")
        {
            var parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
            report.Add($"{type.Name}.{method.Name}({parameters}) -> {method.ReturnType.Name}");
        }
    }
}

foreach (var line in report)
    Console.WriteLine(line);

File.WriteAllText(
    @"D:\KLIMIATA\docs\BGUIR\PROJECT\NalogParserWPF\Docs\_probe\out_api.txt",
    string.Join(Environment.NewLine, report),
    new UTF8Encoding(false));
