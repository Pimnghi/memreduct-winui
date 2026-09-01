<#
.SYNOPSIS
Builds self-contained per-machine installers for x64 and ARM64.
#>
[CmdletBinding()]
param(
    [ValidateSet("x64", "ARM64")]
    [string[]]$Platform = @("x64", "ARM64"),

    [switch]$NoRestore,

    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$buildScript = Join-Path $PSScriptRoot "build-publish.ps1"
$installerScript = Join-Path $projectRoot "packaging\installer\memreduct-winui.iss"
$installedMarker = Join-Path $projectRoot "packaging\installer\installed.marker"
$versionHeader = Join-Path $projectRoot "src\MemReduct.WinUI.Shared\app.h"
$artifactRoot = Join-Path $projectRoot "artifacts"
$publishRoot = Join-Path $artifactRoot "publish"
$installerBaseRoot = Join-Path $artifactRoot "installer"
$stagingBaseRoot = Join-Path $artifactRoot "installer-staging"

function Remove-SafeDirectory {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$AllowedRoot
    )

    $root = [IO.Path]::GetFullPath($AllowedRoot).TrimEnd('\')
    $target = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    if (-not $target.StartsWith(
            $root + '\',
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a directory outside '$root': $target"
    }

    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
}

function Get-InnoCompiler {
    if ((-not [string]::IsNullOrWhiteSpace($env:INNO_SETUP_COMPILER)) -and
        (Test-Path -LiteralPath $env:INNO_SETUP_COMPILER -PathType Leaf)) {
        return [IO.Path]::GetFullPath($env:INNO_SETUP_COMPILER)
    }

    $command = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        (Join-Path ([Environment]::GetFolderPath("LocalApplicationData")) "Programs\Inno Setup 7\ISCC.exe"),
        (Join-Path ([Environment]::GetFolderPath("LocalApplicationData")) "Programs\Inno Setup 6\ISCC.exe"),
        (Join-Path ([Environment]::GetFolderPath("ProgramFiles")) "Inno Setup 7\ISCC.exe"),
        (Join-Path ([Environment]::GetFolderPath("ProgramFilesX86")) "Inno Setup 7\ISCC.exe"),
        (Join-Path ([Environment]::GetFolderPath("ProgramFilesX86")) "Inno Setup 6\ISCC.exe"),
        (Join-Path ([Environment]::GetFolderPath("ProgramFiles")) "Inno Setup 6\ISCC.exe")
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    throw "Inno Setup 7.0.2 or later was not found. Install Inno Setup or set INNO_SETUP_COMPILER to ISCC.exe."
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

    $expected = if ($Architecture -eq "x64") { 0x8664 } else { 0xAA64 }
    $actual = Get-PeMachine -Path $Path
    if ($actual -ne $expected) {
        throw "Architecture mismatch for '$Path': expected 0x$($expected.ToString('X4')), got 0x$($actual.ToString('X4'))."
    }
}

function Copy-InstallerPayload {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination
    )

    $sourcePrefix = [IO.Path]::GetFullPath($Source).TrimEnd('\') + '\'
    foreach ($file in Get-ChildItem -LiteralPath $Source -Recurse -File) {
        $relativePath = $file.FullName.Substring($sourcePrefix.Length)
        $pathParts = $relativePath -split '[\\/]'
        if (($pathParts -contains "data") -or
            ($file.Extension -in @(".pdb", ".ini", ".log"))) {
            continue
        }

        $destinationPath = Join-Path $Destination $relativePath
        $destinationDirectory = Split-Path -Parent $destinationPath
        if (-not (Test-Path -LiteralPath $destinationDirectory)) {
            New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
        }
        Copy-Item -LiteralPath $file.FullName -Destination $destinationPath -Force
    }
}

$headerText = Get-Content -LiteralPath $versionHeader -Raw -Encoding UTF8
$versionMatch = [regex]::Match($headerText, '#define\s+APP_VERSION\s+L"([^"]+)"')
if (-not $versionMatch.Success) {
    throw "Could not read APP_VERSION from '$versionHeader'."
}
$version = $versionMatch.Groups[1].Value
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "APP_VERSION must use the major.minor.patch format: '$version'."
}

$outputRoot = Join-Path $installerBaseRoot $version
$stagingRoot = Join-Path $stagingBaseRoot $version

$innoCompiler = Get-InnoCompiler
if (-not $SkipBuild) {
    & $buildScript `
        -Configuration Release `
        -Platform $Platform `
        -NoRestore:$NoRestore
}

Remove-SafeDirectory -Path $outputRoot -AllowedRoot $installerBaseRoot
Remove-SafeDirectory -Path $stagingRoot -AllowedRoot $stagingBaseRoot
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

$records = [Collections.Generic.List[object]]::new()
try {
    foreach ($architecture in $Platform) {
        $rid = "win-$($architecture.ToLowerInvariant())"
        $publishDirectory = Join-Path $publishRoot $rid
        $payloadDirectory = Join-Path $stagingRoot $rid
        New-Item -ItemType Directory -Path $payloadDirectory -Force | Out-Null
        Copy-InstallerPayload -Source $publishDirectory -Destination $payloadDirectory

        Copy-Item -LiteralPath $installedMarker `
            -Destination (Join-Path $payloadDirectory "installed.marker") -Force
        foreach ($document in @("LICENSE", "README.md", "README_EN.md")) {
            Copy-Item -LiteralPath (Join-Path $projectRoot $document) `
                -Destination (Join-Path $payloadDirectory $document) -Force
        }

        $documentationDirectory = Join-Path $payloadDirectory "docs"
        New-Item -ItemType Directory -Path $documentationDirectory -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $projectRoot "docs\images") `
            -Destination $documentationDirectory -Recurse -Force

        foreach ($binary in @("memreduct-winui.exe", "mrw-cli.exe", "CoreLib.dll")) {
            $binaryPath = Join-Path $payloadDirectory $binary
            if (-not (Test-Path -LiteralPath $binaryPath -PathType Leaf)) {
                throw "Required installer payload file is missing: $binaryPath"
            }
            Assert-PeMachine -Path $binaryPath -Architecture $architecture
        }

        $forbiddenFiles = @(
            Get-ChildItem -LiteralPath $payloadDirectory -Recurse -File |
                Where-Object {
                    ($_.Extension -in @(".pdb", ".ini", ".log")) -or
                    ($_.FullName -split '[\\/]' -contains "data")
                }
        )
        if ($forbiddenFiles.Count -ne 0) {
            throw "Installer payload contains excluded files: $($forbiddenFiles.FullName -join ', ')"
        }

        & $innoCompiler `
            "/DAppVersion=$version" `
            "/DArchitecture=$architecture" `
            "/DSourceDir=$payloadDirectory" `
            "/DOutputDir=$outputRoot" `
            $installerScript
        if ($LASTEXITCODE -ne 0) {
            throw "Inno Setup compilation failed for $architecture with exit code $LASTEXITCODE."
        }

        $installerName = "MemReductWinUI-$version-$rid-setup.exe"
        $installerPath = Join-Path $outputRoot $installerName
        if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
            throw "Expected installer was not generated: $installerPath"
        }

        $file = Get-Item -LiteralPath $installerPath
        $hash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $records.Add([pscustomobject]@{
            Architecture = $architecture
            Rid = $rid
            FileName = $installerName
            SizeBytes = $file.Length
            Sha256 = $hash
        })
    }

    $utf8NoBom = [Text.UTF8Encoding]::new($false)
    $checksumLines = @(
        $records | ForEach-Object { "$($_.Sha256)  $($_.FileName)" }
    )
    [IO.File]::WriteAllText(
        (Join-Path $outputRoot "SHA256SUMS.txt"),
        ($checksumLines -join "`r`n") + "`r`n",
        $utf8NoBom)

    $manifest = [ordered]@{
        schemaVersion = 1
        product = "Mem Reduct WinUI"
        version = $version
        installer = "Inno Setup"
        installScope = "perMachine"
        selfContained = $true
        signed = $false
        configurationDirectory = "%ProgramData%\Mem Reduct WinUI\data"
        files = @(
            $records | ForEach-Object {
                [ordered]@{
                    architecture = $_.Architecture
                    runtimeIdentifier = $_.Rid
                    fileName = $_.FileName
                    sizeBytes = $_.SizeBytes
                    sha256 = $_.Sha256
                }
            }
        )
    }
    [IO.File]::WriteAllText(
        (Join-Path $outputRoot "installer-manifest.json"),
        ($manifest | ConvertTo-Json -Depth 5) + "`r`n",
        $utf8NoBom)
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-SafeDirectory -Path $stagingRoot -AllowedRoot $artifactRoot
    }

    if ((Test-Path -LiteralPath $stagingBaseRoot) -and
        -not (Get-ChildItem -LiteralPath $stagingBaseRoot -Force)) {
        Remove-SafeDirectory -Path $stagingBaseRoot -AllowedRoot $artifactRoot
    }
}

Write-Host "Installer build completed. Files are in '$outputRoot'."
