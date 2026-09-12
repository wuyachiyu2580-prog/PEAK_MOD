param([switch]$Deploy)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'StateKeeper/StateKeeper.csproj'
[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$version = [string]$projectXml.Project.PropertyGroup.Version
$release = Join-Path $root "发行/$version"
$build = Join-Path $root '.build/release'
$manifest = Get-Content -LiteralPath (Join-Path $release 'manifest.json') -Raw | ConvertFrom-Json
if ($manifest.name -ne 'StateKeeper' -or $manifest.version_number -ne $version -or $manifest.description.Length -gt 135) {
    throw 'Release manifest name, version or description is invalid.'
}
if (@($manifest.dependencies).Count -ne 1 -or $manifest.dependencies[0] -ne 'BepInEx-BepInExPack_PEAK-5.4.2403') {
    throw 'Unexpected runtime dependencies.'
}

& dotnet build $project -c Release --no-restore -warnaserror "-p:OutputPath=$build/"
if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
$dll = Join-Path $build 'StateKeeper.dll'
if ([Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString() -ne "$version.0") { throw 'Assembly version mismatch.' }
Copy-Item -LiteralPath $dll -Destination (Join-Path $release 'StateKeeper.dll') -Force

# A reproducible package icon: the same status channels used by the report UI.
Add-Type -AssemblyName System.Drawing
$bitmap = [Drawing.Bitmap]::new(256, 256)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$objects = [Collections.Generic.List[IDisposable]]::new()
try {
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $graphics.Clear([Drawing.Color]::FromArgb(24, 29, 30))
    $white = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(245, 246, 244)); $objects.Add($white)
    $green = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(88, 202, 132)); $objects.Add($green)
    $gold = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(234, 193, 86)); $objects.Add($gold)
    $red = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(207, 89, 104)); $objects.Add($red)
    $muted = [Drawing.Pen]::new([Drawing.Color]::FromArgb(72, 83, 84), 2); $objects.Add($muted)
    $line = [Drawing.Pen]::new([Drawing.Color]::FromArgb(123, 207, 215), 7); $objects.Add($line)
    $line.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
    $titleFont = [Drawing.Font]::new('Arial', 39, [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel); $objects.Add($titleFont)
    $nameFont = [Drawing.Font]::new('Arial', 16, [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel); $objects.Add($nameFont)
    $graphics.DrawString('SK', $titleFont, $white, 23, 14)
    $graphics.DrawString('STATE KEEPER', $nameFont, $white, 27, 215)
    $graphics.DrawLine($muted, 30, 193, 226, 193)
    $graphics.DrawLine($muted, 30, 142, 226, 142)
    $graphics.DrawLine($muted, 30, 91, 226, 91)
    $graphics.DrawLines($line, [Drawing.PointF[]]@(
        [Drawing.PointF]::new(30, 178), [Drawing.PointF]::new(71, 165),
        [Drawing.PointF]::new(104, 180), [Drawing.PointF]::new(150, 112),
        [Drawing.PointF]::new(181, 133), [Drawing.PointF]::new(223, 84)))
    $graphics.FillRectangle($green, 128, 29, 54, 12)
    $graphics.FillRectangle($red, 185, 29, 38, 12)
    $graphics.FillRectangle($gold, 128, 47, 68, 9)
    $bitmap.Save((Join-Path $release 'icon.png'), [Drawing.Imaging.ImageFormat]::Png)
} finally {
    foreach ($item in $objects) { $item.Dispose() }
    $graphics.Dispose(); $bitmap.Dispose()
}

$files = @('StateKeeper.dll', 'manifest.json', 'README.md', 'CHANGELOG.md', 'icon.png')
$actual = @(Get-ChildItem -LiteralPath $release -Force | ForEach-Object Name)
if (@(Compare-Object $files $actual).Count -ne 0) { throw 'Release directory contains missing or unexpected files; refusing to package.' }
foreach ($doc in @('README.md', 'CHANGELOG.md')) {
    if ((Get-Content -LiteralPath (Join-Path $release $doc) -TotalCount 1) -ne "# StateKeeper $version") { throw "Invalid title in $doc" }
    Copy-Item -LiteralPath (Join-Path $release $doc) -Destination (Join-Path $root $doc) -Force
}

$zipPath = Join-Path (Split-Path $release -Parent) "StateKeeper-$version.zip"
$output = [IO.File]::Open($zipPath, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
$archive = [IO.Compression.ZipArchive]::new($output, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($name in $files) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $release $name), $name, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose(); $output.Dispose() }

$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    if (@(Compare-Object $files @($archive.Entries.FullName)).Count -ne 0) { throw 'ZIP file inventory mismatch.' }
    foreach ($entry in $archive.Entries) {
        $stream = $entry.Open(); $sha = [Security.Cryptography.SHA256]::Create()
        try { $actualHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $sha.Dispose(); $stream.Dispose() }
        if ($actualHash -ne (Get-FileHash -LiteralPath (Join-Path $release $entry.FullName) -Algorithm SHA256).Hash) {
            throw "ZIP content mismatch: $($entry.FullName)"
        }
    }
} finally { $archive.Dispose() }

if ($Deploy) {
    Copy-Item -LiteralPath $dll -Destination (Join-Path ([string]$projectXml.Project.PropertyGroup.OutputPath) 'StateKeeper.dll') -Force
}
Get-FileHash -LiteralPath (Join-Path $release 'StateKeeper.dll'), $zipPath -Algorithm SHA256 | Format-List Path, Hash
