param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $projectRoot 'design\tray-icons\concept-v3-svg-hinted'
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$edgePath = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
if (-not (Test-Path -LiteralPath $edgePath)) {
    throw 'Microsoft Edge is required to rasterize the SVG previews.'
}

$culture = [System.Globalization.CultureInfo]::InvariantCulture
$accent = '#6B82F5'
$fontFamily = [System.Drawing.FontFamily]::new('Bahnschrift SemiBold Condensed')
$format = [System.Drawing.StringFormat]::GenericTypographic

function New-RoundedPath {
    param(
        [float] $X,
        [float] $Y,
        [float] $Width,
        [float] $Height,
        [float] $Radius
    )

    $diameter = $Radius * 2
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Format-Coordinate {
    param([float] $Value)
    return $Value.ToString('0.###', $culture)
}

function Convert-GlyphPathToSvgData {
    param(
        [System.Drawing.Drawing2D.GraphicsPath] $Path,
        [System.Drawing.RectangleF] $Bounds,
        [float] $Scale,
        [float] $OffsetX,
        [float] $OffsetY
    )

    $points = $Path.PathPoints
    $types = $Path.PathTypes
    $parts = [System.Collections.Generic.List[string]]::new()
    $index = 0
    while ($index -lt $points.Length) {
        $kind = $types[$index] -band 0x07
        $closed = ($types[$index] -band 0x80) -ne 0
        $point = $points[$index]
        $x = ($point.X - $Bounds.X) * $Scale + $OffsetX
        $y = ($point.Y - $Bounds.Y) * $Scale + $OffsetY

        if ($kind -eq 0) {
            $parts.Add("M$(Format-Coordinate $x) $(Format-Coordinate $y)")
            $index++
            continue
        }

        if ($kind -eq 1) {
            $parts.Add("L$(Format-Coordinate $x) $(Format-Coordinate $y)")
            if ($closed) {
                $parts.Add('Z')
            }
            $index++
            continue
        }

        if ($kind -eq 3 -and $index + 2 -lt $points.Length) {
            $control1 = $points[$index]
            $control2 = $points[$index + 1]
            $end = $points[$index + 2]
            $control1X = ($control1.X - $Bounds.X) * $Scale + $OffsetX
            $control1Y = ($control1.Y - $Bounds.Y) * $Scale + $OffsetY
            $control2X = ($control2.X - $Bounds.X) * $Scale + $OffsetX
            $control2Y = ($control2.Y - $Bounds.Y) * $Scale + $OffsetY
            $endX = ($end.X - $Bounds.X) * $Scale + $OffsetX
            $endY = ($end.Y - $Bounds.Y) * $Scale + $OffsetY
            $parts.Add(
                "C$(Format-Coordinate $control1X) $(Format-Coordinate $control1Y) " +
                "$(Format-Coordinate $control2X) $(Format-Coordinate $control2Y) " +
                "$(Format-Coordinate $endX) $(Format-Coordinate $endY)")
            if (($types[$index + 2] -band 0x80) -ne 0) {
                $parts.Add('Z')
            }
            $index += 3
            continue
        }

        $index++
    }

    return [string]::Join(' ', $parts)
}

function New-TraySvg {
    param([ValidateSet('70', '90', '100')][string] $Value)

    $fontSize = if ($Value.Length -eq 3) { 17.4 } else { 20.4 }

    return @"
<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32">
  <rect x="2.25" y="2.25" width="27.5" height="27.5" rx="5.35"
        fill="none" stroke="$accent" stroke-width="2.5" />
  <text x="16" y="16.45" fill="$accent"
        font-family="Bahnschrift" font-size="$fontSize" font-weight="600" font-stretch="condensed"
        font-variant-numeric="tabular-nums" text-anchor="middle" dominant-baseline="central"
        text-rendering="optimizeLegibility">$Value</text>
</svg>
"@
}

foreach ($value in '70', '90', '100') {
    $svgPath = Join-Path $outputDirectory "tray-$value.svg"
    [System.IO.File]::WriteAllText($svgPath, (New-TraySvg $value), [System.Text.UTF8Encoding]::new($false))
}

$fontFamily.Dispose()
$format.Dispose()

$placements = [System.Collections.Generic.List[object]]::new()
$htmlImages = [System.Collections.Generic.List[string]]::new()
$row = 0
foreach ($value in '70', '90', '100') {
    $x = 10
    foreach ($size in 16, 20, 24, 32, 64, 128) {
        $y = $row * 128 + [Math]::Floor((128 - $size) / 2)
        $svgPath = Join-Path $outputDirectory "tray-$value.svg"
        $svgUri = [System.Uri]::new($svgPath).AbsoluteUri
        $placements.Add([pscustomobject]@{ Value = $value; Size = $size; X = $x; Y = $y })
        $htmlImages.Add(('<img src="{0}" style="position:absolute;left:{1}px;top:{2}px;width:{3}px;height:{3}px">' -f
            $svgUri, $x, $y, $size))
        $x += $size + 18
    }
    $row++
}

$rasterPagePath = Join-Path $outputDirectory 'rasterize.html'
$spritePath = Join-Path $outputDirectory 'rasterized-sprite.png'
$rasterPage = @"
<!doctype html>
<html><head><meta charset="utf-8"><style>
html,body { margin: 0; width: 400px; height: 384px; overflow: hidden; background: transparent; }
</style></head><body>
$([string]::Join([Environment]::NewLine, $htmlImages))
</body></html>
"@
[System.IO.File]::WriteAllText($rasterPagePath, $rasterPage, [System.Text.UTF8Encoding]::new($false))

$profileDirectory = Join-Path $outputDirectory ".edge-profile-$PID"
[System.IO.Directory]::CreateDirectory($profileDirectory) | Out-Null
$edgeArguments = @(
    '--headless=new',
    '--disable-gpu',
    '--hide-scrollbars',
    '--force-device-scale-factor=1',
    '--default-background-color=00000000',
    '--no-first-run',
    "--user-data-dir=$profileDirectory",
    '--window-size=400,384',
    "--screenshot=$spritePath",
    ([System.Uri]::new($rasterPagePath).AbsoluteUri)
)
$edgeProcess = Start-Process -FilePath $edgePath -ArgumentList $edgeArguments -WindowStyle Hidden -Wait -PassThru
if ($edgeProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $spritePath)) {
    throw 'SVG rasterization failed.'
}

$sprite = [System.Drawing.Bitmap]::FromFile($spritePath)
try {
    foreach ($placement in $placements) {
        $rectangle = [System.Drawing.Rectangle]::new(
            $placement.X,
            $placement.Y,
            $placement.Size,
            $placement.Size)
        $icon = $sprite.Clone($rectangle, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $pngPath = Join-Path $outputDirectory "tray-$($placement.Value)-$($placement.Size).png"
            $icon.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $icon.Dispose()
        }
    }
}
finally {
    $sprite.Dispose()
}

$sheet = [System.Drawing.Bitmap]::new(900, 390, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$canvas = [System.Drawing.Graphics]::FromImage($sheet)
$canvas.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$canvas.Clear([System.Drawing.Color]::FromArgb(15, 17, 23))
$titleFont = [System.Drawing.Font]::new('Segoe UI', 18, [System.Drawing.FontStyle]::Bold)
$labelFont = [System.Drawing.Font]::new('Segoe UI', 11, [System.Drawing.FontStyle]::Regular)
$valueFont = [System.Drawing.Font]::new('Segoe UI', 13, [System.Drawing.FontStyle]::Bold)
$white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(243, 245, 250))
$muted = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(170, 178, 194))
$tileBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(29, 33, 43))
$canvas.DrawString('Tray icons · SVG concept 3', $titleFont, $white, 30, 20)
$canvas.DrawString('Bahnschrift SemiBold Condensed · font hinting · dedicated PNG for each target size', $labelFont, $muted, 30, 55)

$cardX = 30
foreach ($value in '70', '90', '100') {
    $cardPath = New-RoundedPath $cardX 95 260 260 18
    $canvas.FillPath($tileBrush, $cardPath)
    $cardPath.Dispose()
    $large = [System.Drawing.Image]::FromFile((Join-Path $outputDirectory "tray-$value-128.png"))
    $canvas.DrawImageUnscaled($large, $cardX + 66, 120)
    $large.Dispose()
    $canvas.DrawString($value, $valueFont, $white, $cardX + 22, 112)

    $sizeX = $cardX + 45
    foreach ($size in 16, 20, 24, 32) {
        $small = [System.Drawing.Image]::FromFile((Join-Path $outputDirectory "tray-$value-$size.png"))
        $canvas.DrawImageUnscaled($small, $sizeX, 286 + [Math]::Floor((32 - $size) / 2))
        $small.Dispose()
        $canvas.DrawString("$size", $labelFont, $muted, $sizeX - 1, 326)
        $sizeX += 50
    }
    $cardX += 290
}

$previewPath = Join-Path $outputDirectory 'preview.png'
$sheet.Save($previewPath, [System.Drawing.Imaging.ImageFormat]::Png)
$tileBrush.Dispose()
$muted.Dispose()
$white.Dispose()
$valueFont.Dispose()
$labelFont.Dispose()
$titleFont.Dispose()
$canvas.Dispose()
$sheet.Dispose()

[System.IO.File]::Delete($rasterPagePath)
[System.IO.File]::Delete($spritePath)
$resolvedOutput = (Resolve-Path -LiteralPath $outputDirectory).Path.TrimEnd([System.IO.Path]::DirectorySeparatorChar)
$resolvedProfile = (Resolve-Path -LiteralPath $profileDirectory).Path
if (-not $resolvedProfile.StartsWith($resolvedOutput + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to remove an Edge profile outside the icon output directory.'
}
[System.IO.Directory]::Delete($resolvedProfile, $true)

Write-Output $previewPath
