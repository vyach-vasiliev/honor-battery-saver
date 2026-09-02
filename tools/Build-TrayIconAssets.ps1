param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceDirectory = Join-Path $projectRoot 'design\tray-icons\concept-v3-svg-hinted'
$targetDirectory = Join-Path $projectRoot 'src\HonorBatterySaver.Tray\Assets\Tray'
[System.IO.Directory]::CreateDirectory($targetDirectory) | Out-Null

$sizes = 16, 20, 24, 32, 64, 128
foreach ($value in '70', '90', '100') {
    $images = foreach ($size in $sizes) {
        $sourcePath = Join-Path $sourceDirectory "tray-$value-$size.png"
        if (-not (Test-Path -LiteralPath $sourcePath)) {
            throw "Missing approved tray icon frame: $sourcePath"
        }

        [pscustomobject]@{
            Size = $size
            Data = [System.IO.File]::ReadAllBytes($sourcePath)
        }
    }

    $targetPath = Join-Path $targetDirectory "tray-$value.ico"
    $stream = [System.IO.File]::Create($targetPath)
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$images.Count)
        $offset = 6 + 16 * $images.Count
        foreach ($image in $images) {
            $writer.Write([byte]$image.Size)
            $writer.Write([byte]$image.Size)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$image.Data.Length)
            $writer.Write([uint32]$offset)
            $offset += $image.Data.Length
        }

        foreach ($image in $images) {
            $writer.Write([byte[]]$image.Data)
        }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

Get-ChildItem -LiteralPath $targetDirectory -Filter 'tray-*.ico' | Select-Object FullName, Length
