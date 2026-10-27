$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$root = 'd:\KLIMIATA\docs\BGUIR\PROJECT\NalogParserWPF\NalogParserWPF'
$files = Get-ChildItem -Path $root -Recurse -Include *.xaml -File |
    Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' }

$defined = @()
$used = @()
foreach ($f in $files) {
    $text = [System.IO.File]::ReadAllText($f.FullName)
    foreach ($m in [regex]::Matches($text, 'x:Key="(?<k>[A-Za-z0-9_]+)"')) { $defined += $m.Groups['k'].Value }
    foreach ($m in [regex]::Matches($text, '\{StaticResource\s+(?<k>[A-Za-z0-9_]+)')) { $used += (@{ Key = $m.Groups['k'].Value; File = $f.Name; Line = ($text.Substring(0, $m.Index) -split "`n").Count }) }
}
$definedSet = $defined | Sort-Object -Unique
$missing = $used | Where-Object { $definedSet -notcontains $_.Key } | Sort-Object Key -Unique
"=== Статические ресурсы, не найденные среди x:Key ==="
if ($missing) { $missing | ForEach-Object { "$($_.Key)  ($($_.File):$($_.Line))" } } else { "(нет)" }
"=== Всего x:Key: $($definedSet.Count); использовано StaticResource: $($used.Count) ==="
