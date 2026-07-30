<#
.SYNOPSIS
Builds versioned portable ZIP packages, checksums, and a release manifest.
#>
[CmdletBinding()]
param(
    [switch]$NoRestore,

    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Split-Path -Parent $projectRoot
$buildScript = Join-Path $projectRoot "build-publish.ps1"
$versionHeader = Join-Path $repositoryRoot "src\app.h"
$artifactRoot = Join-Path $projectRoot "artifacts"
$publishRoot = Join-Path $artifactRoot "publish"
$portableRoot = Join-Path $artifactRoot "portable"
$stagingRoot = Join-Path $portableRoot "staging"
$expectedVersion = "1.0.0"
$architectures = @(
    [pscustomobject]@{
        Name = "x64"
        Rid = "win-x64"
        PeMachine = 0x8664
    },
    [pscustomobject]@{
        Name = "ARM64"
        Rid = "win-arm64"
        PeMachine = 0xAA64
    }
)

function Remove-SafeDirectory {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$AllowedRoot
    )

    $root = [IO.Path]::GetFullPath($AllowedRoot).TrimEnd('\')
    $target = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $isRoot = $target.Equals($root, [StringComparison]::OrdinalIgnoreCase)
    $isChild = $target.StartsWith(
        $root + '\',
        [StringComparison]::OrdinalIgnoreCase)
    if (-not $isRoot -and -not $isChild) {
        throw "Refusing to remove a directory outside '$root': $target"
    }

    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
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
        [Parameter(Mandatory)][UInt16]$ExpectedMachine
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required native binary is missing: $Path"
    }

    $actualMachine = Get-PeMachine -Path $Path
    if ($actualMachine -ne $ExpectedMachine) {
        throw "Architecture mismatch for '$Path': expected 0x$($ExpectedMachine.ToString('X4')), got 0x$($actualMachine.ToString('X4'))."
    }
}

function Copy-PortableFiles {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination
    )

    $sourcePrefix = [IO.Path]::GetFullPath($Source).TrimEnd('\') + '\'
    foreach ($file in Get-ChildItem -LiteralPath $Source -Recurse -File) {
        $relativePath = $file.FullName.Substring($sourcePrefix.Length)
        $pathParts = $relativePath -split '[\\/]'
        $isUserData = $pathParts -contains "data"
        $isExcludedExtension = $file.Extension -in @(".pdb", ".ini", ".log")
        if ($isUserData -or $isExcludedExtension) {
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

function Assert-Unsigned {
    param([Parameter(Mandatory)][string[]]$Paths)

    foreach ($path in $Paths) {
        $signature = Get-AuthenticodeSignature -LiteralPath $path
        if ($signature.Status -ne [Management.Automation.SignatureStatus]::NotSigned) {
            throw "Expected an unsigned release binary, but '$path' has signature status '$($signature.Status)'."
        }
    }
}

function Assert-ZipContents {
    param(
        [Parameter(Mandatory)][string]$ZipPath,
        [Parameter(Mandatory)][string]$TopLevelDirectory
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $entries = @(
            $archive.Entries |
                ForEach-Object { $_.FullName.Replace('\', '/') } |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        )
        if ($entries.Count -eq 0) {
            throw "Archive is empty: $ZipPath"
        }

        $roots = @(
            $entries |
                ForEach-Object { $_.TrimEnd('/').Split('/')[0] } |
                Sort-Object -Unique
        )
        if ($roots.Count -ne 1 -or $roots[0] -cne $TopLevelDirectory) {
            throw "Archive '$ZipPath' must contain only the '$TopLevelDirectory' top-level directory."
        }

        $requiredEntries = @(
            "$TopLevelDirectory/memreduct-winui.exe",
            "$TopLevelDirectory/mrw-cli.exe",
            "$TopLevelDirectory/CoreLib.dll",
            "$TopLevelDirectory/language/memreduct-winui.lng",
            "$TopLevelDirectory/Assets/AppIcon.ico",
            "$TopLevelDirectory/LICENSE",
            "$TopLevelDirectory/README.md",
            "$TopLevelDirectory/README_EN.md"
        )
        foreach ($requiredEntry in $requiredEntries) {
            if ($entries -notcontains $requiredEntry) {
                throw "Required archive entry is missing from '$ZipPath': $requiredEntry"
            }
        }

        $forbiddenEntries = @(
            $entries | Where-Object {
                ($_ -match '(?i)\.(pdb|ini|log)$') -or
                ($_ -match '(?i)(^|/)data(/|$)')
            }
        )
        if ($forbiddenEntries.Count -ne 0) {
            throw "Archive '$ZipPath' contains excluded files: $($forbiddenEntries -join ', ')"
        }
    }
    finally {
        $archive.Dispose()
    }
}

$headerText = Get-Content -LiteralPath $versionHeader -Raw -Encoding UTF8
$versionMatch = [regex]::Match($headerText, '#define\s+APP_VERSION\s+L"([^"]+)"')
if (-not $versionMatch.Success) {
    throw "Could not read APP_VERSION from '$versionHeader'."
}
$version = $versionMatch.Groups[1].Value
if ($version -ne $expectedVersion) {
    throw "This release script packages version '$expectedVersion', but APP_VERSION is '$version'."
}

if (-not $SkipBuild) {
    Write-Host "Building Mem Reduct WinUI $version for x64 and ARM64..."
    & $buildScript `
        -Configuration Release `
        -Platform @("x64", "ARM64") `
        -NoRestore:$NoRestore
}

Remove-SafeDirectory -Path $portableRoot -AllowedRoot $artifactRoot
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

$packageRecords = [Collections.Generic.List[object]]::new()
try {
    foreach ($architecture in $architectures) {
        $publishDirectory = Join-Path $publishRoot $architecture.Rid
        if (-not (Test-Path -LiteralPath $publishDirectory -PathType Container)) {
            throw "Published directory is missing: $publishDirectory"
        }

        $packageName = "MemReductWinUI-$version-$($architecture.Rid)"
        $packageDirectory = Join-Path $stagingRoot $packageName
        New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
        Copy-PortableFiles -Source $publishDirectory -Destination $packageDirectory

        Copy-Item -LiteralPath (Join-Path $projectRoot "LICENSE") `
            -Destination (Join-Path $packageDirectory "LICENSE") -Force
        Copy-Item -LiteralPath (Join-Path $projectRoot "README.md") `
            -Destination (Join-Path $packageDirectory "README.md") -Force
        Copy-Item -LiteralPath (Join-Path $projectRoot "README_EN.md") `
            -Destination (Join-Path $packageDirectory "README_EN.md") -Force

        $mainExecutable = Join-Path $packageDirectory "memreduct-winui.exe"
        $cliExecutable = Join-Path $packageDirectory "mrw-cli.exe"
        $coreLibrary = Join-Path $packageDirectory "CoreLib.dll"
        foreach ($nativeBinary in @($mainExecutable, $cliExecutable, $coreLibrary)) {
            Assert-PeMachine `
                -Path $nativeBinary `
                -ExpectedMachine $architecture.PeMachine
        }
        Assert-Unsigned -Paths @($mainExecutable, $cliExecutable, $coreLibrary)

        $mainVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo(
            $mainExecutable).ProductVersion
        $cliVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo(
            $cliExecutable).ProductVersion
        if ((-not $mainVersion.StartsWith($version, [StringComparison]::Ordinal)) -or
            (-not $cliVersion.StartsWith($version, [StringComparison]::Ordinal))) {
            throw "Version metadata mismatch in '$packageDirectory'."
        }

        $zipFileName = "$packageName.zip"
        $zipPath = Join-Path $portableRoot $zipFileName
        Write-Host "Creating $zipFileName..."
        Compress-Archive `
            -LiteralPath $packageDirectory `
            -DestinationPath $zipPath `
            -CompressionLevel Optimal
        Assert-ZipContents -ZipPath $zipPath -TopLevelDirectory $packageName

        $zipFile = Get-Item -LiteralPath $zipPath
        $sha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $packageRecords.Add([pscustomobject]@{
            Architecture = $architecture.Name
            Rid = $architecture.Rid
            FileName = $zipFileName
            SizeBytes = $zipFile.Length
            Sha256 = $sha256
        })
    }

    $checksumLines = @(
        $packageRecords |
            ForEach-Object { "$($_.Sha256)  $($_.FileName)" }
    )
    $utf8NoBom = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText(
        (Join-Path $portableRoot "SHA256SUMS.txt"),
        ($checksumLines -join "`r`n") + "`r`n",
        $utf8NoBom)

    $manifest = [ordered]@{
        schemaVersion = 1
        product = "Mem Reduct WinUI"
        version = $version
        portable = $true
        selfContained = $true
        signed = $false
        minimumWindowsVersion = "10.0.17763.0"
        files = @(
            $packageRecords | ForEach-Object {
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
    $manifestJson = $manifest | ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText(
        (Join-Path $portableRoot "release-manifest.json"),
        $manifestJson + "`r`n",
        $utf8NoBom)

    $parsedManifest = Get-Content `
        -LiteralPath (Join-Path $portableRoot "release-manifest.json") `
        -Raw `
        -Encoding UTF8 |
        ConvertFrom-Json
    if (($parsedManifest.version -ne $version) -or
        ($parsedManifest.files.Count -ne $packageRecords.Count) -or
        ($parsedManifest.signed -ne $false)) {
        throw "Generated release manifest failed validation."
    }

    foreach ($record in $packageRecords) {
        $zipPath = Join-Path $portableRoot $record.FileName
        $actualHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $manifestEntry = @(
            $parsedManifest.files |
                Where-Object { $_.fileName -eq $record.FileName }
        )
        if (($actualHash -ne $record.Sha256) -or
            ($manifestEntry.Count -ne 1) -or
            ($manifestEntry[0].sha256 -ne $actualHash) -or
            ([Int64]$manifestEntry[0].sizeBytes -ne (Get-Item -LiteralPath $zipPath).Length)) {
            throw "Release hash or manifest validation failed for '$($record.FileName)'."
        }
    }
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-SafeDirectory -Path $stagingRoot -AllowedRoot $portableRoot
    }
}

Write-Host "Portable package build completed. Files are in '$portableRoot'."
