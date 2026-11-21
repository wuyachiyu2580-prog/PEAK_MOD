$ErrorActionPreference='Stop'
$workspace='C:\Users\Administrator\Desktop\MOD\PEAK'
$source=Join-Path $PSScriptRoot 'bin'
$profile='C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx'
$plugins=(Resolve-Path -LiteralPath (Join-Path $profile 'plugins')).Path
$expected='5FC795194989B4F0A47FF73B50914642FA86390526CD90EF2268B7D482AFBBC5'
if (Get-Process -Name PEAK -ErrorAction SilentlyContinue) { throw 'PEAK is running; deployment stopped before modifying files.' }
$names=@('WhereIsThing.dll','WhereIsThing.pdb')
$sourceInfo=@($names | ForEach-Object {
    $path=Join-Path $source $_
    $item=Get-Item -LiteralPath $path
    [pscustomobject]@{Name=$_;Path=$item.FullName;Bytes=$item.Length;SHA256=(Get-FileHash -LiteralPath $path).Hash}
})
if (($sourceInfo | Where-Object Name -eq 'WhereIsThing.dll').SHA256 -ne $expected) { throw 'Validated DLL hash mismatch.' }
$existing=@(Get-ChildItem -LiteralPath $plugins -Recurse -File | Where-Object {$_.Name -in $names})
$prefix=$plugins.TrimEnd('\')+'\'
foreach($item in $existing) {
    if (-not $item.FullName.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Existing file escaped profile plugins root.' }
    $stream=[IO.File]::Open($item.FullName,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    $stream.Dispose()
}
$cfg=@(Get-ChildItem -LiteralPath (Join-Path $profile 'config') -File | Where-Object {$_.Name -like '*WhereIsThing*'})
$configBefore=@($cfg | ForEach-Object {[pscustomobject]@{Path=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
$stamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$backup=Join-Path $PSScriptRoot ('deployment-'+$stamp)
New-Item -ItemType Directory -Path $backup | Out-Null
$previous=@()
foreach($item in $existing) {
    $relative=$item.FullName.Substring($prefix.Length)
    $target=Join-Path (Join-Path $backup 'previous') $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    $hash=(Get-FileHash -LiteralPath $item.FullName).Hash
    Copy-Item -LiteralPath $item.FullName -Destination $target
    if((Get-FileHash -LiteralPath $target).Hash -ne $hash) {throw 'Backup verification failed.'}
    $previous+=[pscustomobject]@{Original=$item.FullName;Backup=$target;SHA256=$hash;Bytes=$item.Length}
}
if (Get-Process -Name PEAK -ErrorAction SilentlyContinue) { throw 'PEAK started during backup; installation stopped.' }
foreach($item in $sourceInfo) {
    $target=Join-Path $plugins $item.Name
    Copy-Item -LiteralPath $item.Path -Destination $target -Force
    if ((Get-FileHash -LiteralPath $target).Hash -ne $item.SHA256) {throw 'Installed artifact hash mismatch.'}
}
foreach($item in $previous) {
    $parent=Split-Path -Parent $item.Original
    if ($parent -eq $plugins) {continue}
    $absolute=(Resolve-Path -LiteralPath $item.Original).Path
    if (-not $absolute.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) {throw 'Duplicate path escaped plugins root.'}
    if ((Get-FileHash -LiteralPath $absolute).Hash -ne $item.SHA256) {throw 'Duplicate changed after backup; preserving it.'}
    Remove-Item -LiteralPath $absolute
}
$installed=@(Get-ChildItem -LiteralPath $plugins -Recurse -File | Where-Object {$_.Name -in $names})
foreach($name in $names) {
    $matching=@($installed | Where-Object {$_.Name -eq $name})
    if($matching.Count -ne 1 -or $matching[0].DirectoryName -ne $plugins) {throw 'Duplicate or misplaced installation remains.'}
}
foreach($item in $configBefore) {
    if((Get-FileHash -LiteralPath $item.Path).Hash -ne $item.SHA256) {throw 'Configuration changed during deployment.'}
}
$result=[pscustomobject]@{
    deployedAt=(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')
    developmentVersion='0.1.3-dev.20261116.fixes'
    assemblyVersion=[Reflection.AssemblyName]::GetAssemblyName((Join-Path $plugins 'WhereIsThing.dll')).Version.ToString()
    destination=$plugins
    installed=@($installed | ForEach-Object {[pscustomobject]@{Path=$_.FullName;Bytes=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
    backup=$backup
    previous=$previous
    configBefore=$configBefore
    configUnchanged=$true
    gameWasStopped=$true
    inGameValidation=$false
    releaseChanged=$false
}
$json=$result | ConvertTo-Json -Depth 7
[IO.File]::WriteAllText((Join-Path $backup 'deployment.json'),$json,[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'deployment-latest.json'),$json,[Text.UTF8Encoding]::new($false))
$json
