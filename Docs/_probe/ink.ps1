param([string]$Root = 'D:\KLIMIATA\docs\BGUIR\PROJECT\NalogParserWPF\Docs\_probe\sigc')

Add-Type -AssemblyName System.Drawing

foreach ($dir in Get-ChildItem $Root -Directory -Filter 'png-*') {
    $png = Join-Path $dir.FullName 'page-0.png'
    if (-not (Test-Path $png)) { continue }

    $src = [System.Drawing.Image]::FromFile($png)
    $tw = 595
    $th = [int][Math]::Round($src.Height * $tw / $src.Width)
    $small = New-Object System.Drawing.Bitmap $tw, $th
    $g = [System.Drawing.Graphics]::FromImage($small)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($src, 0, 0, $tw, $th)
    $g.Dispose()
    $src.Dispose()

    $rect = New-Object System.Drawing.Rectangle 0, 0, $tw, $th
    $data = $small.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $data.Stride
    $buf = New-Object byte[] ($stride * $th)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buf, 0, $buf.Length)
    $small.UnlockBits($data)

    $rows = New-Object int[] $th
    for ($y = 0; $y -lt $th; $y++) {
        $base = $y * $stride
        $count = 0
        for ($x = 0; $x -lt $tw; $x++) {
            $i = $base + $x * 4
            if ($buf[$i] -lt 200 -and $buf[$i + 1] -lt 200 -and $buf[$i + 2] -lt 200) { $count++ }
        }
        $rows[$y] = $count
    }

    # Сегменты «чернил» ниже 45% высоты страницы; разрыв более 3 строк = новый сегмент.
    $start = [int]($th * 0.45)
    $segments = New-Object System.Collections.Generic.List[string]
    $segStart = -1
    $lastInk = -1

    for ($y = $start; $y -lt $th; $y++) {
        if ($rows[$y] -gt 0) {
            if ($segStart -lt 0) {
                $segStart = $y
            }
            elseif ($y - $lastInk -gt 3) {
                $segments.Add(('{0:N1}%..{1:N1}%' -f (100.0 * $segStart / $th), (100.0 * $lastInk / $th)))
                $segStart = $y
            }
            $lastInk = $y
        }
    }

    if ($segStart -ge 0) {
        $segments.Add(('{0:N1}%..{1:N1}%' -f (100.0 * $segStart / $th), (100.0 * $lastInk / $th)))
    }

    '{0}: {1}' -f $dir.Name, (($segments -join ' | '))
}
