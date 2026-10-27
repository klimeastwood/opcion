// Проверка живого ответа Белстата: datastructure (names.ru) + values (какие языки приходят).
// Запуск: dotnet run Docs\_probe\probe_belstat_ds.cs [indicatorCode]
#:property UseAppHost=false
#:property AllowMissingPrunePackageData=true
#:property JsonSerializerIsReflectionEnabledByDefault=true
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var code = args.Length > 0 ? args[0] : "10103000003";

using var http = new HttpClient
{
    BaseAddress = new Uri("https://dataportal.belstat.gov.by/osids-public-api/"),
    Timeout = TimeSpan.FromSeconds(60)
};
http.DefaultRequestHeaders.Add("Accept-Language", "ru");

var dsJson = await http.GetStringAsync($"sdmx-api/indicator/datastructure/SDMX-JSON/{code}");
var report = new List<string> { $"DS JSON length = {dsJson.Length}" };

var ds = JsonSerializer.Deserialize<StructureResponse>(dsJson)!;
report.Add($"conceptSchemes={ds.Data.ConceptSchemes.Count} codelists={ds.Data.Codelists.Count} dataStructures={ds.Data.DataStructures.Count}");

var concepts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
foreach (var scheme in ds.Data.ConceptSchemes)
    foreach (var concept in scheme.Concepts)
        if (!string.IsNullOrWhiteSpace(concept.Names?.Ru))
            concepts[concept.Id] = concept.Names!.Ru!;

var codelists = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
foreach (var codelist in ds.Data.Codelists)
{
    var codes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var item in codelist.Codes)
    {
        var ru = item.Names?.Ru ?? item.Name;
        if (!string.IsNullOrWhiteSpace(ru))
            codes[item.Id] = ru!;
    }

    codelists[codelist.Id] = codes;
}

var structure = ds.Data.DataStructures.FirstOrDefault();
if (structure?.DataStructureComponents?.DimensionList != null)
{
    foreach (var dimension in structure.DataStructureComponents.DimensionList.Dimensions)
    {
        concepts.TryGetValue(ExtractUrnId(dimension.ConceptIdentity) ?? "", out var ruName);
        var codelistId = ExtractUrnId(dimension.LocalRepresentation?.Enumeration);
        var codeCount = codelistId != null && codelists.TryGetValue(codelistId, out var codes) ? codes.Count : 0;

        report.Add($"DIM {dimension.Id} concept={ExtractUrnId(dimension.ConceptIdentity)} ru={(ruName ?? "<нет>")} codelist={codelistId} codes={codeCount}");

        if (codelistId != null && codelists.TryGetValue(codelistId, out var map))
        {
            foreach (var pair in map.Take(4))
                report.Add($"    {pair.Key} => {pair.Value}");
        }
    }
}

var valuesJson = await http.PostAsync(
    $"sdmx-api/indicator/values/SDMX-JSON/{code}",
    new StringContent("{\"years\":[2019,2020]}", Encoding.UTF8, "application/json")).Result.Content.ReadAsStringAsync();

var values = JsonSerializer.Deserialize<ValuesResponse>(valuesJson);
if (values?.Structure?.Dimensions != null)
{
    foreach (var dimension in values.Structure.Dimensions.Observation)
    {
        report.Add($"VALUES DIM {dimension.Id} name=\"{dimension.Name}\" values={dimension.Values.Count}");
        foreach (var value in dimension.Values.Take(4))
            report.Add($"    {value.Id} => \"{value.Name}\"");

        if (codelists.TryGetValue("CL_" + dimension.Id, out var map))
        {
            foreach (var value in dimension.Values.Take(4))
                report.Add($"    RU[{value.Id}] = {(map.TryGetValue(value.Id, out var ru) ? ru : "<нет совпадения>")}");
        }
    }

    report.Add("OBSERVATIONS: " + values.DataSets.Sum(d => d.Observations.Count));
}
else
{
    report.Add("VALUES: неудачный ответ: " + valuesJson[..Math.Min(300, valuesJson.Length)]);
}

foreach (var line in report)
    Console.WriteLine(line);

File.WriteAllText(
    @"D:\KLIMIATA\docs\BGUIR\PROJECT\NalogParserWPF\Docs\_probe\out_ds.txt",
    string.Join(Environment.NewLine, report),
    new UTF8Encoding(false));

static string? ExtractUrnId(string? urn)
{
    if (string.IsNullOrWhiteSpace(urn))
        return null;

    var value = urn.Trim();
    var colon = value.LastIndexOf(':');
    if (colon >= 0)
        value = value[(colon + 1)..];

    if (value.EndsWith(")", StringComparison.Ordinal))
    {
        var open = value.LastIndexOf('(');
        if (open >= 0)
            value = value[..open];
    }

    var dot = value.LastIndexOf('.');
    return dot >= 0 ? value[(dot + 1)..] : value;
}

public class LocalizedNames
{
    [JsonPropertyName("en")] public string? En { get; set; }
    [JsonPropertyName("ru")] public string? Ru { get; set; }
    [JsonPropertyName("be")] public string? Be { get; set; }
}

public class StructureResponse
{
    [JsonPropertyName("data")] public StructureData Data { get; set; } = new();
}

public class StructureData
{
    [JsonPropertyName("conceptSchemes")] public List<ConceptScheme> ConceptSchemes { get; set; } = new();
    [JsonPropertyName("codelists")] public List<Codelist> Codelists { get; set; } = new();
    [JsonPropertyName("dataStructures")] public List<Structure> DataStructures { get; set; } = new();
}

public class ConceptScheme
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("concepts")] public List<Concept> Concepts { get; set; } = new();
}

public class Concept
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("names")] public LocalizedNames? Names { get; set; }
}

public class Codelist
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("names")] public LocalizedNames? Names { get; set; }
    [JsonPropertyName("codes")] public List<Code> Codes { get; set; } = new();
}

public class Code
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("names")] public LocalizedNames? Names { get; set; }
}

public class Structure
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("dataStructureComponents")] public Components? DataStructureComponents { get; set; }
}

public class Components
{
    [JsonPropertyName("dimensionList")] public DimensionList? DimensionList { get; set; }
}

public class DimensionList
{
    [JsonPropertyName("dimensions")] public List<StructureDimension> Dimensions { get; set; } = new();
}

public class StructureDimension
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("conceptIdentity")] public string? ConceptIdentity { get; set; }
    [JsonPropertyName("localRepresentation")] public LocalRepresentation? LocalRepresentation { get; set; }
}

public class LocalRepresentation
{
    [JsonPropertyName("enumeration")] public string? Enumeration { get; set; }
}

public class ValuesResponse
{
    [JsonPropertyName("structure")] public ValuesStructure? Structure { get; set; }
    [JsonPropertyName("dataSets")] public List<ValuesDataSet> DataSets { get; set; } = new();
}

public class ValuesStructure
{
    [JsonPropertyName("dimensions")] public ValuesDimensions? Dimensions { get; set; }
}

public class ValuesDimensions
{
    [JsonPropertyName("observation")] public List<ValuesDimension> Observation { get; set; } = new();
}

public class ValuesDimension
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("values")] public List<ValuesDimensionValue> Values { get; set; } = new();
}

public class ValuesDimensionValue
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public class ValuesDataSet
{
    [JsonPropertyName("observations")] public Dictionary<string, JsonElement[]> Observations { get; set; } = new();
}
