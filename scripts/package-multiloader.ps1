param(
    [string]$Configuration = "Release",
    [string]$ReleaseName = "",
    [string]$OutputDirectory = "",
    [switch]$DeployUmm,
    [string]$GameDirectory = "D:\Steam\steamapps\common\A Dance of Fire and Ice"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
if ([string]::IsNullOrWhiteSpace($ReleaseName)) {
    $manifest = Get-Content -LiteralPath (Join-Path $projectRoot "Info.json") -Raw | ConvertFrom-Json
    $safeVersion = ($manifest.Version -replace '[^0-9A-Za-z._-]+', '_').Trim('_')
    $ReleaseName = "CheryTools_" + $safeVersion
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot "dist"
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$stageRoot = Join-Path $OutputDirectory ".staging"

function Assert-File([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required file is missing: $Path"
    }
}

function Copy-RequiredFile([string]$Source, [string]$DestinationDirectory) {
    Assert-File $Source
    New-Item -ItemType Directory -Force -Path $DestinationDirectory | Out-Null
    Copy-Item -LiteralPath $Source -Destination $DestinationDirectory -Force
}

function Copy-ManagedRuntime([string]$DestinationDirectory, [string]$CoreOutput) {
    $managedFiles = @(
        "CheryTools.dll",
        "ImGui.NET.dll",
        "System.Buffers.dll",
        "System.Numerics.Vectors.dll",
        "System.Runtime.CompilerServices.Unsafe.dll"
    )
    foreach ($file in $managedFiles) {
        Copy-RequiredFile (Join-Path $CoreOutput $file) $DestinationDirectory
    }
}

function Copy-ModData([string]$DestinationDirectory, [string]$CoreOutput) {
    Copy-RequiredFile (Join-Path $CoreOutput "cimgui.dll") $DestinationDirectory
    Copy-RequiredFile (Join-Path $projectRoot "strings.json") $DestinationDirectory
    Copy-Item -LiteralPath (Join-Path $projectRoot "Resources") `
        -Destination $DestinationDirectory -Recurse -Force
}

function New-Package([string]$StageDirectory, [string]$ZipPath) {
    if (Test-Path -LiteralPath $ZipPath) {
        Remove-Item -LiteralPath $ZipPath -Force
    }
    Compress-Archive -Path (Join-Path $StageDirectory "*") -DestinationPath $ZipPath -CompressionLevel Optimal
}

function Assert-ZipEntries([string]$ZipPath, [string[]]$RequiredEntries) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace("\", "/") })
        foreach ($required in $RequiredEntries) {
            if ($entries -notcontains $required) {
                throw "Package validation failed for $ZipPath (missing $required)"
            }
        }
        if ($entries | Where-Object { $_ -match '(^|/)Settings\.xml$' -or $_ -match '\.pdb$' }) {
            throw "Package validation failed for $ZipPath (contains settings or debug symbols)"
        }
    }
    finally {
        $archive.Dispose()
    }
}

Write-Host "Building CheryTools Core and loader adapters..."
& dotnet build (Join-Path $projectRoot "CheryTools.MultiLoader.slnx") -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

$coreOutput = Join-Path $projectRoot "bin\Core\$Configuration"
$ummOutput = Join-Path $projectRoot "bin\Loaders\UMM\$Configuration"
$melonOutput = Join-Path $projectRoot "bin\Loaders\MelonLoader\$Configuration"
$bepInExOutput = Join-Path $projectRoot "bin\Loaders\BepInEx\$Configuration"

$coreAssembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom((Join-Path $coreOutput "CheryTools.dll"))
$forbiddenLoaderReferences = @(
    $coreAssembly.GetReferencedAssemblies() |
        Where-Object { $_.Name -match 'UnityModManager|MelonLoader|BepInEx' }
)
if ($forbiddenLoaderReferences.Count -ne 0) {
    $names = ($forbiddenLoaderReferences | ForEach-Object Name) -join ", "
    throw "Core must not reference a loader assembly: $names"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$stageFullPath = [IO.Path]::GetFullPath($stageRoot)
if (-not $stageFullPath.StartsWith($OutputDirectory, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean staging directory outside output directory: $stageFullPath"
}
if (Test-Path -LiteralPath $stageFullPath) {
    Remove-Item -LiteralPath $stageFullPath -Recurse -Force
}

$ummStage = Join-Path $stageRoot "UMM"
Copy-ManagedRuntime $ummStage $coreOutput
Copy-ModData $ummStage $coreOutput
Copy-RequiredFile (Join-Path $ummOutput "CheryTools.Loader.UMM.dll") $ummStage
Copy-RequiredFile (Join-Path $projectRoot "Info.json") $ummStage

$melonStage = Join-Path $stageRoot "MelonLoader"
$melonMods = Join-Path $melonStage "Mods"
$melonUserLibs = Join-Path $melonStage "UserLibs"
$melonData = Join-Path $melonStage "UserData\CheryTools"
Copy-RequiredFile (Join-Path $coreOutput "CheryTools.dll") $melonMods
Copy-RequiredFile (Join-Path $melonOutput "CheryTools.Loader.MelonLoader.dll") $melonMods
foreach ($file in @("ImGui.NET.dll", "System.Buffers.dll", "System.Numerics.Vectors.dll", "System.Runtime.CompilerServices.Unsafe.dll")) {
    Copy-RequiredFile (Join-Path $coreOutput $file) $melonUserLibs
}
Copy-ModData $melonData $coreOutput

$bepInExStage = Join-Path $stageRoot "BepInEx"
$bepInExPlugin = Join-Path $bepInExStage "BepInEx\plugins\CheryTools"
Copy-ManagedRuntime $bepInExPlugin $coreOutput
Copy-ModData $bepInExPlugin $coreOutput
Copy-RequiredFile (Join-Path $bepInExOutput "CheryTools.Loader.BepInEx.dll") $bepInExPlugin

# The universal package mirrors each loader's native search path under one
# game-root archive. It intentionally contains runtime copies per loader;
# this avoids relying on symlinks or undocumented probing behavior.
$multiStage = Join-Path $stageRoot "MultiLoader"
$multiUmm = Join-Path $multiStage "Mods\CheryTools"
New-Item -ItemType Directory -Force -Path $multiUmm | Out-Null
Copy-Item -Path (Join-Path $ummStage "*") -Destination $multiUmm -Recurse -Force
Copy-Item -Path (Join-Path $melonStage "*") -Destination $multiStage -Recurse -Force
Copy-Item -Path (Join-Path $bepInExStage "*") -Destination $multiStage -Recurse -Force

$ummZip = Join-Path $OutputDirectory ($ReleaseName + "_UMM.zip")
$melonZip = Join-Path $OutputDirectory ($ReleaseName + "_MelonLoader.zip")
$bepInExZip = Join-Path $OutputDirectory ($ReleaseName + "_BepInEx.zip")
$multiZip = Join-Path $OutputDirectory ($ReleaseName + ".zip")

New-Package $ummStage $ummZip
New-Package $melonStage $melonZip
New-Package $bepInExStage $bepInExZip
New-Package $multiStage $multiZip

Assert-ZipEntries $ummZip @(
    "CheryTools.dll",
    "CheryTools.Loader.UMM.dll",
    "Info.json",
    "cimgui.dll",
    "Resources/Defaults/CheryTools_Default_Jipper.cyt"
)
Assert-ZipEntries $melonZip @(
    "Mods/CheryTools.dll",
    "Mods/CheryTools.Loader.MelonLoader.dll",
    "UserLibs/ImGui.NET.dll",
    "UserData/CheryTools/cimgui.dll",
    "UserData/CheryTools/Resources/Defaults/CheryTools_Default_Jipper.cyt"
)
Assert-ZipEntries $bepInExZip @(
    "BepInEx/plugins/CheryTools/CheryTools.dll",
    "BepInEx/plugins/CheryTools/CheryTools.Loader.BepInEx.dll",
    "BepInEx/plugins/CheryTools/cimgui.dll",
    "BepInEx/plugins/CheryTools/Resources/Defaults/CheryTools_Default_Jipper.cyt"
)
Assert-ZipEntries $multiZip @(
    "Mods/CheryTools/CheryTools.dll",
    "Mods/CheryTools/CheryTools.Loader.UMM.dll",
    "Mods/CheryTools/Info.json",
    "Mods/CheryTools.Loader.MelonLoader.dll",
    "UserLibs/ImGui.NET.dll",
    "UserData/CheryTools/cimgui.dll",
    "BepInEx/plugins/CheryTools/CheryTools.dll",
    "BepInEx/plugins/CheryTools/CheryTools.Loader.BepInEx.dll"
)

if ($DeployUmm) {
    $ummDestination = [IO.Path]::GetFullPath((Join-Path $GameDirectory "Mods\CheryTools"))
    $expectedRoot = [IO.Path]::GetFullPath($GameDirectory)
    if (-not $ummDestination.StartsWith($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to deploy outside the game directory: $ummDestination"
    }
    New-Item -ItemType Directory -Force -Path $ummDestination | Out-Null
    Copy-Item -Path (Join-Path $ummStage "*") -Destination $ummDestination -Recurse -Force
    Write-Host "UMM build deployed to: $ummDestination"
}

Remove-Item -LiteralPath $stageFullPath -Recurse -Force
Write-Host "Packages created and validated:"
Write-Host "  $ummZip"
Write-Host "  $melonZip"
Write-Host "  $bepInExZip"
Write-Host "  $multiZip"
