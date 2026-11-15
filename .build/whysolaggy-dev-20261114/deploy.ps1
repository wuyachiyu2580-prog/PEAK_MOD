$ErrorActionPreference = 'Stop'
$workspace = 'C:\Users\Administrator\Desktop\MOD\PEAK'
$buildRoot = Join-Path $workspace '.build\whysolaggy-dev-20261114'
$profileRoot = 'C:\Users\Administrator\AppData\Roaming\r2modmanPlus-local\PEAK\profiles\2.0.a\BepInEx'
$pluginRoot = Join-Path $profileRoot 'plugins'
$configPath = Join-Path $profileRoot 'config\com.wuyachiyu.WhySoLaggy.cfg'
if (Get-Process PEAK -ErrorAction SilentlyContinue) { throw 'PEAK is running; installation aborted.' }
[xml]$tests = Get-Content -LiteralPath (Join-Path $buildRoot 'results\final-verification.trx')
if ([int]$tests.TestRun.ResultSummary.Counters.failed -ne 0 -or [int]$tests.TestRun.ResultSummary.Counters.passed -ne 70) { throw 'Final 70-test verification missing.' }
$configBefore = (Get-FileHash -LiteralPath $configPath).Hash
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$rollback = Join-Path $buildRoot ('rollback-' + $stamp)
New-Item -ItemType Directory -Path $rollback | Out-Null
Copy-Item -LiteralPath $configPath -Destination (Join-Path $rollback 'com.wuyachiyu.WhySoLaggy.cfg')
$oldHashes = @{}
$newHashes = @{}
foreach ($name in @('WhySoLaggy.dll','WhySoLaggy.pdb')) {
    $source = Join-Path $buildRoot ('bin\' + $name)
    $target = Join-Path $pluginRoot $name
    if (!(Test-Path -LiteralPath $source)) { throw ('Missing artifact: ' + $name) }
    if (Test-Path -LiteralPath $target) {
        $oldHashes[$name] = (Get-FileHash -LiteralPath $target).Hash
        Copy-Item -LiteralPath $target -Destination (Join-Path $rollback $name)
    }
    $staged = Join-Path $pluginRoot ($name + '.wsl-staged')
    Copy-Item -LiteralPath $source -Destination $staged
    if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $staged).Hash) { throw 'Staged artifact mismatch.' }
    if (Get-Process PEAK -ErrorAction SilentlyContinue) { throw 'PEAK started during installation.' }
    if (Test-Path -LiteralPath $target) { [System.IO.File]::Replace($staged,$target,(Join-Path $rollback ($name + '.atomic-backup'))) } else { [System.IO.File]::Move($staged,$target) }
    $newHashes[$name] = (Get-FileHash -LiteralPath $target).Hash
    if ($newHashes[$name] -ne (Get-FileHash -LiteralPath $source).Hash) { throw 'Installed artifact mismatch.' }
}
$copies = @(Get-ChildItem -LiteralPath $pluginRoot -Filter WhySoLaggy.dll -File -Recurse)
if ($copies.Count -ne 1 -or $copies[0].FullName -ne (Join-Path $pluginRoot 'WhySoLaggy.dll')) { throw 'Duplicate or unexpected plugin install detected.' }
if ((Get-FileHash -LiteralPath $configPath).Hash -ne $configBefore) { throw 'Configuration unexpectedly changed.' }
$manifest = [ordered]@{
    installedAt=(Get-Date).ToString('o'); build='1.0.5-dev.20261115'; pluginVersion='1.0.5'; assemblyVersion='1.0.5.0';
    profile=$profileRoot; rollback=$rollback; oldHashes=$oldHashes; installedHashes=$newHashes;
    configSha256=$configBefore; dllCopies=$copies.Count; automatedTests=70;
    gameplayAcceptance='PENDING: no game launch, no real multiplayer/performance comparison'; releaseUpdated=$false
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $buildRoot 'installation.json') -Encoding UTF8
$manifest | ConvertTo-Json -Depth 5
