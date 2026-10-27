$ErrorActionPreference = 'Stop'

# Splices exactly one regex match of $Pattern in $Text using $Build (scriptblock -> string).
function Get-Spliced {
    param([string]$Text, [string]$Pattern, [scriptblock]$Build)
    $ms = [regex]::Matches($Text, $Pattern)
    if ($ms.Count -ne 1) { throw ("Pattern matched {0} time(s), expected exactly 1:" -f $ms.Count) + "`r`n" + $Pattern }
    $m = $ms[0]
    $new = & $Build $m
    return $Text.Substring(0, $m.Index) + $new + $Text.Substring($m.Index + $m.Length)
}

$root = 'd:\KLIMIATA\docs\BGUIR\PROJECT\NalogParserWPF\NalogParserWPF'
$enc = New-Object System.Text.UTF8Encoding($false)

$accentBgSetter = '<Setter Property="Background" Value="{DynamicResource AccentBrush}"/>'
$onAccentFgSetter = '<Setter Property="Foreground" Value="{DynamicResource OnAccentBrush}"/>'
$lightBgSetter = '<Setter Property="Background" Value="{DynamicResource LightAccentBrush}"/>'
$primaryFgSetter = '<Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>'

# ==================== App.xaml: ComboBox item hover vs selection ====================
$appPath = Join-Path $root 'App.xaml'
$appText = [System.IO.File]::ReadAllText($appPath)

$pattern = '(?s)' +
    '(?<hi>' + [regex]::Escape('<Trigger Property="IsHighlighted" Value="True">') + ')' +
    '(?<w1>\s+)(?<s1>' + [regex]::Escape($accentBgSetter) + ')' +
    '(?<w2>\s+)(?<s2>' + [regex]::Escape($onAccentFgSetter) + ')' +
    '(?<w3>\s+)(?<e1>' + [regex]::Escape('</Trigger>') + ')' +
    '(?<w4>\s+)(?<si>' + [regex]::Escape('<Trigger Property="IsSelected" Value="True">') + ')' +
    '(?<w5>\s+)(?<s3>' + [regex]::Escape($accentBgSetter) + ')' +
    '(?<w6>\s+)(?<s4>' + [regex]::Escape($onAccentFgSetter) + ')' +
    '(?<w7>\s+)(?<e2>' + [regex]::Escape('</Trigger>') + ')'

$comboBuild = {
    param($m)
    $w1 = $m.Groups['w1'].Value
    $w2 = $m.Groups['w2'].Value
    $w3 = $m.Groups['w3'].Value
    $w4 = $m.Groups['w4'].Value
    $w5 = $m.Groups['w5'].Value
    $w6 = $m.Groups['w6'].Value
    $w7 = $m.Groups['w7'].Value
    $out1 = $m.Groups['hi'].Value + $w1 + $lightBgSetter + $w2 + $primaryFgSetter + $w3 +
        $m.Groups['e1'].Value + $w4 + $m.Groups['si'].Value + $w5 +
        $accentBgSetter + $w6 + $onAccentFgSetter + $w7 + $m.Groups['e2'].Value + $w3 +
        '<Trigger Property="IsEnabled" Value="False">' + $w1 +
        '<Setter Property="Opacity" Value="0.5"/>' + $w3 + '</Trigger>'
    return $out1
}

$appNew = Get-Spliced -Text $appText -Pattern $pattern -Build $comboBuild

# ==================== MainWindow.xaml: compact icon-only action buttons ====================
$mwPath = Join-Path $root 'MainWindow.xaml'
$mwText = [System.IO.File]::ReadAllText($mwPath)

$mwNew = $mwText
foreach ($res in @('IconRepeatOnlyTemplate', 'IconViewOnlyTemplate')) {
    $ctAttr = 'ContentTemplate="{StaticResource ' + $res + '}"'
    $pat = '(?s)(?<ct>' + [regex]::Escape($ctAttr) + ')' +
        '(?<nl>\s+)(?<f>FontSize="14")' +
        '(?<nl2>\s+)(?<p>Padding="6,4")' +
        '(?<nl3>\s+)(?<m>Margin="2")'
    $mwBuild = {
        param($m)
        $nl = $m.Groups['nl'].Value
        $ind = [regex]::Replace($nl, '(?s)^.*[\r\n]', '')
        return $m.Groups['ct'].Value + $nl + $ind + 'Style="{StaticResource IconButton}"'
    }
    $mwNew = Get-Spliced -Text $mwNew -Pattern $pat -Build $mwBuild
}

[System.IO.File]::WriteAllText($appPath, $appNew, $enc)
[System.IO.File]::WriteAllText($mwPath, $mwNew, $enc)
Write-Output 'OK: App.xaml and MainWindow.xaml patched'
