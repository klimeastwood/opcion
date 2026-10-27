#:package QuestPDF@2026.7.2
#:property AllowMissingPrunePackageData=true

using System;
using System.Linq;
using System.Reflection;

foreach (var method in typeof(QuestPDF.Fluent.Document)
             .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
             .OrderBy(m => m.Name))
    Console.WriteLine("D: " + method);

Console.WriteLine("--- settings ---");
foreach (var property in typeof(QuestPDF.Settings).GetProperties())
    Console.WriteLine("S: " + property.PropertyType.Name + " " + property.Name);

