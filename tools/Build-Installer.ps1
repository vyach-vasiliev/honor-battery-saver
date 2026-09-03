[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidatePattern('^\d+\.\d+\.\d+(?:\.\d+)?$')]
    [string]$Version,

    [string]$OutputDirectory,

    [string]$InnoSetupCompiler,

    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solutionPath = Join-Path $projectRoot 'HonorBatterySaver.sln'
$publishRoot = Join-Path $projectRoot 'artifacts\publish'
$trayPublishDirectory = Join-Path $publishRoot 'Tray'
$servicePublishDirectory = Join-Path $publishRoot 'Service'
$installerScript = Join-Path $projectRoot 'installer\HonorBatterySaver.iss'

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$buildProperties = Get-Content -LiteralPath (Join-Path $projectRoot 'Directory.Build.props') -Raw
    $Version = $buildProperties.SelectSingleNode('/Project/PropertyGroup/Version').InnerText.Trim()
}
if ($Version -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') {
    throw "Invalid installer version in Directory.Build.props or -Version: $Version"
}
Write-Host "Building Honor Battery Saver $Version"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot 'artifacts\installer'
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

function Assert-PathWithinProject {
    param([Parameter(Mandatory)][string]$Path)

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $projectPrefix = $projectRoot.TrimEnd('\') + '\'
    if (-not $resolvedPath.StartsWith($projectPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the project: $resolvedPath"
    }
}

function Remove-BuildDirectory {
    param([Parameter(Mandatory)][string]$Path)

    Assert-PathWithinProject -Path $Path
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code ${LASTEXITCODE}: dotnet $($Arguments -join ' ')"
    }
}

function Find-InnoSetupCompiler {
    if (-not [string]::IsNullOrWhiteSpace($InnoSetupCompiler)) {
        $candidate = [System.IO.Path]::GetFullPath($InnoSetupCompiler)
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            throw "Inno Setup Compiler was not found at: $candidate"
        }
        return $candidate
    }

    $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    throw @'
Inno Setup Compiler was not found. Install Inno Setup 6.3 or newer from
https://jrsoftware.org/isdl.php and run this script again. No dependency was installed automatically.
'@
}

$dotnetCommand = Get-Command 'dotnet' -ErrorAction SilentlyContinue
if ($null -eq $dotnetCommand) {
    throw '.NET SDK 10 x64 is required, but dotnet was not found.'
}

$sdkVersion = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or $sdkVersion -notmatch '^10\.') {
    throw ".NET SDK 10 x64 is required. Detected SDK version: $sdkVersion"
}

Remove-BuildDirectory -Path $trayPublishDirectory
Remove-BuildDirectory -Path $servicePublishDirectory
Assert-PathWithinProject -Path $OutputDirectory
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

Invoke-DotNet -Arguments @('restore', $solutionPath)
Invoke-DotNet -Arguments @(
    'build', $solutionPath,
    '-c', $Configuration,
    '--no-restore',
    "-p:Version=$Version"
)
if (-not $SkipTests) {
    Invoke-DotNet -Arguments @('test', $solutionPath, '-c', $Configuration, '--no-build', '--no-restore')
}

Invoke-DotNet -Arguments @(
    'publish', (Join-Path $projectRoot 'src\HonorBatterySaver.Tray'),
    '-c', $Configuration,
    '-r', 'win-x64',
    '--self-contained', 'false',
    '--no-restore',
    "-p:Version=$Version",
    '-o', $trayPublishDirectory
)
Invoke-DotNet -Arguments @(
    'publish', (Join-Path $projectRoot 'src\HonorBatterySaver.Service'),
    '-c', $Configuration,
    '-r', 'win-x64',
    '--self-contained', 'false',
    '--no-restore',
    "-p:Version=$Version",
    '-o', $servicePublishDirectory
)

$compiler = Find-InnoSetupCompiler
& $compiler "/DMyAppVersion=$Version" "/DSourceRoot=$publishRoot" "/O$OutputDirectory" $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup Compiler failed with exit code $LASTEXITCODE."
}

$setupPath = Join-Path $OutputDirectory 'HonorBatterySaverSetup.exe'
if (-not (Test-Path -LiteralPath $setupPath -PathType Leaf)) {
    throw "The compiler completed without producing the expected installer: $setupPath"
}

Write-Host "Installer created: $setupPath"
