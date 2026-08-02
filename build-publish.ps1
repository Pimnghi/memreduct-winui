<#
.SYNOPSIS
Builds and publishes the unpackaged application directories for x64 and ARM64.
#>
[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("x64", "ARM64")]
    [string[]]$Platform = @("x64", "ARM64"),

    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$nativeProject = Join-Path $projectRoot "CoreLib\CoreLib.vcxproj"
$cliProject = Join-Path $projectRoot "CliHost\mrw-cli.vcxproj"
$managedProject = Join-Path $projectRoot "memreduct-winui.csproj"
$versionHeader = Join-Path $projectRoot "src\app.h"
$artifactRoot = Join-Path $projectRoot "artifacts"
$publishRoot = Join-Path $artifactRoot "publish"

function Get-MsBuildPath {
    $programFilesX86 = [Environment]::GetFolderPath("ProgramFilesX86")
    $vswhere = Join-Path $programFilesX86 "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path -LiteralPath $vswhere)) {
        throw "Visual Studio Installer (vswhere.exe) was not found."
    }

    $installationPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($installationPath)) {
        throw "Visual Studio 2022 with MSBuild was not found."
    }

    $msbuild = Join-Path $installationPath "MSBuild\Current\Bin\MSBuild.exe"
    if (-not (Test-Path -LiteralPath $msbuild)) {
        throw "MSBuild.exe was not found at '$msbuild'."
    }
    return $msbuild
}

function Get-PeMachine {
    param([Parameter(Mandatory)][string]$Path)

    $stream = [IO.File]::OpenRead($Path)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        $stream.Position = $peOffset + 4
        return $reader.ReadUInt16()
    }
    finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}

function Assert-PeMachine {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Architecture
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required binary is missing: $Path"
    }

    $expected = if ($Architecture -eq "x64") { 0x8664 } else { 0xAA64 }
    $actual = Get-PeMachine -Path $Path
    if ($actual -ne $expected) {
        throw "Architecture mismatch for '$Path': expected 0x$($expected.ToString('X4')), got 0x$($actual.ToString('X4'))."
    }
}

function Remove-ArtifactDirectory {
    param([Parameter(Mandatory)][string]$Path)

    $root = [IO.Path]::GetFullPath($artifactRoot).TrimEnd('\') + '\'
    $target = [IO.Path]::GetFullPath($Path)
    if (-not $target.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a directory outside '$artifactRoot': $target"
    }

    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
}

$headerText = Get-Content -LiteralPath $versionHeader -Raw -Encoding UTF8
$projectText = Get-Content -LiteralPath $managedProject -Raw -Encoding UTF8
$nativeVersionMatch = [regex]::Match($headerText, '#define\s+APP_VERSION\s+L"([^"]+)"')
$managedVersionMatch = [regex]::Match($projectText, '<Version>([^<]+)</Version>')
if (-not $nativeVersionMatch.Success -or -not $managedVersionMatch.Success) {
    throw "Could not read the native or managed application version."
}
if ($nativeVersionMatch.Groups[1].Value -ne $managedVersionMatch.Groups[1].Value) {
    throw "Version mismatch: src\app.h is '$($nativeVersionMatch.Groups[1].Value)', managed project is '$($managedVersionMatch.Groups[1].Value)'."
}

$msbuildPath = Get-MsBuildPath
foreach ($architecture in $Platform) {
    Write-Host "Building native CoreLib ($Configuration|$architecture)..."
    & $msbuildPath $nativeProject /t:Rebuild /m /nologo /v:minimal `
        "/p:Configuration=$Configuration" "/p:Platform=$architecture"
    if ($LASTEXITCODE -ne 0) {
        throw "Native $architecture build failed with exit code $LASTEXITCODE."
    }

    $nativeDll = Join-Path $projectRoot "CoreLib\bin\$architecture\CoreLib.dll"
    Assert-PeMachine -Path $nativeDll -Architecture $architecture

    Write-Host "Building command-line host ($Configuration|$architecture)..."
    & $msbuildPath $cliProject /t:Rebuild /m /nologo /v:minimal `
        "/p:Configuration=$Configuration" "/p:Platform=$architecture"
    if ($LASTEXITCODE -ne 0) {
        throw "Command-line host $architecture build failed with exit code $LASTEXITCODE."
    }

    $cliExecutable = Join-Path $projectRoot "CliHost\bin\$architecture\mrw-cli.exe"
    Assert-PeMachine -Path $cliExecutable -Architecture $architecture

    $rid = "win-$($architecture.ToLowerInvariant())"
    $publishDirectory = Join-Path $publishRoot $rid
    Remove-ArtifactDirectory -Path $publishDirectory

    Write-Host "Publishing WinUI application ($Configuration|$architecture)..."
    $publishArguments = @(
        "publish",
        $managedProject,
        "-c", $Configuration,
        "-p:Platform=$architecture",
        "-r", $rid,
        "--self-contained", "true",
        "-p:PublishDir=$publishDirectory\",
        "-v:minimal"
    )
    if ($NoRestore) {
        $publishArguments += "--no-restore"
    }

    & dotnet @publishArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Managed $architecture publish failed with exit code $LASTEXITCODE."
    }

    Copy-Item -LiteralPath $cliExecutable `
        -Destination (Join-Path $publishDirectory "mrw-cli.exe") -Force

    $publishedCore = Join-Path $publishDirectory "CoreLib.dll"
    $publishedCli = Join-Path $publishDirectory "mrw-cli.exe"
    $requiredPublishFiles = @(
        "memreduct-winui.exe",
        "mrw-cli.exe",
        "memreduct-winui.pri",
        "App.xbf",
        "MainWindow.xbf",
        "MainPage.xbf",
        "SettingsPage.xbf",
        "AboutPage.xbf"
    )
    foreach ($fileName in $requiredPublishFiles) {
        $publishedFile = Join-Path $publishDirectory $fileName
        if (-not (Test-Path -LiteralPath $publishedFile -PathType Leaf)) {
            throw "Published file is missing: $publishedFile"
        }
    }
    Assert-PeMachine -Path $publishedCore -Architecture $architecture
    Assert-PeMachine -Path $publishedCli -Architecture $architecture

    Copy-Item -LiteralPath (Join-Path $projectRoot "LICENSE") `
        -Destination (Join-Path $publishDirectory "LICENSE") -Force
}

Write-Host "Build completed. Published application directories are in '$publishRoot'."
