param()

Add-Type -AssemblyName System.Drawing

$accentColor = [System.Drawing.Color]::FromArgb(107, 130, 245)
$outputDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'design\tray-icons\concept-v1'
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null

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

function Draw-Digit {
    param(
        [System.Drawing.Graphics] $Graphics,
        [System.Drawing.Pen] $Pen,
        [char] $Digit,
        [float] $X,
        [float] $Y,
        [float] $Width,
        [float] $Height
    )

    switch ($Digit) {
        '0' {
            $zero = New-RoundedPath $X $Y $Width $Height ([Math]::Min($Width * 0.48, $Height * 0.22))
            $Graphics.DrawPath($Pen, $zero)
            $zero.Dispose()
        }
        '1' {
            $Graphics.DrawLine($Pen, $X + $Width * 0.20, $Y + $Height * 0.18, $X + $Width * 0.55, $Y)
            $Graphics.DrawLine($Pen, $X + $Width * 0.55, $Y, $X + $Width * 0.55, $Y + $Height)
        }
        '7' {
            $Graphics.DrawLine($Pen, $X, $Y, $X + $Width, $Y)
            $Graphics.DrawLine($Pen, $X + $Width, $Y, $X + $Width * 0.28, $Y + $Height)
        }
        '9' {
            $loopHeight = $Height * 0.54
            $nine = New-RoundedPath $X $Y $Width $loopHeight ([Math]::Min($Width * 0.48, $loopHeight * 0.48))
            $Graphics.DrawPath($Pen, $nine)
            $nine.Dispose()
            $Graphics.DrawLine($Pen, $X + $Width, $Y + $loopHeight * 0.48, $X + $Width * 0.42, $Y + $Height)
        }
    }
}

function New-TrayIconBitmap {
    param(
        [ValidateSet('70', '90', '100')]
        [string] $Value,
        [int] $Size
    )

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.ScaleTransform($Size / 16.0, $Size / 16.0)

    $outlinePen = [System.Drawing.Pen]::new($accentColor, 1.35)
    $outlinePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $outer = New-RoundedPath 1.15 1.15 13.7 13.7 2.65
    $graphics.DrawPath($outlinePen, $outer)

    if ($Value.Length -eq 2) {
        [float] $digitWidth = 3.25
        [float] $digitHeight = 6.65
        [float] $gap = 0.95
        [float] $strokeWidth = 1.12
    }
    else {
        [float] $digitWidth = 2.55
        [float] $digitHeight = 6.15
        [float] $gap = 0.65
        [float] $strokeWidth = 0.96
    }

    $totalWidth = $digitWidth * $Value.Length + $gap * ($Value.Length - 1)
    $digitX = (16 - $totalWidth) / 2
    $digitY = (16 - $digitHeight) / 2
    $digitPen = [System.Drawing.Pen]::new($accentColor, $strokeWidth)
    $digitPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $digitPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $digitPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    foreach ($digit in $Value.ToCharArray()) {
        Draw-Digit $graphics $digitPen $digit $digitX $digitY $digitWidth $digitHeight
        $digitX += $digitWidth + $gap
    }

    $digitPen.Dispose()
    $outer.Dispose()
    $outlinePen.Dispose()
    $graphics.Dispose()
    return $bitmap
}

foreach ($value in '70', '90', '100') {
    foreach ($size in 16, 20, 24, 32, 64, 128, 256) {
        $bitmap = New-TrayIconBitmap $value $size
        $path = Join-Path $outputDirectory "tray-$value-$size.png"
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        $bitmap.Dispose()
    }
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
$canvas.DrawString('Tray icons · concept 1', $titleFont, $white, 30, 20)
$canvas.DrawString('Transparent background · consistent outline · vector-built digits without fonts', $labelFont, $muted, 30, 55)

$cardX = 30
foreach ($value in '70', '90', '100') {
    $cardPath = New-RoundedPath $cardX 95 260 260 18
    $canvas.FillPath($tileBrush, $cardPath)
    $cardPath.Dispose()

    $large = New-TrayIconBitmap $value 128
    $canvas.DrawImageUnscaled($large, $cardX + 66, 120)
    $large.Dispose()
    $canvas.DrawString($value, $valueFont, $white, $cardX + 22, 112)

    $sizeX = $cardX + 45
    foreach ($size in 16, 20, 24, 32) {
        $small = New-TrayIconBitmap $value $size
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

Write-Output $previewPath
