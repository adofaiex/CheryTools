param(
    [string]$Configuration = 'Release',
    [string]$PackageName = '',
    [string]$TargetGameDir = 'C:\Program Files (x86)\Steam\steamapps\common\A Dance of Fire and Ice\Mods\CheryTools',
    [switch]$Deploy,
    # Build against a saved copy of the game's Managed folder
    # instead of the installed game, when the game is on another branch.
    [string]$ManagedAssembliesDir = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
# The release version has a single source: CheryToolsVersion in Directory.Build.props.
[xml]$buildProps = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw -Encoding UTF8
$version = ($buildProps.Project.PropertyGroup | ForEach-Object { $_.CheryToolsVersion } | Where-Object { $_ } | Select-Object -First 1)
if ([string]::IsNullOrWhiteSpace($version)) { throw 'CheryToolsVersion is missing from Directory.Build.props.' }
$version = $version.Trim()
if ([string]::IsNullOrWhiteSpace($PackageName)) { $PackageName = "CheryTools_Sonnet_$version.zip" }
Write-Host "Version: $version"
$libRoot = Join-Path $root 'lib'
$packageRoot = Join-Path $root ('dist\.package-' + [Guid]::NewGuid().ToString('N'))
$modulesRoot = Join-Path $packageRoot 'Modules'
$resourcesRoot = Join-Path $packageRoot 'Resources'

$buildArgs = @((Join-Path $root "CheryTools.Sonnet.sln"), '-c', $Configuration)
if (-not [string]::IsNullOrWhiteSpace($ManagedAssembliesDir)) {
    $buildArgs += "-p:ManagedAssembliesDir=$([System.IO.Path]::GetFullPath($ManagedAssembliesDir))"
}
dotnet build @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw 'Sonnet build failed; packaging stopped.'
}

New-Item -ItemType Directory -Path $modulesRoot -Force | Out-Null
New-Item -ItemType Directory -Path $resourcesRoot -Force | Out-Null

$ffmpegSourceDir = Join-Path $root 'ThirdParty\FFmpeg'
if (Test-Path -LiteralPath $ffmpegSourceDir) {
    $thirdPartyRoot = Join-Path $packageRoot 'ThirdParty\FFmpeg'
    New-Item -ItemType Directory -Path $thirdPartyRoot -Force | Out-Null
    Copy-Item -Path (Join-Path $ffmpegSourceDir '*') -Destination $thirdPartyRoot -Force
}

Copy-Item -LiteralPath (Join-Path $root 'out\Framework\CheryTools.Sonnet.Framework.dll') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $root 'out\Contracts\CheryTools.Sonnet.Contracts.dll') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $root 'out\Modules\CheryTools.Tools.Preview.dll') -Destination $modulesRoot
Copy-Item -LiteralPath (Join-Path $root 'out\Modules\CheryTools.KeyViewer.Preview.dll') -Destination $modulesRoot
Copy-Item -LiteralPath (Join-Path $root 'out\Modules\CheryTools.Overlayer.Preview.dll') -Destination $modulesRoot
Copy-Item -LiteralPath (Join-Path $root 'out\Modules\CheryTools.AetRecorder.Preview.dll') -Destination $modulesRoot
# ReadHelper 实验模块暂不随默认包发布（保留源码与工程，需要时取消注释即可带入）
# Copy-Item -LiteralPath (Join-Path $root 'out\Modules\CheryTools.ReadHelper.Preview.dll') -Destination $modulesRoot
$defaultsSourceDir = Join-Path $root 'Package\Defaults'
if (Test-Path -LiteralPath $defaultsSourceDir) {
    $defaultsDestDir = Join-Path $packageRoot 'Defaults'
    New-Item -ItemType Directory -Path $defaultsDestDir -Force | Out-Null
    Copy-Item -Path (Join-Path $defaultsSourceDir '*') -Destination $defaultsDestDir -Force
}

$infoJson = Get-Content -LiteralPath (Join-Path $root 'Package\Info.json') -Raw -Encoding UTF8
$infoJson = [regex]::Replace($infoJson, '("Version"\s*:\s*")[^"]*(")', "`${1}$version`${2}")
[System.IO.File]::WriteAllText((Join-Path $packageRoot 'Info.json'), $infoJson, (New-Object System.Text.UTF8Encoding($false)))
Copy-Item -LiteralPath (Join-Path $root 'Resources\MiSans-Bold.ttf') -Destination $resourcesRoot
Copy-Item -LiteralPath (Join-Path $root 'Resources\CyreneBackground.png') -Destination $resourcesRoot

# GPL-3.0 requires the license to travel with every binary package; Contracts carries its own MIT
# license, and the notices cover the bundled third-party files.
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $root 'Contracts\LICENSE') -Destination (Join-Path $packageRoot 'LICENSE-Contracts')
Copy-Item -LiteralPath (Join-Path $root 'THIRD_PARTY_NOTICES.md') -Destination $packageRoot

$runtimeFiles = @(
    'cimgui.dll',
    'ImGui.NET.dll',
    'System.Buffers.dll',
    'System.Numerics.Vectors.dll',
    'System.Runtime.CompilerServices.Unsafe.dll'
)

foreach ($file in $runtimeFiles) {
    $source = Join-Path $libRoot $file
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Missing runtime dependency: $source"
    }
    Copy-Item -LiteralPath $source -Destination $packageRoot
}

$zipPath = Join-Path $root ('dist\' + $PackageName)
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $packageRoot,
    $zipPath,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false)

[System.GC]::Collect()
[System.GC]::WaitForPendingFinalizers()
Start-Sleep -Milliseconds 200
try {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force -ErrorAction SilentlyContinue
} catch {}

Write-Host "Package: $zipPath"
Get-FileHash -LiteralPath $zipPath -Algorithm SHA256

if ($Deploy) {
    Write-Host "Deploying directly to $TargetGameDir ..."
    # Never wipe the target directory: it holds user data the package does not ship
    # (Framework.xml, Modules\*.xml, Modules\*.json, imported assets, AetRecorder workspace).
    # Only stale binaries are removed; package files are then extracted over the top.
    New-Item -ItemType Directory -Path $TargetGameDir -Force | Out-Null
    $staleBinaries = @(Get-ChildItem -LiteralPath $TargetGameDir -File -Filter '*.dll')
    $targetModulesDir = Join-Path $TargetGameDir 'Modules'
    if (Test-Path -LiteralPath $targetModulesDir) {
        $staleBinaries += @(Get-ChildItem -LiteralPath $targetModulesDir -File -Filter '*.dll')
    }
    $staleBinaries += @(Get-ChildItem -LiteralPath $TargetGameDir -File -Filter '*.dll.*.cache')
    foreach ($binary in $staleBinaries) {
        Remove-Item -LiteralPath $binary.FullName -Force
    }

    $targetFull = [System.IO.Path]::GetFullPath($TargetGameDir).TrimEnd('\') + '\'
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        foreach ($entry in $archive.Entries) {
            $destPath = [System.IO.Path]::GetFullPath((Join-Path $TargetGameDir $entry.FullName))
            if (-not $destPath.StartsWith($targetFull, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Refusing to extract entry outside target directory: $($entry.FullName)"
            }
            if ([string]::IsNullOrEmpty($entry.Name)) {
                New-Item -ItemType Directory -Path $destPath -Force | Out-Null
                continue
            }
            New-Item -ItemType Directory -Path (Split-Path -Parent $destPath) -Force | Out-Null
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destPath, $true)
        }
    } finally {
        $archive.Dispose()
    }
    Write-Host "Deployed successfully to game directory! User configs and data were left untouched."
}
